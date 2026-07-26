# 0003 — Algorithme de calcul du score RSE

## Statut

Acceptée — 2026-07-27

## Contexte

Le score RSE est le cœur du produit : c'est la partie la plus susceptible
d'être disséquée en soutenance, et celle dont une erreur silencieuse serait
la plus dommageable (un score faux, montré à une PME cliente, sans qu'on
puisse l'expliquer). La spécification complète est dans
`docs/specs/scoring.md` ; cette ADR consigne les décisions structurantes de
l'implémentation (`backend/MAAT.Domain/Services/ScoringService.cs`) et les
cas de test qui les vérifient (`backend/MAAT.Domain.Tests/ScoringServiceTests.cs`),
pour qu'elles puissent être justifiées devant le jury sans rouvrir le code.

Le service a été développé en TDD strict : tous les cas de test cités
existaient avant la première ligne d'implémentation.

## Décision

### Deux formules, séparées

**Score d'un domaine `d`** (sur 100) :

```
Score_d = ( Σ(rᵢ × wᵢ) / Σ(wᵢ × 5) ) × 100
```

Le dénominateur `Σ(wᵢ × 5)` est le score maximal atteignable si toutes les
réponses du domaine valaient 5 : le score de domaine est donc un pourcentage
de ce plafond, indépendant du nombre de questions ou de leur poids relatif.
→ Cas 1 (plancher, `r=0` partout), Cas 2 (plafond, `r=5` partout), Cas 3
(calcul détaillé avec poids hétérogènes : `19 / 30 × 100 = 63,33`).

**Score global**, moyenne des scores de domaine pondérée par la pondération
sectorielle `ρ` :

```
Score_global = Σ (Score_d × ρ_d)
```

Les deux formules sont volontairement distinctes plutôt que fusionnées en un
seul calcul : `Score_d` ne connaît que les réponses, `Score_global` ne
connaît que les scores de domaine déjà calculés et `ρ`. Ça permet d'exposer
`DomainScoreDetail` (numérateur, dénominateur, score) indépendamment du score
global, condition de la traçabilité exigée par la spec — le détail par
domaine doit pouvoir être affiché et justifié sans recalcul.
→ Cas 4, le test qui démontre la valeur métier du produit : mêmes réponses,
deux secteurs différents (transport routier vs conseil), trois points d'écart
sur le score final uniquement parce que `ρ` diffère.

### `decimal`, jamais `double`

Tout le calcul — numérateurs, dénominateurs, scores de domaine, score global —
est fait en `decimal`. Les flottants binaires (`double`) introduisent des
écarts d'arrondi qui rendent un score non reproductible à l'identique d'une
exécution à l'autre selon l'ordre des opérations, ce qui est inacceptable
pour un score régénéré à la demande (aucun PDF n'est persisté — voir ADR
0001) et invérifiable en test sans tolérance d'epsilon.
→ C'est ce choix qui permet aux tests de comparer les résultats par égalité
stricte (`Assert.Equal(19m, ...)`, `Assert.Equal(0m, ...)`, `Assert.Equal(100m, ...)`)
plutôt qu'à une marge près : Cas 1, Cas 2, Cas 3.

### Arrondi d'affichage : `MidpointRounding.AwayFromZero`, jamais en cours de calcul

Deux règles distinctes :

1. **Aucun arrondi intermédiaire.** `Score_d` est réinjecté dans
   `Score_global` en pleine précision decimal, jamais arrondi au préalable.
   Arrondir un score de domaine à 2 décimales avant de le pondérer fausserait
   le résultat final de façon difficile à détecter (l'écart est de l'ordre du
   centième, mais s'accumule sur cinq domaines). `DomainScoreDetail.Score` et
   `ScoringResult.GlobalScore` sont donc tous deux non arrondis ; c'est
   l'appelant (persistance en `numeric(5,2)`, affichage) qui arrondit.
   → Cas 3 et Cas 4 : les valeurs attendues (`63.33`, `57.33`, `53.83`) ne
   sont vérifiées qu'après un `Math.Round(..., 2)` fait côté test, jamais côté
   service.

2. **`RoundForDisplay` utilise `MidpointRounding.AwayFromZero`, pas l'arrondi
   par défaut de .NET** (`ToEven`, dit « du banquier »). L'arrondi par défaut
   rapproche `57,5` de `58` mais aussi `58,5` de `58` — un comportement
   incohérent à l'œil d'un utilisateur qui voit son score reculer après un
   arrondi, et impossible à justifier simplement à l'oral. `AwayFromZero`
   arrondit toujours le point médian vers le haut en valeur absolue :
   comportement intuitif et seul défendable devant un jury.
   → Cas 5, qui isole cette fonction du reste du calcul et teste précisément
   le point médian : `57.50 → 58`, `58.50 → 59` (le cas qui échoue avec
   l'arrondi par défaut), `57.49 → 57`, `0.50 → 1`.

### Renormalisation quand un domaine n'a aucune question active

Si toutes les questions d'un domaine sont désactivées, son dénominateur
`Σ(wᵢ × 5)` vaut zéro : le domaine est exclu du calcul plutôt que de
provoquer une division par zéro. Les coefficients `ρ` des domaines restants
sont alors renormalisés pour que leur somme revienne à 1 :

```
ρ_d,effectif = ρ_d / Σ ρ_d' (pour les domaines d' effectivement inclus)
```

Cette formule est appliquée systématiquement, y compris quand aucun domaine
n'est exclu — dans ce cas `Σ ρ_d' = 1` et la division est neutre. Un seul
chemin de calcul couvre donc le cas général et le cas d'exclusion, sans
branche spéciale : c'est un choix délibéré pour garder l'algorithme simple à
expliquer (pas de `if` distinct pour un cas « normal » vs un cas « dégradé »).
→ Cas 7 : Achats (`ρ = 0,15`) exclu, les quatre coefficients restants
(`0,30 · 0,30 · 0,20 · 0,05`, somme `0,85`) divisés par `0,85`, résultat
vérifié chiffré (`51 / 0,85 = 60`). Cas 1, 2, 3, 4 et 6 vérifient implicitement
que la formule reste neutre quand `Σ ρ = 1` d'emblée.

### Pondération sectorielle manquante : exception, jamais un repli à zéro

Un domaine avec au moins une question active a nécessairement un `Score_d` à
agréger. Si `sectorWeights` ne contient aucune entrée pour ce domaine, le
traiter comme `ρ_d = 0` l'exclurait silencieusement du score global — une
distorsion invisible, contraire à l'exigence de traçabilité de la spec (« la
valeur Transparence du produit interdit la boîte noire »).

C'est un cas distinct de la pondération sectorielle par défaut (secteur NAF
non couvert) : ce repli-là se décide **en amont** de l'appel au service, et le
dictionnaire par défaut couvre par construction les cinq domaines — garanti
par l'invariant testé sur le seed `SectorWeight` (cas 10, `MAAT.IntegrationTests`).
Une entrée manquante à ce stade signale donc une erreur de données de
référence (coefficient oublié, migration partielle), pas un secteur inconnu :
le service lève `SectorWeightNotFoundException`, identifiant le domaine en
cause, et ne produit aucun score, ni partiel ni complet.
→ Cas 11 : un domaine avec une question active absent du dictionnaire
`sectorWeights` transmis lève l'exception avec le bon domaine.

## Conséquences

- Le calcul n'a aucune constante liée au nombre de domaines : il itère sur
  les domaines réellement présents dans les données. Ajouter un sixième
  domaine ne demande qu'une valeur d'enum `RseDomain` et des lignes
  `SectorWeight` — aucune modification de `ScoringService`.
- Deux exceptions métier explicites (`IncompleteDiagnosticException`,
  `SectorWeightNotFoundException`) plus une garde `ArgumentOutOfRangeException`
  sur les bornes de `r` et `w` (cas 8 et 9) : le service échoue fort plutôt
  que de produire un score partiel ou faussé. Aucun repli silencieux nulle
  part dans le calcul.
- Toute évolution de la formule de score (domaine ou global) doit d'abord
  casser un test existant dans `ScoringServiceTests.cs` — conformément à la
  règle du `CLAUDE.md` sur le TDD strict pour ce service.
- L'arrondi n'existe qu'à deux endroits : la persistance (`numeric(5,2)`) et
  `RoundForDisplay`. Toute nouvelle surface d'affichage du score (API,
  frontend, PDF) doit passer par `RoundForDisplay`, jamais par un
  `Math.Round` ad hoc qui réintroduirait l'arrondi du banquier.
