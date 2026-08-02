# Spécification — Tableau de bord

Périmètre : écran principal après connexion. Score global, radar des cinq
domaines, historique d'évolution, benchmark sectoriel anonyme, et accès au plan
d'actions.

C'est l'écran que l'utilisateur revoit chaque mois, et celui qui sera projeté en
soutenance. Il doit répondre à trois questions en un coup d'œil : où j'en suis,
sur quoi je suis faible, et que dois-je faire ensuite.

Dépendances : `scoring.md` (scores persistés dans `DomainScore`),
`recommandations.md` (plan d'actions priorisé), `questionnaire.md` (reprise d'un
diagnostic en cours), skill `charte-maat` (couleurs par domaine, typographie).

---

## 1. Endpoint

`GET /api/dashboard`

Un seul appel retourne l'ensemble des données de l'écran. Quatre appels séparés
produiraient quatre états de chargement désordonnés pour une page qui doit
s'afficher d'un bloc.

Contenu de la réponse :

- le diagnostic le plus récent : identifiant, score global, date de complétion ;
- les cinq scores de domaine avec leur pondération sectorielle appliquée ;
- l'historique : score global et date de chaque diagnostic complété, du plus
  ancien au plus récent ;
- le benchmark sectoriel, ou son absence motivée ;
- les cinq premières recommandations par `priority_rank`, avec leur état
  d'avancement, et le nombre total ;
- le diagnostic en cours s'il existe, avec son avancement.

Scopé par entreprise selon le patron établi. Accessible aux trois rôles.

**Trois états distincts** à traiter explicitement, et non comme des cas d'erreur :

| État | Réponse |
| --- | --- |
| Aucun diagnostic | 200 avec un contenu vide et un indicateur `hasCompletedDiagnostic: false` |
| Diagnostic en cours uniquement | 200 avec l'avancement, sans score |
| Au moins un diagnostic complété | contenu complet |

Le premier état est celui de tout nouvel utilisateur. C'est le premier écran qu'il
voit après inscription, il mérite autant de soin que les autres.

---

## 2. Score global

Affiché en entier, arrondi selon `RoundForDisplay`, accompagné d'un libellé
qualitatif qui rend le nombre interprétable :

| Score | Libellé |
| --- | --- |
| 0–24 | Démarche à initier |
| 25–49 | Premiers pas engagés |
| 50–69 | Démarche structurée |
| 70–84 | Démarche avancée |
| 85–100 | Démarche exemplaire |

Ces seuils sont des constantes du frontend. Ils doivent apparaître à l'identique
dans le rapport PDF — un même score ne peut pas être qualifié différemment selon
le support.

**Ne jamais présenter le score comme une note ou un classement.** Le vocabulaire
décrit une trajectoire, pas un jugement : l'utilisateur type découvre la RSE sous
contrainte réglementaire, un score de 30 affiché comme un échec le fait fuir.

Le score est accompagné de la mention du secteur d'activité utilisé pour la
pondération — c'est ce qui rend le chiffre explicable et ce qui distingue MAAT
d'un questionnaire générique.

---

## 3. Radar des cinq domaines

Recharts `RadarChart`, cinq axes, échelle fixe de 0 à 100.

**L'échelle ne s'adapte jamais aux données.** Un radar dont l'axe s'arrête à la
valeur maximale observée donne visuellement le même dessin à une entreprise
médiocre et à une exemplaire. L'échelle absolue est ce qui rend le graphique
honnête.

Couleurs par domaine, telles que définies dans le skill `charte-maat` :
Environnement `#29CC6A` · Social `#1E88E5` · Éthique `#7E57C2` · Achats
responsables `#FFB74D` · Gouvernance `#42A5F5`.

Chaque axe indique, au survol ou au clic, le score du domaine, sa pondération
sectorielle appliquée, et le nombre de recommandations qu'il a déclenchées.

**Accessibilité — exigence WCAG AA.** Un radar SVG est illisible pour un lecteur
d'écran. Fournir systématiquement, sous le graphique, un tableau des cinq scores,
visuellement discret mais présent dans le DOM et accessible au clavier. C'est
aussi ce qui permet de copier les valeurs, ce que les utilisateurs feront pour
leurs propres présentations.

Le graphique porte un `<title>` et une description textuelle.

---

## 4. Historique d'évolution

Courbe du score global par date de complétion, affichée uniquement à partir de
**deux** diagnostics. Avec un seul point, afficher plutôt une invitation à
renouveler le diagnostic dans quelques mois.

Échelle de 0 à 100, comme le radar, et pour la même raison.

Les diagnostics `Archived` n'apparaissent pas : ils n'ont pas de score.

