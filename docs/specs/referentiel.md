# Spécification — Référentiel des 45 questions

Périmètre : structure du questionnaire, répartition des questions par domaine,
échelle des poids, ancrage normatif, règles de rédaction, format du CSV et
contrôles de cohérence.

Ce document cadre **la forme** du référentiel. Le texte des 45 questions relève
de la rédaction de contenu et ne se délègue pas : c'est lui qui sera lu ligne à
ligne en soutenance, et c'est la seule partie du produit qu'un jury peut évaluer
sans lire de code.

Dépendances : `modele-donnees.md` (entité `Question`, format des CSV de
référence), `scoring.md` (rôle de `weight` dans le calcul), `questionnaire.md`
(échelle de réponse, section 8 sur la vigilance RGPD), `recommandations.md`
(chaque question doit porter au moins une recommandation).

---

## 1. Avertissement préalable — droit d'auteur

L'ISO 26000 est une norme protégée, vendue par l'ISO et ses relais nationaux.
Le fait d'en détenir un exemplaire n'autorise pas à en republier le texte.

**Les questions doivent être rédigées de zéro, dans vos propres mots.** Ni
transcription, ni traduction, ni reformulation serrée d'un paragraphe de la
norme. La référence normative se porte par un *renvoi* — un identifiant de
domaine d'action — jamais par une citation.

Ce n'est pas une précaution théorique : le questionnaire est le contenu d'un
produit commercial, et la reproduction de texte normatif dans un SaaS payant est
une exposition réelle. La même règle vaut pour le GRI et pour le VSME, dont les
conditions de réutilisation sont plus ouvertes mais non illimitées.

Ce que vous pouvez utiliser sans réserve : la **structure** des référentiels —
les sept domaines d'action de l'ISO 26000, les intitulés des piliers — qui sont
des faits publics largement documentés, y compris par l'ISO elle-même.

---

## 2. Des sept domaines d'action aux cinq domaines MAAT

L'ISO 26000 organise la responsabilité sociétale en sept domaines d'action.
MAAT en retient cinq. Cette correspondance doit être explicite : c'est la
première question que posera un jury qui connaît la norme.

| Domaine MAAT | Domaines d'action ISO 26000 couverts |
| --- | --- |
| `Governance` | Gouvernance de l'organisation |
| `Social` | Droits de l'Homme · Relations et conditions de travail |
| `Environmental` | Environnement |
| `Ethics` | Loyauté des pratiques · Questions relatives aux consommateurs |
| `Procurement` | Communautés et développement local · volet achats de la loyauté des pratiques |

Le regroupement n'est pas arbitraire et doit se défendre ainsi : les sept
domaines d'action de la norme s'adressent à toute organisation, y compris
publique et associative, tandis que MAAT s'adresse à une PME dont
l'interlocuteur est un donneur d'ordres. Le découpage retenu épouse la
structure des questionnaires qu'elle reçoit — Environnement, Social,
Gouvernance, Éthique, Achats — plutôt que celle de la norme.

**Le domaine `Procurement` est le plus discutable et le plus différenciant.**
Les achats responsables ne sont pas un domaine d'action de l'ISO 26000 ; ils
traversent plusieurs d'entre eux. Les isoler se justifie par le déclencheur
d'achat du produit : une PME qui reçoit un questionnaire RSE le reçoit *parce
qu'elle est fournisseur*, et sa propre chaîne d'approvisionnement est le premier
sujet sur lequel son client l'interroge. À assumer explicitement, y compris
comme un écart revendiqué vis-à-vis de la norme.

**Règles de frontière entre domaines.** Le tableau de correspondance ci-dessus
situe MAAT par rapport à l'ISO 26000 ; les règles suivantes situent les
domaines MAAT les uns par rapport aux autres, pour trancher à quel domaine
rattacher une question qui pourrait a priori relever de deux.

