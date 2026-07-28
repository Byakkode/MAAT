# Spécification — Modèle de données

Schéma relationnel PostgreSQL 18, accédé via EF Core 10 (code-first).

**Conventions.** Tables et colonnes en `snake_case` côté PostgreSQL, entités et
propriétés en `PascalCase` côté C#. Le mapping est déclaré explicitement dans les
`IEntityTypeConfiguration<T>` sous `MAAT.Infrastructure/Persistence/Configurations/`,
jamais par convention implicite.

Clés primaires : `uuid` (`Guid` en C#), générées côté application. Pas d'entier
auto-incrémenté : les identifiants apparaissent dans les URL d'API et ne doivent
pas être énumérables.

Horodatages : `timestamptz`, toujours en UTC. La conversion en heure locale est
une responsabilité du frontend.

---

## Les cinq domaines RSE

Toute référence à un domaine dans ce document et dans le code utilise l'énumération
suivante, alignée sur les référentiels VSME, ISO 26000, ESRS et EcoVadis.

| Valeur `RseDomain` | Libellé | Ancrage normatif |
| --- | --- | --- |
| `Environmental` | Environnement | ISO 26000 §6.5 · VSME B3–B7 · ESRS E1–E5 · GRI 300 |
| `Social` | Social & droits humains | ISO 26000 §6.3–6.4 · VSME B8–B10 · ESRS S1–S4 · GRI 400 |
| `Ethics` | Éthique des affaires | ISO 26000 §6.6 · VSME B11 · ESRS G1 |
| `Procurement` | Achats responsables | ISO 26000 §6.6.6 · VSME C8 · EcoVadis |
| `Governance` | Gouvernance & pilotage | ISO 26000 §6.2 · VSME B1–B2, C1, C9 |

L'énumération s'appelle `RseDomain`, pas `Domain` : le projet .NET racine du Domain
s'appelle déjà `MAAT.Domain`, et un type `Domain` dans ce même arbre de namespaces
entre en collision avec lui (C# résout alors `Domain` vers le namespace, pas vers
le type — erreur de compilation).

Il n'existe **pas** de domaine « Économique ». Cet axe, hérité du triptyque
historique du développement durable et du GRI 200, est absent du VSME, des ESRS et
d'EcoVadis. Ses quelques éléments pertinents pour une PME — délais de paiement
fournisseurs, contribution économique locale — sont rattachés respectivement à
`Ethics` (les pratiques de paiement relèvent de l'ESRS G1) et à `Governance`.

---

## Company

Profil de l'entreprise cliente.

| Colonne | Type | Contraintes |
| --- | --- | --- |
| `id` | uuid | PK |
| `siret` | varchar(14) | unique, nullable, 14 chiffres |
| `name` | varchar(200) | requis |
| `sector_code` | varchar(6) | requis, code NAF (ex. `4941A`) |
| `size_range` | enum | requis |
| `region` | varchar(100) | requis |
| `created_at` | timestamptz | requis |

`size_range` ∈ { `Micro` (< 10), `Small` (10–49), `Medium` (50–249), `Large` (≥ 250) }.

Le SIRET est nullable : une entreprise peut démarrer un diagnostic avant de le
renseigner. Quand il est fourni, valider la longueur et la clé de Luhn.

`sector_code` sert à retrouver la pondération sectorielle (voir `SectorWeight`).
Une entreprise dont le code NAF n'a pas d'entrée dans `SectorWeight` utilise la
pondération par défaut — cas à gérer explicitement, pas à laisser planter.

Relations : `1 Company → N Users`, `1 Company → N Diagnostics`.

---

## User

Compte utilisateur authentifié.

| Colonne | Type | Contraintes |
| --- | --- | --- |
| `id` | uuid | PK |
| `email` | varchar(320) | unique, requis, stocké en minuscules |
| `password_hash` | varchar(100) | requis, bcrypt |
| `company_id` | uuid | FK → Company, requis |
| `role` | enum | requis, défaut `User` |
| `email_verified` | boolean | requis, défaut `false` |
| `email_verified_at` | timestamptz | nullable |
| `last_login` | timestamptz | nullable |
| `created_at` | timestamptz | requis |

`role` ∈ { `Admin`, `User`, `Viewer` }.

L'unicité de l'email est globale, pas par entreprise. L'index unique doit porter
sur la valeur normalisée en minuscules.

Ne jamais exposer `password_hash` dans un DTO, sous aucun prétexte.

`email_verified` distingue un compte dont l'adresse a été confirmée via
`EmailVerificationToken` d'un compte qui ne l'a pas encore été. Un compte non
vérifié peut se connecter mais ne peut pas générer de rapport PDF (section 1 de
`auth-securite-rgpd.md`) — la restriction porte sur la génération de rapport, pas
sur l'authentification. `email_verified_at` reste `null` tant que
`email_verified` est `false`.

---

## RefreshToken

Support des refresh tokens à 7 jours. Absent du rapport initial, mais requis par
la stratégie d'authentification à double token.

| Colonne | Type | Contraintes |
| --- | --- | --- |
| `id` | uuid | PK |
| `user_id` | uuid | FK → User, requis |
| `token_hash` | varchar(100) | requis, indexé |
| `expires_at` | timestamptz | requis |
| `revoked_at` | timestamptz | nullable |
| `created_at` | timestamptz | requis |

Stocker le **hash** du token, jamais sa valeur en clair : une fuite de la base ne
doit pas permettre d'usurper des sessions.

À la rotation, révoquer l'ancien token plutôt que le supprimer — la présence d'un
token révoqué réutilisé signale une compromission.

---

## EmailVerificationToken

Jeton de vérification d'adresse e-mail, émis à l'inscription (section 1 de
`auth-securite-rgpd.md`).

| Colonne | Type | Contraintes |
| --- | --- | --- |
| `id` | uuid | PK |
| `user_id` | uuid | FK → User, requis |
| `token_hash` | varchar(100) | requis, indexé |
| `expires_at` | timestamptz | requis |
| `consumed_at` | timestamptz | nullable |
| `created_at` | timestamptz | requis |

Stocker le **hash** du jeton, jamais sa valeur en clair — même raison que pour
`RefreshToken.token_hash` : une fuite de la base ne doit pas permettre de
vérifier une adresse à la place de son titulaire. Jeton à usage unique, valable
24 heures.

`consumed_at` est renseigné au moment où le jeton est utilisé pour vérifier
l'adresse ; il fait passer `User.email_verified` à `true` et fige
`User.email_verified_at`. Un jeton déjà consommé ou expiré est refusé,
symétriquement à la détection de réutilisation des `RefreshToken` — mais sans
révocation en cascade : un jeton de vérification n'ouvre pas de session, sa
réutilisation n'a pas la même gravité qu'un vol de refresh token.

---

## Diagnostic

Session d'évaluation RSE.

| Colonne | Type | Contraintes |
| --- | --- | --- |
| `id` | uuid | PK |
| `company_id` | uuid | FK → Company, requis |
| `status` | enum | requis, défaut `InProgress` |
| `global_score` | numeric(5,2) | nullable |
| `created_at` | timestamptz | requis |
| `completed_at` | timestamptz | nullable |

`status` ∈ { `InProgress`, `Completed`, `Archived` }.

`global_score` reste **null** tant que le statut est `InProgress`. Un score partiel
n'a aucun sens et ne doit jamais être affiché : il induirait l'utilisateur en erreur
sur sa maturité RSE.

`completed_at` est renseigné au moment exact du passage à `Completed`, et sert de
date de référence dans le rapport PDF.

Relations : `1 Diagnostic → N Responses`, `1 Diagnostic → 5 DomainScores`,
`1 Diagnostic → N Recommendations` (via table de jointure).

---

## DomainScore

Score par domaine pour un diagnostic donné.

| Colonne | Type | Contraintes |
| --- | --- | --- |
| `diagnostic_id` | uuid | PK composite, FK → Diagnostic |
| `domain` | enum | PK composite |
| `score` | numeric(5,2) | requis, 0 ≤ score ≤ 100 |
| `sector_weight` | numeric(4,3) | requis |

Cinq lignes par diagnostic complété, une par domaine.

`sector_weight` fige le coefficient sectoriel **appliqué au moment du calcul**.
Sans cela, une révision ultérieure de la table `SectorWeight` rendrait les
diagnostics historiques inexplicables : le score global stocké ne correspondrait
plus à ce qu'un recalcul produirait. C'est la condition de la traçabilité promise
par la valeur « Transparence » du produit.

Ces cinq lignes alimentent directement le radar chart du tableau de bord et du
rapport PDF.

---

## Question

Question du référentiel. Table de référence, alimentée par seed, non modifiable
par les utilisateurs.

| Colonne | Type | Contraintes |
| --- | --- | --- |
| `id` | uuid | PK |
| `code` | varchar(20) | unique, requis (ex. `ENV-04`) |
| `text` | text | requis |
| `help_text` | text | nullable |
| `domain` | enum | requis |
| `weight` | numeric(4,2) | requis, > 0 |
| `display_order` | int | requis |
| `vsme_ref` | varchar(50) | nullable (ex. `B7`) |
| `iso_ref` | varchar(50) | nullable (ex. `6.5.4`) |
| `gri_ref` | varchar(50) | nullable (ex. `305-1`) |
| `ecovadis_ref` | varchar(50) | nullable |
| `is_active` | boolean | requis, défaut true |

Répartition cible du MVP — 45 questions actives :

| Domaine | Questions | Préfixe de code |
| --- | --- | --- |
| Environnement | 11 | `ENV-` |
| Social & droits humains | 11 | `SOC-` |
| Éthique des affaires | 8 | `ETH-` |
| Achats responsables | 7 | `ACH-` |
| Gouvernance & pilotage | 8 | `GOU-` |

`ecovadis_ref` est ajouté au modèle initial. EcoVadis est le déclencheur d'achat
principal du produit : un utilisateur qui vient de recevoir un questionnaire
EcoVadis doit pouvoir relier chaque question MAAT au critère correspondant. C'est
un argument de vente autant qu'une aide à la complétion.

`code` est l'identifiant stable et lisible ; `id` peut changer entre
environnements, `code` non. C'est `code` qui doit apparaître dans les tests et les
règles de recommandation.

`help_text` porte l'explication en langage courant exigée par la valeur
« Pédagogie » du produit.

`is_active` permet de retirer une question sans casser les diagnostics passés :
**ne jamais supprimer une Question**, les `Response` historiques y font référence.

---

## Response

Réponse à une question pour un diagnostic donné.

| Colonne | Type | Contraintes |
| --- | --- | --- |
| `id` | uuid | PK |
| `diagnostic_id` | uuid | FK → Diagnostic, requis |
| `question_id` | uuid | FK → Question, requis |
| `value` | int | requis, 0 ≤ value ≤ 5 |
| `answered_at` | timestamptz | requis |

Index unique sur `(diagnostic_id, question_id)` : une seule réponse par question
et par diagnostic. La sauvegarde automatique du questionnaire fait un *upsert*,
pas un insert.

Le rapport initial prévoyait une colonne `score_contribution`. **Ne pas
l'implémenter.** C'est une valeur dérivée, recalculable à tout moment depuis
`value` et `Question.weight` ; la stocker crée un risque de désynchronisation si
la pondération évolue. Le calcul appartient au `ScoringService`.

---

## SectorWeight

Pondération sectorielle des domaines par code NAF. Table de référence.

| Colonne | Type | Contraintes |
| --- | --- | --- |
| `id` | uuid | PK |
| `sector_code` | varchar(6) | nullable, code NAF, indexé |
| `is_default` | boolean | requis, défaut `false` |
| `domain` | enum | requis |
| `weight` | numeric(4,3) | requis, 0 ≤ weight ≤ 1 |

`sector_code` est nullable : la pondération par défaut n'a pas de code NAF, elle
est identifiée par `is_default = true`. Les deux champs sont mutuellement
exclusifs — une ligne a soit un `sector_code`, soit `is_default = true`, jamais
les deux, jamais ni l'un ni l'autre.

Index unique sur `(sector_code, domain)` : cinq lignes par secteur réel.

Index unique filtré sur `domain` où `is_default = true` : garantit qu'il
n'existe qu'un seul jeu de pondération par défaut (une ligne par domaine au
plus parmi les lignes marquées par défaut).

**Invariant impératif** : pour un `sector_code` donné (y compris pour le jeu
`is_default = true`, regroupé sous une clé logique unique puisqu'il n'a pas de
`sector_code`), la somme des `weight` sur les 5 domaines vaut exactement 1. Une
somme différente de 1 produit un score global faux — c'est le bug le plus
dangereux du produit.

Pour tout `sector_code` non nul, cet invariant est appliqué en base par un
trigger différé en fin de transaction (`CONSTRAINT TRIGGER ... DEFERRABLE
INITIALLY DEFERRED` sur `sector_weights` — un `CHECK` ordinaire ne peut pas
porter sur un agrégat multi-lignes ; voir la migration
`AddSectorWeightCoverageConstraint`) : une insertion, mise à jour ou suppression
laissant un secteur avec une couverture partielle (1 à 4 domaines, ou une somme
différente de 1) fait échouer la transaction. Zéro ligne pour un `sector_code`
reste un état valide — secteur non configuré, repli sur la pondération par
défaut. Le jeu `is_default = true` n'est volontairement pas couvert par ce
trigger ; sa cohérence continue de reposer sur un test dédié parcourant
l'intégralité de la table de seed.

Prévoir un jeu de 5 lignes avec `is_default = true`, `sector_code = null` et
0,200 sur chaque domaine, utilisé quand le code NAF de l'entreprise n'est pas
couvert.

**Logique de repli** : pour trouver la pondération d'une entreprise, chercher
d'abord les lignes où `sector_code` correspond au code NAF de l'entreprise ; à
défaut (aucune ligne trouvée), prendre les lignes où `is_default = true`. Ce
repli ne doit jamais lever d'exception — voir la note sur `Company.sector_code`
plus haut.

Cible : 38 secteurs couverts. La construction de cette table est un travail de
documentation normative, pas de développement : elle s'appuie sur les guides
sectoriels ADEME et sur la matérialité sectorielle de l'ISO 26000. Chaque jeu de
coefficients doit être justifiable à l'oral.

---

## Recommendation

Action corrective proposée. Table de référence.

| Colonne | Type | Contraintes |
| --- | --- | --- |
| `id` | uuid | PK |
| `code` | varchar(20) | unique, requis |
| `domain` | enum | requis |
| `action_text` | text | requis |
| `detail_text` | text | nullable |
| `impact_points` | numeric(4,2) | requis |
| `effort_level` | enum | requis |
| `trigger_question_code` | varchar(20) | requis, → Question.code |
| `trigger_max_value` | int | requis, 0 ≤ v ≤ 5 |
| `is_active` | boolean | requis, défaut true |

`effort_level` ∈ { `Low`, `Medium`, `High` }.

Le rapport prévoyait un champ libre `condition_rule`. **Ne pas l'implémenter sous
forme de chaîne interprétée** : une règle stockée en texte et évaluée à l'exécution
est intestable et ouvre une surface d'injection. Le couple
`(trigger_question_code, trigger_max_value)` exprime la même chose de façon typée
et vérifiable : la recommandation se déclenche si la réponse à cette question est
inférieure ou égale à ce seuil.

Cible : 150+ recommandations, réparties sur les cinq domaines.

---

## DiagnosticRecommendation

Table de jointure — recommandations retenues pour un diagnostic, avec leur suivi.

| Colonne | Type | Contraintes |
| --- | --- | --- |
| `diagnostic_id` | uuid | PK composite, FK → Diagnostic |
| `recommendation_id` | uuid | PK composite, FK → Recommendation |
| `is_completed` | boolean | requis, défaut false |
| `completed_at` | timestamptz | nullable |
| `priority_rank` | int | requis |

`priority_rank` fige l'ordre calculé au moment du diagnostic. Sans lui, la liste
d'actions se réordonnerait à chaque affichage et l'utilisateur perdrait ses repères.

`is_completed` porte l'état des cases à cocher du tableau de bord.

---

## Report

Métadonnées de génération de rapport. **Aucun fichier n'est stocké.**

| Colonne | Type | Contraintes |
| --- | --- | --- |
| `id` | uuid | PK |
| `diagnostic_id` | uuid | FK → Diagnostic, requis |
| `format` | enum | requis, défaut `Pdf` |
| `generated_at` | timestamptz | requis |
| `generated_by_user_id` | uuid | FK → User, requis |

Le champ `file_url` du rapport initial est **supprimé**. Le PDF est régénéré à la
demande depuis les données du diagnostic : la génération est déterministe, donc
rien ne justifie de persister le fichier.

Trois conséquences à assumer et à savoir défendre : aucun stockage de fichiers à
sauvegarder ou sécuriser, aucun rapport périmé quand le diagnostic évolue, et
aucune durée de conservation de fichiers à justifier au registre des traitements.

Cette table sert uniquement de journal d'audit : qui a généré quoi, et quand.

---

## Points de vigilance transverses

**Suppressions.** Ne jamais supprimer physiquement une `Question`, une
`Recommendation` ou un `Diagnostic` : utiliser `is_active` ou le statut `Archived`.
L'historique des diagnostics est un argument produit — l'utilisateur suit
l'évolution de son score dans le temps.

**Droit à l'effacement RGPD.** Il constitue la seule exception : la suppression
d'un compte doit purger `User`, `RefreshToken`, `EmailVerificationToken`,
`Company`, `Diagnostic`, `Response`, `DomainScore`, `DiagnosticRecommendation`
et `Report` en cascade. `EmailVerificationToken` s'ajoute à `RefreshToken` pour
la même raison : c'est une donnée liée à un compte, pas une donnée de
référence. Les tables de référence (`Question`, `Recommendation`,
`SectorWeight`) ne contiennent aucune donnée personnelle et ne sont pas
concernées.

**Benchmark sectoriel.** Le calcul de position relative (« votre score dépasse
67 % des PME de votre secteur ») s'appuie sur une agrégation des `Diagnostic`
complétés par `sector_code`. Il doit rester anonyme : ne jamais exposer un score
individuel, et ne pas afficher de benchmark en dessous d'un seuil de 5 entreprises
dans le secteur, sous peine de rendre les scores ré-identifiables.

**Seed.** Deux jeux distincts : un seed de référence (questions, recommandations,
pondérations) appliqué dans tous les environnements, et un seed de démonstration
(entreprises et diagnostics fictifs) réservé au développement.
