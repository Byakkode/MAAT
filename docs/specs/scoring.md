# Spécification — Moteur de calcul du score RSE

C'est le cœur du produit et la partie la plus susceptible d'être disséquée en
soutenance. Elle doit être implémentée en TDD strict : les tests de cette
spécification sont écrits **avant** toute ligne d'implémentation.

## Emplacement et contraintes

`MAAT.Domain/Services/ScoringService.cs`.

Service **pur** : aucune I/O, aucun accès base, aucune dépendance à EF Core ou à
quoi que ce soit hors de la BCL. Il reçoit des données en entrée et retourne un
résultat. C'est ce qui rend `MAAT.Domain.Tests` instantané et le calcul
vérifiable indépendamment de l'infrastructure.

## Les cinq domaines

`Environmental` · `Social` · `Ethics` · `Procurement` · `Governance`

Voir `modele-donnees.md` pour leur ancrage normatif. Le service ne connaît que
l'énumération : il ne fait aucune hypothèse sur le nombre de domaines en dur.
Écrire le calcul de façon à ce que l'ajout d'un sixième domaine ne demande qu'une
valeur d'enum et des lignes de `SectorWeight`.

## Entrées

- La liste des réponses : pour chaque question, sa valeur `r` (0 à 5), son poids
  `w` (> 0) et son domaine.
- La pondération sectorielle : un coefficient `ρ` par domaine, dont la somme
  vaut 1.

## Formules

Note attribuée par question : `r ∈ {0, 1, 2, 3, 4, 5}` — 0 = critère non satisfait,
5 = pleinement satisfait, valeurs intermédiaires pour les réponses partielles.

**Score d'un domaine `d`**, exprimé sur 100 :

```
Score_d = ( Σ(rᵢ × wᵢ) / Σ(wᵢ × 5) ) × 100
```

où la somme porte sur les questions du domaine `d`.

**Score global**, moyenne pondérée des scores de domaine :

```
Score_global = Σ (Score_d × ρ_d)
```

## Précision et arrondi

Calculer en `decimal`, jamais en `double` : les flottants binaires introduisent
des écarts qui rendent les tests instables et les scores non reproductibles.

Ne jamais arrondir un score de domaine avant de le réinjecter dans le score
global — l'arrondi intermédiaire fausse le résultat final. Les scores de domaine
sont conservés en pleine précision et arrondis **uniquement** à l'affichage.

Persistance : `numeric(5,2)`, arrondi à 2 décimales.
Affichage : entier, `MidpointRounding.AwayFromZero`.

Ce dernier point n'est pas un détail : l'arrondi par défaut de .NET est celui du
banquier, qui arrondit `57,5` à `58` mais `58,5` à `58` également. Un utilisateur
qui voit son score passer de 58,5 à 58 ne comprendra pas, et vous ne saurez pas
l'expliquer en soutenance.

---

## Cas de test

À écrire dans `MAAT.Domain.Tests/ScoringServiceTests.cs` avant l'implémentation.

### 1 — Plancher

Toutes les réponses à 0, quelle que soit la pondération.
→ Chaque `Score_d` = 0, `Score_global` = 0.

### 2 — Plafond

Toutes les réponses à 5, quelle que soit la pondération.
→ Chaque `Score_d` = 100, `Score_global` = 100.

Ce test vérifie implicitement l'invariant `Σρ_d = 1` : si la somme des
coefficients diffère de 1, le score global s'écarte de 100 et le test échoue.
C'est le filet de sécurité contre le bug le plus dangereux du produit.

### 3 — Score de domaine, calcul détaillé

Domaine Environnement, trois questions :

| Question | poids `w` | réponse `r` | `r × w` |
| --- | --- | --- | --- |
| ENV-01 | 3 | 5 | 15 |
| ENV-02 | 2 | 2 | 4 |
| ENV-03 | 1 | 0 | 0 |

```
Σ(r × w)     = 19
Σ(w × 5)     = (3 + 2 + 1) × 5 = 30
Score_env    = 19 / 30 × 100 = 63,3333…
```

→ Attendu : `63.33` après arrondi de persistance, `63` à l'affichage.

### 4 — Pondération sectorielle : mêmes réponses, secteurs différents

**C'est le test qui prouve la valeur métier du produit.** Un transporteur routier
et un cabinet de conseil n'ont pas les mêmes enjeux prioritaires : l'empreinte
carbone est critique pour l'un, marginale pour l'autre, tandis que l'éthique des
affaires et la gestion des données pèsent davantage sur le second.

Scores de domaine identiques dans les deux cas :

| Domaine | Score |
| --- | --- |
| Environnement | 63,3333… |
| Social | 80 |
| Éthique | 50 |
| Achats responsables | 40 |
| Gouvernance | 25 |

**Transport routier** — ρ : Env 0,40 · Social 0,20 · Éthique 0,15 · Achats 0,15 · Gouv 0,10

```
63,3333 × 0,40 = 25,3333
80      × 0,20 = 16
50      × 0,15 = 7,50
40      × 0,15 = 6
25      × 0,10 = 2,50
                 ────────
                 57,3333…
```

→ Attendu : `57.33`, affiché `57`.

**Conseil** — ρ : Env 0,10 · Social 0,30 · Éthique 0,25 · Achats 0,15 · Gouv 0,20

