# Pondérations sectorielles et relecture d'`effort_level`

---

## 1. `sector-weights.csv` — onze jeux, 55 lignes

Dix secteurs plus le jeu par défaut. Somme exactement égale à 1 sur chacun,
vérifiée en `Decimal` et non en flottant.

| Code NAF | Activité | Env | Soc | Éth | Ach | Gouv |
| --- | --- | --- | --- | --- | --- | --- |
| *(défaut)* | non couvert | 0,200 | 0,200 | 0,200 | 0,200 | 0,200 |
| `4941A` | Transport routier de fret | **0,400** | 0,200 | 0,150 | 0,150 | 0,100 |
| `7022Z` | Conseil pour les affaires | 0,100 | 0,300 | 0,250 | 0,150 | 0,200 |
| `4120A` | Construction de maisons individuelles | 0,300 | 0,300 | 0,100 | 0,200 | 0,100 |
| `4321A` | Travaux d'installation électrique | 0,250 | 0,300 | 0,100 | 0,250 | 0,100 |
| `1071C` | Boulangerie-pâtisserie | 0,300 | 0,250 | 0,150 | 0,200 | 0,100 |
| `5610A` | Restauration traditionnelle | 0,275 | 0,300 | 0,150 | 0,175 | 0,100 |
| `6201Z` | Programmation informatique | 0,100 | 0,300 | **0,300** | 0,100 | 0,200 |
| `2562B` | Mécanique industrielle | 0,350 | 0,250 | 0,100 | 0,200 | 0,100 |
| `8121Z` | Nettoyage courant des bâtiments | 0,150 | **0,400** | 0,150 | 0,150 | 0,150 |
| `4649Z` | Commerce de gros de biens domestiques | 0,175 | 0,175 | 0,175 | **0,375** | 0,100 |

**Les deux premiers ne sont pas choisis, ils sont imposés.** `4941A` et `7022Z`
portent les coefficients littéraux du cas 4 de `scoring.md`. Vérification faite
avec les scores de domaine du cas : `57.3333…` pour le transporteur, `53.8333…`
pour le conseil, `51.6667…` sur le jeu par défaut du cas 6 — soit `57.33`,
`53.83` et `51.67` après arrondi de persistance. Les trois valeurs attendues par
la spécification tombent au centime.

**Deux bornes que je me suis données, à défendre à l'oral.**

Aucun coefficient sous `0,100`. En dessous, le domaine cesse de compter dans le
score global alors qu'il continue d'occuper un cinquième du radar : l'utilisateur
verrait un axe bas sans effet sur sa note, ce qui est incompréhensible.

Aucun coefficient au-dessus de `0,400`. Au-delà, un seul domaine décide du score
et les quatre autres deviennent décoratifs. Deux secteurs atteignent la borne —
le transport sur l'environnement, le nettoyage sur le social — et c'est
volontaire : ce sont les deux cas où la matérialité est la plus concentrée.

**Le choix des huit autres secteurs suit le raisonnement du produit** : deux
métiers du bâtiment, deux de l'alimentaire et de la restauration, un de
l'industrie, un du numérique, un des services à forte main-d'œuvre, un du négoce.
Ce sont les profils qui reçoivent le plus de questionnaires fournisseurs, et ils
couvrent les quatre personas.

`4649Z` est le seul dont le profil est plat, à l'exception des achats à `0,375` :
un négociant n'a presque pas d'impact propre, tout son enjeu est dans ce qu'il
achète. C'est le secteur qui illustre le mieux pourquoi `Procurement` existe comme
domaine distinct.

---

## 2. `effort_level` — relecture de la colonne seule

Vous aviez raison : un seul `High` sur 45 était un signal, pas une propriété du
lot. J'ai relu la colonne sans les textes, contre une échelle que je n'avais pas
écrite avant de la remplir — ce qui était le vrai défaut.

| | Avant | Après |
| --- | --- | --- |
| `Low` | 25 | 24 |
| `Medium` | 19 | 16 |
| `High` | 1 | 5 |

**Cinq passages à `High`** : réduire ses déchets à la source et regrouper ses
trajets, qui demandent tous deux de changer une habitude d'achat ou une
organisation ; tenir un échange annuel avec chaque salarié et financer une
formation par an, qui engagent du temps récurrent et une dépense ; ajouter un
critère RSE pesant réellement dans une consultation fournisseurs, qui modifie un
processus et se négocie avec des tiers.

**Un passage à `Low`, et il était contradictoire.** REC-SOC-05 était notée
`Medium` alors que son propre texte dit « l'exercice prend vingt minutes ».
Écrire les critères avant de recevoir les candidats ne coûte rien — c'est
précisément l'argument.

**Deux passages à `Medium`.** Confier le suivi RSE à quelqu'un suppose d'allouer
un temps récurrent, ce qui n'est pas une action d'une journée. Annexer une charte
aux conditions d'achat suppose de rédiger puis d'intégrer à des documents
contractuels.

**Vingt-quatre `Low` restants, et je l'assume plutôt que de le corriger.** Ce lot
est le premier niveau : chaque recommandation vise une entreprise entre 0 et 3, et
son premier geste est délibérément léger. Si les seuils sont gradués plus tard,
les recommandations de niveau haut seront majoritairement `Medium` et `High`, et
la distribution s'équilibrera d'elle-même. C'est une propriété du niveau, pas un
biais de notation — mais il fallait le dire.

---

## 3. Ce que donne le haut du tableau de bord

Sous la pondération par défaut, les cinq premières recommandations affichées :

| Rang | Code | Domaine | Impact | Effort |
| --- | --- | --- | --- | --- |
| 1 | `REC-PRO-01` | Achats | 8,57 | `Low` |
| 2 | `REC-GOV-02` | Gouvernance | 7,50 | `Low` |
| 3 | `REC-PRO-05` | Achats | 5,71 | `Low` |
| 4 | `REC-ENV-06` | Environnement | 5,45 | `Low` |
| 5 | `REC-SOC-05` | Social | 5,45 | `Low` |

Quatre domaines représentés, et les cinq actions sont légères — ce qui est le bon
message pour une entreprise qui découvre son score.

**Une propriété qui ressemble à un biais et n'en est pas.** Les recommandations
d'`Achats` ont mécaniquement plus d'impact que celles d'`Environnement` à poids
égal : `8,57` contre `5,45` pour une question de poids 3. La raison est que
`Procurement` compte sept questions et `Environmental` onze, donc `Σ(w×5)` y vaut
70 contre 110.

Ce n'est pas une distorsion : chaque domaine étant ramené sur 100, un point gagné
vaut la même chose partout, et `impact_points × ρ_d` est donc le gain réel sur le
score global. Une action dans un petit domaine déplace effectivement plus la note,
parce que les 100 points de ce domaine se répartissent sur moins de questions.

À savoir expliquer : c'est exactement le genre d'anomalie apparente qu'un jury
relèvera dans le tableau de bord.

---

## 4. État du seed

| Fichier | Lignes | État |
| --- | --- | --- |
| `questions.csv` | 45 | vert |
| `recommendations.csv` | 45 | vert, cas 10 satisfait |
| `sector-weights.csv` | 55 | vert, invariant de somme vérifié |

Les trois contrôles bloquants de `seed --validate` passent. Le contrôle 4 ne
produit aucun avertissement.

Reste, par ordre d'urgence décroissante : l'arbitrage des préfixes de code avant
tout `seed` en production, les vingt-huit secteurs manquants pour atteindre la
cible de 38, la colonne `ecovadis_ref`, et la confrontation VSME en une passe.

Aucun de ces quatre points ne bloque une démonstration.