Indiquer l'écart avec le diagnostic précédent — « +7 points depuis février ».
C'est la donnée qui donne sa valeur au produit dans la durée, et l'argument
central du renouvellement d'abonnement.

---

## 5. Benchmark sectoriel

Position relative de l'entreprise parmi celles du même `sector_code` ayant un
diagnostic complété.

**Seuil d'anonymat : 5 entreprises minimum.** En deçà, ne rien afficher et
indiquer que la comparaison sera disponible quand davantage d'entreprises du
secteur auront réalisé un diagnostic. C'est une exigence RGPD — sous ce seuil,
les scores redeviennent ré-identifiables par recoupement — et c'est le cas 21
retiré de `auth-securite-rgpd.md`, à vérifier ici.

L'agrégation ne retient que le **diagnostic le plus récent de chaque entreprise**.
Sans cette règle, une entreprise ayant réalisé dix diagnostics pèse dix fois plus
dans la moyenne du secteur.

Ne jamais exposer un score individuel, ni un identifiant, ni un nom d'entreprise.
La réponse ne contient qu'un percentile et un effectif.

Formulation : « votre score vous situe au-dessus de 67 % des entreprises de votre
secteur ayant réalisé un diagnostic ». Préciser que l'échantillon est celui des
utilisateurs de MAAT, pas du secteur entier — une approximation présentée comme
une statistique nationale serait indéfendable devant un jury.

---

## 6. Plan d'actions

Les cinq premières recommandations par `priority_rank`, avec libellé, domaine,
niveau d'effort et case d'avancement. Lien vers la liste complète.

Les cases sont actionnables directement depuis le tableau de bord pour `Admin` et
`User` ; en lecture seule pour `Viewer`.

Cocher une action ne modifie pas le score. Le rappeler discrètement dans
l'interface : « pris en compte lors de votre prochain diagnostic ».

Afficher le taux d'avancement global du plan — « 3 actions terminées sur 24 ».

---

## 7. Diagnostic en cours

S'il existe un diagnostic `InProgress`, l'afficher en haut de l'écran avec son
avancement et un bouton de reprise, conformément à la section 5 de
`questionnaire.md`.

Il n'écrase pas le tableau de bord du dernier diagnostic complété : les deux
coexistent, le diagnostic en cours étant un bandeau, pas une page de substitution.

---

## 8. Frontend

Un seul appel réseau, un seul état de chargement. Squelette de chargement plutôt
qu'un indicateur centré : la structure de la page reste stable, ce qui évite le
décalage visuel à l'arrivée des données.

Les composants de graphique sont mémoïsés — Recharts recalcule sa géométrie à
chaque rendu du parent.

`recharts` requiert `react-is` à la même version majeure que React.

Responsive : le radar reste lisible en dessous de 480 px, où il occupe toute la
largeur avec le tableau de données en dessous. Les cartes passent en colonne
unique.

Toutes les couleurs, typographies et espacements proviennent du skill
`charte-maat`. Aucune valeur hexadécimale inventée.

---

## Cas de test

**Endpoint**

1. Aucun diagnostic → 200, `hasCompletedDiagnostic: false`, aucune erreur.
2. Diagnostic `InProgress` uniquement → 200 avec avancement, sans score.
3. Un diagnostic complété → cinq scores de domaine, score global, historique à un point.
4. Plusieurs diagnostics → historique trié du plus ancien au plus récent, le dernier servant de référence.
5. Diagnostic `Archived` → absent de l'historique.
6. Tableau de bord d'une autre entreprise inaccessible → seules les données du principal authentifié apparaissent.
7. `Viewer` → 200.

**Benchmark**

8. Moins de 5 entreprises dans le secteur → benchmark absent, motif indiqué.
9. Cinq entreprises ou plus → percentile calculé.
10. Une entreprise ayant plusieurs diagnostics complétés ne compte qu'une fois.
11. La réponse ne contient aucun identifiant, nom ni score individuel d'une autre entreprise.

**Plan d'actions**

12. Cinq recommandations retournées au maximum, triées par `priority_rank`.
13. Le total renvoyé correspond au nombre réel de recommandations du diagnostic.
14. Le taux d'avancement reflète les actions cochées.

**Frontend**

15. Le radar affiche cinq axes sur une échelle fixe 0–100, indépendamment des valeurs.
16. Le tableau alternatif des scores est présent dans le DOM et accessible au clavier.
17. Un seul diagnostic → pas de courbe d'évolution, message de renouvellement.
18. Deux diagnostics ou plus → courbe affichée avec l'écart au précédent.
19. `Viewer` → cases d'avancement non actionnables.
20. Test axe sur l'écran complet, dans les trois états de la section 1.

Les cas 8 et 11 portent l'exigence RGPD d'anonymisation : ce sont ceux à
vérifier réellement plutôt qu'à asserter.
