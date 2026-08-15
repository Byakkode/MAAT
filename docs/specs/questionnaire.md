# Spécification — Module questionnaire

Périmètre : parcours des 45 questions, sauvegarde automatique, reprise d'un
diagnostic interrompu, et déclenchement du calcul de score à la complétion.

Contrainte produit dominante : **le diagnostic doit se terminer en moins de
30 minutes**. C'est la promesse centrale du produit et elle contraint chaque
décision de ce document — nombre d'écrans, coût d'une réponse, tolérance à
l'interruption.

Dépendances : `modele-donnees.md` (entités `Question`, `Response`, `Diagnostic`,
`DomainScore`), `scoring.md` (calcul déclenché à la complétion),
`auth-securite-rgpd.md` (cloisonnement par entreprise, section 4).

---

## 1. Cycle de vie d'un diagnostic

**Une entreprise ne peut avoir qu'un seul diagnostic `InProgress` à la fois.**
Sans cette règle, la reprise devient ambiguë et l'utilisateur ne sait plus lequel
il complète.

`POST /api/diagnostics` :

- s'il existe déjà un diagnostic `InProgress`, retourner **409** avec son
  identifiant, plutôt que d'en créer un second silencieusement ;
- le client propose alors deux options explicites : reprendre, ou abandonner
  l'existant et recommencer.

`POST /api/diagnostics/{id}/abandon` fait passer un diagnostic `InProgress` en
`Archived`. Un diagnostic abandonné n'est jamais supprimé : il conserve les
réponses déjà saisies et reste invisible dans l'historique de score, faute de
score calculé.

**Un diagnostic `Completed` est immuable.** Toute tentative de modification d'une
réponse retourne 409. Sans cette règle, l'historique d'évolution du score — qui
est un argument produit — ne signifie plus rien : on ne saurait pas si une
progression reflète des actions réelles ou une réécriture du passé. Pour
réévaluer, on crée un nouveau diagnostic.

---

## 2. Structure du parcours

Les 45 questions sont présentées **groupées par domaine**, soit cinq étapes de
7 à 11 questions. Pas une question par écran : 45 chargements successifs sont
incompatibles avec la promesse des 30 minutes et donnent une impression de
formulaire administratif.

`GET /api/diagnostics/{id}/questions` retourne l'intégralité des questions actives
avec les réponses déjà enregistrées. Un seul appel : les 45 questions représentent
quelques dizaines de kilo-octets, la pagination serait un coût sans bénéfice.

L'ordre suit `Question.display_order`, groupé par `Question.domain`. L'ordre des
domaines est fixe et identique pour tous les utilisateurs — sans quoi les
comparaisons de durée de complétion perdent leur sens.

Cet endpoint est scopé par entreprise selon le patron des autres dépôts de
diagnostic (`auth-securite-rgpd.md`, section 4) : un diagnostic d'une autre
entreprise retourne 404. C'est une lecture, accessible aux trois rôles, `Viewer`
inclus. Il fonctionne aussi sur un diagnostic `Completed`, en lecture seule —
c'est ce qui permet au frontend d'afficher un diagnostic terminé.

**Indicateur de progression** : nombre de questions répondues sur le total, plus
l'étape courante sur cinq. Afficher aussi une estimation du temps restant,
calculée sur la durée moyenne observée par question, et non sur une constante
codée en dur.

L'utilisateur peut revenir aux étapes précédentes et modifier ses réponses tant
que le diagnostic est `InProgress`.

---

## 3. Échelle de réponse

La valeur stockée est un entier de 0 à 5, mais **elle n'est jamais présentée comme
un nombre**. Une échelle numérique abstraite produit des réponses de complaisance
et contredit la valeur « Pédagogie » du produit.

| Valeur | Libellé affiché |
| --- | --- |
| 0 | Non, ce n'est pas en place |
| 1 | Nous y réfléchissons |
| 2 | Une démarche a été initiée |
| 3 | En cours de déploiement |
| 4 | Largement déployé |
| 5 | Pleinement en place et suivi |

Ces libellés sont des constantes du frontend, pas des colonnes en base : ils
relèvent de la présentation, et une future traduction ne doit pas exiger de
migration.

Chaque question affiche son `help_text` — accessible d'un clic, replié par défaut
pour ne pas alourdir l'écran.