- **`Environmental` / `Procurement`.** `Environmental` couvre les interactions
  physiques entre l'entreprise et son milieu, dans les deux sens — ce qu'elle
  consomme, émet, rejette, et ce qu'elle subit. `Procurement` couvre les
  décisions portant sur un tiers : choisir, évaluer, contractualiser.
- **`Governance` / domaines opérationnels.** `Governance` couvre qui décide et
  selon quel processus. La transmission des consignes opérationnelles reste
  dans le domaine dont elles relèvent.
- **`Governance` / `Social`.** `Governance` couvre la composition de l'organe
  qui décide. `Social` couvre le traitement de l'ensemble des salariés. Une
  question sur l'égalité de traitement, la rémunération ou le recrutement
  relève de `Social`, y compris lorsqu'elle porte sur la mixité.
- **`Ethics` / `Procurement`.** `Ethics` couvre la conduite de l'entreprise
  vis-à-vis de son marché et de ses clients. Le domaine d'action ISO 6.6.6 —
  promotion de la responsabilité sociétale dans la chaîne de valeur — relève de
  `Procurement`.
- **`Procurement` / `Social`.** `Social` couvre les personnes que l'entreprise
  emploie. `Procurement` couvre celles qu'emploient ses fournisseurs et
  prestataires.
- **`Ethics` / `Social` sur les signalements.** `Ethics` couvre le signalement
  d'un manquement à la conduite des affaires — fraude, conflit d'intérêts.
  `Social` couvre le recours ouvert à un salarié qui subit une atteinte
  personnelle. Deux dispositifs distincts, deux questions distinctes.

Consigner la correspondance ISO 26000 et les six règles de frontière ci-dessus
dans une ADR sous `docs/adr/`.

---

## 3. Répartition des 45 questions

| Domaine | Questions | Justification |
| --- | --- | --- |
| `Environmental` | 11 | Sujet le plus attendu par les donneurs d'ordres, et le plus dense en dispositifs concrets |
| `Social` | 11 | Couvre deux domaines d'action de la norme |
| `Governance` | 8 | Socle de la démarche, mais moins d'items mesurables dans une PME |
| `Ethics` | 8 | |
| `Procurement` | 7 | Le plus étroit, mais le plus différenciant |

Total : 45.

**Aucun domaine sous 7 questions.** En dessous, une seule réponse déplace le
score du domaine de plus de dix points, et le radar devient instable d'un
diagnostic à l'autre pour des raisons qui n'ont rien à voir avec la maturité de
l'entreprise.

La répartition n'est pas un équilibre esthétique : elle est neutre pour le score
global, puisque chaque domaine est ramené sur 100 avant pondération sectorielle
(`scoring.md`). Elle n'influence que la finesse de mesure à l'intérieur d'un
domaine.

**Contrainte de durée.** 45 questions en moins de 30 minutes laisse environ
30 secondes par question, lecture du texte d'aide comprise. Toute question qui
demande à l'utilisateur d'aller chercher un chiffre dans un autre logiciel
casse la promesse centrale du produit. Le test de rédaction est simple : *un
dirigeant peut-il répondre de mémoire ?* Si non, la question est mal posée pour
ce produit — même si elle est excellente sur le fond.

---

## 4. Échelle des poids

`weight` est un décimal strictement positif (`numeric(4,2)`). Trois valeurs
seulement, et pas davantage :

| Poids | Signification |
| --- | --- |
| `1.00` | Pratique utile, mais secondaire ou émergente |
| `2.00` | Pratique attendue d'une PME engagée |
| `3.00` | Attendu structurant, souvent exigé par un donneur d'ordres |

Une échelle à trois crans se défend ; une échelle continue ne se défend pas. Si
vous introduisez `2.50`, il faudra expliquer au jury pourquoi telle question
vaut 2,50 et pas 2,25 — et vous ne le pourrez pas. Trois niveaux nommés sont
justifiables un par un.