```
63,3333 × 0,10 = 6,3333
80      × 0,30 = 24
50      × 0,25 = 12,50
40      × 0,15 = 6
25      × 0,20 = 5
                 ────────
                 53,8333…
```

→ Attendu : `53.83`, affiché `54`.

Deux entreprises, des réponses rigoureusement identiques, trois points d'écart au
score affiché. Ce test est la démonstration chiffrée que la pondération
sectorielle fonctionne — c'est celui à montrer au jury.

### 5 — Arrondi au point médian

Test unitaire de la fonction d'arrondi d'affichage, isolément du calcul :

| Entrée | Attendu |
| --- | --- |
| `57.50` | `58` |
| `58.50` | `59` |
| `57.49` | `57` |
| `0.50` | `1` |

Le deuxième cas est celui qui échoue avec l'arrondi par défaut de .NET.

### 6 — Secteur non couvert

Entreprise dont le code NAF n'a aucune entrée dans `SectorWeight`.
→ Repli sur la pondération par défaut : 0,20 sur chacun des cinq domaines.
→ Aucune exception levée.

Vérifier avec les scores du cas 4 :

```
(63,3333 + 80 + 50 + 40 + 25) × 0,20 = 258,3333 × 0,20 = 51,6666…
```

→ Attendu : `51.67`, affiché `52`.

### 7 — Domaine sans question active

Si toutes les questions d'un domaine sont désactivées, `Σ(wᵢ × 5) = 0`.
→ **Ne pas diviser par zéro.** Le domaine est exclu du calcul, et les coefficients
des domaines restants sont renormalisés pour que leur somme revienne à 1.

Exemple : si Achats responsables (ρ = 0,15) n'a aucune question active, les quatre
coefficients restants sont divisés par 0,85.

### 8 — Diagnostic incomplet

Au moins une question active sans réponse.
→ Le service refuse de calculer et lève une exception métier explicite.
Un score partiel n'est jamais produit ni persisté.

### 9 — Valeurs hors bornes

`r` < 0 ou `r` > 5, ou `w` ≤ 0.
→ Exception d'argument. Ces cas ne doivent jamais atteindre le calcul, mais la
garde documente le contrat.

### 10 — Invariant de la table de pondération

Test parcourant l'intégralité du seed `SectorWeight` :

- pour chaque `sector_code` non nul, la somme des cinq coefficients vaut
  exactement 1, et les cinq domaines sont présents ;
- il existe **exactement un** jeu de pondérations par défaut (`is_default =
  true`), couvrant lui aussi les cinq domaines avec une somme de 1.

Ce test appartient à `MAAT.IntegrationTests` puisqu'il lit la base, mais il est
mentionné ici parce qu'il protège le moteur de scoring : un secteur mal saisi,
ou un jeu par défaut dupliqué ou incomplet, produit un score faux et silencieux.

### 11 — Dictionnaire de pondération sectorielle incomplet

Distinct du cas 7. Un domaine `d` a au moins une question active — donc un
`Score_d` à agréger — mais `sectorWeights` ne contient aucune entrée pour `d`.

Ce n'est pas un secteur non couvert (cas 6 : le repli sur la pondération par
défaut se décide **avant** l'appel à `ScoringService`, en amont, et le
dictionnaire par défaut couvre par construction les cinq domaines — garanti
par le cas 10). Ici, le dictionnaire *transmis* au service est lui-même
incomplet : c'est une erreur de données de référence (`SectorWeight` mal
saisi, migration partielle, coefficient oublié), qui atteint le service parce
qu'elle n'a pas été détectée en amont.

Traiter un `ρ_d` absent comme `0` serait un repli silencieux : le domaine
disparaîtrait du score global sans qu'aucune erreur ne le signale, produisant
un score faux et non détectable par l'utilisateur ni par les tests
d'intégration du cas 10 (qui valide le seed, pas les données réellement
transmises à l'exécution).

→ Le service lève une exception métier explicite (`SectorWeightNotFoundException`)
identifiant le domaine en cause. Aucun score, partiel ou complet, n'est produit.

---

## Ce qu'il ne faut pas faire

**Ne pas persister de contribution par réponse.** Le score se recalcule
intégralement depuis les réponses brutes et la pondération courante. Stocker des
valeurs dérivées crée une désynchronisation silencieuse le jour où un poids change.

**Ne pas mélanger calcul et présentation.** Le service retourne des valeurs en
pleine précision. L'arrondi d'affichage appartient à la couche API ou au frontend.

**Ne pas coder le nombre de domaines en dur.** Aucune constante `5`, aucun tableau
de taille fixe, aucune série de cinq `if`. Le calcul itère sur les domaines
présents dans les données.

**Ne pas introduire d'aléatoire ni de dépendance temporelle.** Deux exécutions sur
les mêmes entrées doivent produire le même résultat, indéfiniment. C'est ce qui
permet de régénérer un rapport PDF à la demande sans le stocker.

---

## Traçabilité

Le score doit rester explicable — la valeur « Transparence » du produit interdit
la boîte noire. Le service expose, en plus du score global, le détail par domaine
avec le numérateur, le dénominateur et le coefficient sectoriel appliqué. Ces
éléments sont persistés dans `DomainScore` et alimentent l'explication affichée à
l'utilisateur ainsi que le rapport PDF.

Une fois l'implémentation validée, consigner l'algorithme dans une ADR sous
`docs/adr/` : c'est le document que vous ouvrirez si le jury demande à voir le
calcul.