**Pas d'option « non concerné » dans le MVP.** C'est une demande légitime, mais
elle oblige à exclure la question du dénominateur du domaine, donc à modifier le
moteur de scoring et ses tests, et elle ouvre une échappatoire : un utilisateur
peut déclarer non concerné tout ce qu'il ne fait pas et obtenir un score flatteur.
La pondération sectorielle traite déjà le problème qu'elle prétend résoudre. À
inscrire à la feuille de route, avec cette justification.

---

## 4. Sauvegarde automatique

`PUT /api/diagnostics/{id}/responses/{questionCode}`

Corps : la valeur. Opération **idempotente** — un *upsert* sur la contrainte
unique `(diagnostic_id, question_id)`, jamais un insert.

Déclenchement : à chaque sélection, avec un anti-rebond de 500 ms. Une réponse
est un clic unique, il n'y a pas de frappe à absorber ; l'anti-rebond ne sert
qu'à écraser les hésitations successives.

**Retour visuel obligatoire.** Trois états distincts : en cours d'enregistrement,
enregistré, échec. Un utilisateur qui perd vingt minutes de saisie sans avoir vu
d'avertissement ne revient pas — et il en parle. C'est le point où la promesse des
30 minutes se transforme en préjudice si elle est mal tenue.

**En cas d'échec réseau** : trois tentatives avec recul exponentiel, puis
conservation de la réponse en mémoire et bandeau persistant invitant à ne pas
fermer l'onglet. Ne pas utiliser `localStorage` : le questionnaire contient des
données d'entreprise, et la règle de la section sécurité s'applique.

Le passage à l'étape suivante est bloqué tant qu'une sauvegarde est en échec.

---

## 5. Reprise

`GET /api/diagnostics/current` retourne le diagnostic `InProgress` de l'entreprise,
ou 404.

À la connexion, si un diagnostic est en cours, proposer la reprise directement
depuis le tableau de bord, en indiquant l'avancement et la date de dernière
activité. L'utilisateur reprend à la première étape comportant une question sans
réponse, pas au début.

---

## 6. Complétion

`POST /api/diagnostics/{id}/complete`

Préconditions : toutes les questions actives ont une réponse, et le statut est
`InProgress`. À défaut, 400 avec la liste des codes de questions manquantes.

Traitement, dans **une seule transaction** :

1. appel de `ScoringService` avec les réponses et la pondération sectorielle
   correspondant au `sector_code` de l'entreprise, ou la pondération par défaut ;
2. écriture des cinq lignes `DomainScore`, avec le `sector_weight` effectivement
   appliqué ;
3. écriture de `Diagnostic.global_score` et de
   `Diagnostic.default_sector_weighting_applied` (vrai si l'étape 1 est retombée
   sur la pondération par défaut, décidé à la source, pas redéduit plus tard) ;
4. passage du statut à `Completed` et horodatage de `completed_at` ;
5. génération des recommandations (module suivant, hors périmètre de cette spec).

**Idempotence** : un second appel sur un diagnostic déjà `Completed` retourne 409
sans recalculer. Un double-clic ne doit pas produire dix lignes `DomainScore`.

Si le calcul lève une exception — pondération manquante, données incohérentes — la
transaction est annulée intégralement. Un diagnostic à moitié complété avec des
scores partiels est pire qu'un échec net.

---

## 7. Frontend

État global du questionnaire dans Zustand. Les composants de question s'abonnent
**uniquement à leur propre réponse**, jamais à l'objet d'état complet : sans cela,
chaque clic déclenche le rendu des 45 composants. `React.memo` sur le composant de
question, avec des gestionnaires stabilisés par `useCallback`.

C'est le problème de performance identifié dans votre rapport, et la mémoïsation
seule ne suffit pas si les sélecteurs sont trop larges.

**Accessibilité** — exigence WCAG AA de la charte :

- chaque groupe de réponses est un `radiogroup` correctement étiqueté ;
- navigation complète au clavier, focus visible ;
- l'indicateur de progression est annoncé via une région `aria-live` ;
- les états de sauvegarde sont annoncés, pas seulement colorés ;
- ne jamais coder une information par la seule couleur.

Les couleurs, typographies et espacements proviennent du skill `charte-maat`.

---

## 8. Rédaction des questions — vigilance RGPD