Le type de la colonne reste décimal, ce qui laisse la porte ouverte à un
affinage futur sans migration. Ne pas confondre cette souplesse de schéma avec
une autorisation de contenu.

**Répartition indicative par domaine** : environ un quart de questions à `3.00`,
la moitié à `2.00`, un quart à `1.00`. Un domaine dont toutes les questions
pèsent 3 n'exprime plus aucune priorité.

---

## 5. Ancrage normatif

Chaque question porte trois renvois : `vsme_ref`, `iso_ref`, `gri_ref`. Ils sont
la matérialisation de la valeur « Transparence » et apparaissent dans le rapport.

Règles :

- un renvoi est un **identifiant**, jamais une phrase extraite d'un référentiel ;
- `iso_ref` désigne le domaine d'action ou la question centrale correspondante ;
- un renvoi peut être vide si la question n'a pas de correspondance honnête —
  mieux vaut une case vide qu'un rattachement inventé, qui sera repéré par un
  jury qui connaît le référentiel ;
- **aucune question ne doit avoir ses trois renvois vides.** Une question sans
  ancrage normatif ne relève pas d'un diagnostic RSE mais d'une opinion.

Ce dernier point mérite un contrôle automatique sur le seed.

**Granularité de `gri_ref`.** Le renvoi désigne une série de normes
thématiques, sans le préfixe `GRI` — la colonne le porte déjà. Les éléments
d'information de niveau inférieur sont des indicateurs chiffrés, alors que les
questions portent sur l'existence de dispositifs : un renvoi à cette
granularité serait un faux rattachement. Les normes universelles `2` et `3`
sont admises lorsque le sujet relève de la gouvernance ou de la hiérarchisation
des enjeux.

**Spécificité de `vsme_ref`.** Le renvoi désigne l'élément thématique le plus
spécifique qui couvre le sujet. `B2` — pratiques, politiques et initiatives de
transition vers une économie plus durable — n'est utilisé qu'à défaut d'élément
thématique, **et seulement pour les sujets qui relèvent de cette transition**.
Il ne couvre ni la conduite des affaires ni la relation client, qui n'ont pas
d'élément correspondant dans le module de base : les questions du domaine
`Ethics` ont donc un `vsme_ref` vide, sauf `B11` pour la corruption.

**Modules `B` et `C`.** `vsme_ref` peut renvoyer au module de base (`B`) ou au
module complet (`C`). Le préfixe est autodocumenté ; aucune distinction n'est
portée dans le rapport à ce stade. Un renvoi `C` signale un sujet situé au-delà
du socle qu'un donneur d'ordres peut exiger au titre du plafond de chaîne de
valeur — cette lecture est notée en feuille de route, pas implémentée.

---

## 6. Règles de rédaction

**Une question porte sur l'existence d'un dispositif, jamais sur une
performance chiffrée.** « Avez-vous formalisé un plan de réduction de vos
consommations d'énergie ? » et non « De combien avez-vous réduit vos
consommations ? ». La seconde forme est incompatible avec l'échelle de réponse
en six niveaux, et incompatible avec les 30 minutes.

**Une question, un sujet.** Une question portant sur deux dispositifs ne peut
pas recevoir de réponse graduée sensée, et rend le score inexplicable.

**Formulation neutre.** Une question ne doit pas suggérer sa bonne réponse. Les
réponses de complaisance sont le principal risque de crédibilité d'une
auto-évaluation ; la formulation est le seul levier dont vous disposez contre
elles.

**Vocabulaire de PME.** L'utilisateur type découvre la RSE sous contrainte
réglementaire. « Analyse de matérialité », « parties prenantes », « due
diligence » sont à éviter dans l'intitulé — et à expliquer dans le `help_text`
s'ils sont inévitables.

**Le `help_text` est obligatoire et compte 60 à 90 mots.** Il comprend ce que la
question recouvre, au moins un exemple concret, et **trois repères sur
l'échelle de réponse**, au format `0 — … 3 — … 5 — …`. C'est ce qui rend le
questionnaire pédagogique plutôt qu'administratif.

Les niveaux 1, 2 et 4 ne sont pas décrits : les libellés fixes de l'échelle
s'en chargent. Le `help_text` dit ce que la question recouvre aux trois états
structurels — rien, engagé mais incomplet, en place et suivi.

**Le repère 0 décrit une absence, jamais un état partiel.** Un repère 0 qui
présuppose l'existence de l'objet laisse sans place le répondant qui n'a rien,
et le pousse vers 1 ou 2 par défaut.

Le repère 3 est rédigé comme **une pratique réelle mais incomplète**, jamais
comme une demi-mesure floue : action ponctuelle sur un seul poste,
justificatifs existants mais dispersés, consignes transmises à l'embauche puis
jamais rappelées. C'est ce qui le rend inconfortable à cocher par réflexe.

**Vigilance RGPD, article 9.** Le domaine `Social` dérive facilement vers des
catégories particulières — santé, appartenance syndicale, opinions, origine.
Les questions portent sur l'existence de dispositifs organisationnels, jamais
sur des situations individuelles ni sur un dénombrement de salariés par état.
En cas de doute sur une formulation, la reformuler plutôt que l'arbitrer
(`questionnaire.md`, section 8).

**Neutralité sectorielle.** L'intitulé ne contient aucun terme propre à un
métier ou à un secteur. Le produit ne dispose d'aucun mécanisme de
non-applicabilité : une question à laquelle un secteur entier ne peut répondre
que par zéro produit un score faux, et silencieusement. Le concret appartient
au `help_text`, où les exemples couvrent au moins deux univers d'activité
différents. Test : *une boulangerie de 20 salariés peut-elle répondre autrement
que par zéro ?*

**Variation des formulations.** Deux questions consécutives d'un même domaine
ne reprennent pas la même tournure d'introduction. Une série de questions quasi
identiques produit un effet d'acquiescement : le répondant cesse de lire et
répond en série. **La variation porte sur la tournure, jamais sur le périmètre
de la question.** Retirer un qualificatif pour éviter une répétition élargit
l'objet et détruit le pouvoir discriminant de la question.

**Autonomie de l'intitulé.** Chaque intitulé se comprend seul, hors de sa
séquence. Aucun renvoi implicite à la question précédente — « ces sujets », « ce
domaine », « cette démarche » — n'est admis si l'antécédent n'est pas dans
l'intitulé lui-même. La contrainte n'est pas cosmétique : l'intitulé est repris
dans le plan d'actions, dans le rapport PDF et dans le libellé des
recommandations qu'il déclenche, où il apparaît sans ses voisines. C'est aussi
ce qui empêche la règle de variation de produire des renvois implicites.

---

## 7. Exemples de calibrage

Ces exemples fixent le registre attendu. Ils ne sont pas à recopier tels quels.

**Bien.** *Gouvernance, poids 3.00* — « Une personne de votre entreprise est-elle
identifiée comme responsable du suivi des sujets RSE ? »
`help_text` : il ne s'agit pas de créer un poste dédié. La question porte sur
le fait qu'un salarié ou le dirigeant lui-même ait cette mission explicitement
reconnue, avec du temps pour l'exercer. Réponse basse : personne n'est
identifié. Réponse haute : la mission est formalisée et un temps y est consacré.

**Bien.** *Achats responsables, poids 2.00* — « Interrogez-vous vos fournisseurs
sur leurs pratiques environnementales ou sociales avant de les référencer ? »

**Mal.** « Avez-vous mis en place une politique RSE ? » — trop vague, réponse de
complaisance garantie, et aucune recommandation actionnable ne peut en découler.

**Mal.** « Quel est votre taux d'absentéisme ? » — demande un chiffre, incompatible
avec l'échelle, et hors des 30 minutes.

**Interdit.** « Combien de vos salariés sont en situation de handicap ? » —
donnée de santé au sens de l'article 9.

---