Point qui relève de la rédaction du contenu, pas du code, mais qui doit être
tranché avant le seed des 45 questions.

Les questions du domaine Social peuvent facilement dériver vers des **catégories
particulières au sens de l'article 9** — santé, appartenance syndicale, opinions.
Elles doivent porter sur l'existence de dispositifs organisationnels, jamais sur
des situations individuelles.

- Acceptable : « Votre entreprise dispose-t-elle d'un accord ou d'une charte sur
  le télétravail ? »
- À proscrire : toute formulation appelant un dénombrement de salariés par état de
  santé, origine ou appartenance.

En cas de doute sur une formulation, la reformuler plutôt que l'arbitrer.

---

## Cas de test

**Cycle de vie**

1. Création d'un diagnostic sans autre en cours → 201.
2. Création alors qu'un `InProgress` existe → 409 portant l'identifiant existant.
3. Abandon → statut `Archived`, réponses conservées.
4. Modification d'une réponse sur un diagnostic `Completed` → 409.

**Réponses**

5. Première réponse à une question → 201, une ligne créée.
6. Seconde réponse à la même question → 200, valeur mise à jour, **toujours une seule ligne**.
7. Valeur hors de l'intervalle 0–5 → 400.
8. Réponse à une question inactive → 400.
9. Réponse sur un diagnostic d'une autre entreprise → 404 (section 4 de la spec sécurité).

**Complétion**

10. Complétion avec des réponses manquantes → 400 listant les codes concernés.
11. Complétion valide → 5 lignes `DomainScore`, `global_score` renseigné, statut `Completed`, `completed_at` horodaté.
12. Le `sector_weight` persisté correspond à la pondération du secteur de l'entreprise.
13. Entreprise dont le code NAF n'est pas couvert → pondération par défaut appliquée, et le
    repli persisté sur `Diagnostic.default_sector_weighting_applied` (`modele-donnees.md`) —
    jamais seulement déductible de `DomainScore.sector_weight`, que la renormalisation du cas 7
    de `scoring.md` peut ramener à 1.00 aussi bien dans le cas par défaut que dans le cas
    spécifique dès qu'un seul domaine est actif.
14. Second appel à `complete` → 409, aucune ligne supplémentaire.
15. Échec du calcul → transaction annulée, statut resté `InProgress`, aucune ligne `DomainScore`.

**Reprise**

16. `GET /current` avec un diagnostic en cours → le retourne avec l'avancement.
17. `GET /current` sans diagnostic en cours → 404.

**Rôles** (`auth-securite-rgpd.md`, section 4 : le rôle `Viewer` n'a que la lecture)

18. `Viewer` tentant d'abandonner un diagnostic → 403.
19. `Viewer` tentant d'enregistrer une réponse → 403.
20. `Viewer` tentant de compléter un diagnostic → 403.

**Consultation des questions** (`GET /api/diagnostics/{id}/questions`)

21. Questions actives uniquement, triées par `display_order` à l'intérieur de
    chaque domaine, domaines dans leur ordre fixe ; une question déjà répondue
    porte sa valeur, les autres une valeur nulle.
22. Consultation d'un diagnostic d'une autre entreprise → 404.
23. `Viewer` consultant les questions → 200 : c'est une lecture, elle lui est
    ouverte contrairement aux cas 18-20.
24. Consultation sur un diagnostic `Completed` → 200, en lecture seule.

Le cas 15 est le plus important de la liste : c'est celui qui garantit qu'aucun
diagnostic ne peut exister dans un état intermédiaire incohérent.

Ce cas teste la transaction de la complétion, pas le moteur de scoring : il doit
prouver qu'un échec du calcul — quelle qu'en soit la cause — n'écrit jamais d'état
partiel, sans reproduire lui-même une cause précise d'échec. Une pondération
sectorielle incomplète, envisagée un temps comme déclencheur, est désormais
rejetée en base par une contrainte différée sur `sector_weights` (voir
`modele-donnees.md`) et ne peut donc plus servir à construire ce scénario par de
simples insertions. Le déclencheur est une implémentation de `IScoringService`
substituée en test, configurée pour lever — ce qui isole proprement la garantie
transactionnelle vérifiée ici de la question, distincte, de savoir ce qui peut
concrètement faire échouer un calcul de score.