## 8. Format du CSV

`backend/MAAT.Infrastructure/Seed/questions.csv`, conformément à
`modele-donnees.md`.

- `code` : `ENV-01`, `SOC-01`, `GOV-01`, `ETH-01`, `PRO-01`. Préfixe par domaine,
  numérotation à deux chiffres. La règle de stabilité prend effet au **premier
  `seed` exécuté en production**. Avant ce point, les codes peuvent être
  réattribués librement au sein d'un domaine. Après, un `code` ne désigne
  jamais un autre sujet : une question retirée est désactivée, une question
  nouvelle prend le numéro suivant, **même si cela laisse un trou dans la
  numérotation**. La spec imposait jusqu'ici une numérotation sans trou *et* la
  stabilité des codes ; les deux règles se contredisent dès la première
  désactivation, et l'arbitrage va aux trous, parce que la stabilité protège
  les diagnostics passés et que la contiguïté ne protège rien.
- `display_order` : ordre à l'intérieur du domaine, contigu à partir de 1.
- `weight` : `1.00`, `2.00` ou `3.00`, point décimal.
- `is_active` : une question retirée est désactivée, jamais supprimée — les
  diagnostics passés y font référence.

Lancer `seed --validate` avant toute proposition de modification.

---

## 9. Ordre de travail

1. **Les pondérations sectorielles d'abord**, pour une dizaine de secteurs
   représentatifs plutôt que 38 approximatifs. Sans elles, l'argument produit
   central ne se démontre pas, et le cas 4 de `scoring.md` reste théorique.
2. **Un domaine complet ensuite**, `Environmental` de préférence : le plus
   attendu, le plus facile à rédiger, et il sert d'étalon de registre pour les
   quatre autres. Le faire relire par les trois membres de l'équipe avant de
   continuer.
3. **Les quatre domaines restants.**
4. **Les recommandations**, au moins une par question. Le test de couverture du
   seed le vérifie déjà.

Rédiger `Social` en dernier : c'est celui qui demande le plus de vigilance
RGPD, et il bénéficiera du registre calé sur les autres.

---

## Cas de test

À ajouter aux contrôles de seed existants.

1. Exactement 45 questions actives.
2. Répartition par domaine conforme à la section 3.
3. Les cinq domaines sont représentés, aucun sous 7 questions.
4. Tous les `weight` valent `1.00`, `2.00` ou `3.00`.
5. Aucun domaine dont toutes les questions portent le même poids.
6. `display_order` contigu à partir de 1 à l'intérieur de chaque domaine.
7. Les `code` sont uniques et suivent le préfixe de leur domaine.
8. Chaque question a un `help_text` non vide.
9. Aucune question n'a ses trois renvois normatifs vides à la fois.
10. Chaque question active est référencée par au moins une recommandation active
    (test déjà existant, `recommandations.md` cas 22).
11. Chaque `help_text` contient exactement trois repères, aux valeurs 0, 3 et
    5, au format `N — `.
12. Tous les CSV de seed utilisent des fins de ligne `LF`. Un `.gitattributes`
    fixant `*.csv text eol=lf` accompagne le contrôle.

Le cas 9 est celui qui distingue un diagnostic RSE d'un questionnaire d'opinion.
Le cas 5 est celui qui garantit que les poids expriment réellement une priorité.

Le contrôle de forme du cas 11 est automatisable et entre dans `seed
--validate` ; la clause « le repère 0 décrit une absence » (section 6) ne l'est
pas — une heuristique sur les tournures d'absence produit des faux positifs, et
reste une règle de rédaction vérifiée en relecture. Le cas 12 n'a rien à voir
avec la compatibilité de lecture (`CsvFile` accepte déjà CRLF, LF et CR, y
compris mélangés) : sa raison est la lisibilité des diffs — un changement de
fin de ligne fait apparaître les 45 lignes comme modifiées et rend illisible la
revue du seul contenu que l'équipe relit régulièrement.
