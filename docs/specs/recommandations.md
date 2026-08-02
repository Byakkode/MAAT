# Spécification — Moteur de recommandations

Périmètre : sélection des recommandations déclenchées par un diagnostic, calcul
de leur ordre de priorité, persistance, consultation et suivi d'avancement.

C'est le module qui transforme un score en plan d'actions — donc celui qui porte
la valeur d'usage du produit. Un score seul n'aide personne à progresser.

Dépendances : `modele-donnees.md` (entités `Recommendation`,
`DiagnosticRecommendation`), `questionnaire.md` section 6 (le déclenchement a lieu
à la complétion, étape 5 laissée en suspens), `scoring.md` (pondération sectorielle
réutilisée dans la priorisation).

---

## 1. Déclenchement

Une recommandation est retenue si la réponse à sa question déclencheuse est
**inférieure ou égale** à son seuil :

```
Response.value ≤ Recommendation.trigger_max_value
```

Seules les recommandations actives (`is_active = true`) sont évaluées.

Une question sans réponse ne peut pas se produire à ce stade : la complétion exige
que toutes les questions actives soient renseignées. Si le cas survient malgré
tout, c'est une incohérence de données — lever une exception plutôt que d'ignorer
silencieusement.

Le calcul a lieu **dans la transaction de complétion**, après l'écriture des
scores. Si la sélection échoue, toute la complétion est annulée : un diagnostic
avec un score mais sans plan d'actions est un produit à moitié livré.

---

## 2. Priorisation

Toutes les recommandations déclenchées sont conservées, mais elles sont ordonnées.
Un plan d'actions de quarante lignes non hiérarchisées est aussi inutilisable
qu'une page blanche.

```
Priorité = (impact_points × ρ_domaine) / coût_effort
```

| Terme | Source |
| --- | --- |
| `impact_points` | Colonne de `Recommendation` |
| `ρ_domaine` | Pondération sectorielle du domaine de la recommandation |
| `coût_effort` | `Low` = 1 · `Medium` = 2 · `High` = 3 |

**Justification, à savoir défendre à l'oral.** Le facteur `ρ_domaine` traduit un
fait mathématique du modèle de score : un point gagné dans un domaine pondéré à
0,40 pour votre secteur déplace le score global quatre fois plus qu'un point gagné
dans un domaine pondéré à 0,10. La priorité exprime donc « gain attendu sur le
score global, par unité d'effort ». Ce n'est pas une heuristique arbitraire, c'est
la dérivée du score par rapport à l'effort.

Utiliser la pondération **effectivement appliquée** au diagnostic, celle persistée
dans `DomainScore.sector_weight`, et non une relecture de `SectorWeight` — sinon
un plan d'actions recalculé après révision de la table ne correspondrait plus au
score qu'il accompagne.

**Départage.** À priorité égale, trier par `Recommendation.code` croissant. Ce
n'est pas cosmétique : sans départage déterministe, deux exécutions produisent des
ordres différents, et la promesse de régénération du PDF à l'identique tombe.

`priority_rank` est ensuite écrit sur chaque ligne `DiagnosticRecommendation`,
en partant de 1. Il est **figé** : l'ordre ne se recalcule jamais à l'affichage.

---

## 3. Calibrage de `impact_points`

Définition à respecter lors de la rédaction du contenu : **gain estimé en points
sur le score du domaine** si l'action est pleinement mise en œuvre.

Cette définition rend la valeur vérifiable. Le gain maximal théorique atteignable
via la question déclencheuse est :

```
(5 − trigger_max_value) × w / Σ(5w du domaine) × 100
```

Une recommandation dont `impact_points` dépasse nettement cette borne est mal
calibrée. Ajouter un test de cohérence sur le seed qui le signale — pas un échec
bloquant, une liste d'avertissements : une recommandation peut légitimement
améliorer plusieurs questions à la fois.

Sans cette discipline, `impact_points` devient un nombre d'ambiance et la
priorisation perd tout fondement.

---

## 4. Consultation

`GET /api/diagnostics/{id}/recommendations`

Retourne les recommandations du diagnostic, triées par `priority_rank`, avec le
libellé, le détail, le domaine, le niveau d'effort, les points d'impact, l'état
d'avancement et la date d'achèvement.

Accessible aux trois rôles, `Viewer` compris — c'est une lecture. Scopé par
entreprise selon le patron établi : un diagnostic d'une autre entreprise
retourne 404.

**La jointure vers `Recommendation` ignore `is_active`.** Une recommandation
désactivée après coup doit rester visible dans les plans d'actions déjà émis,
sinon l'historique se réécrit tout seul.

**Aucune recommandation déclenchée** est un résultat valide, pas une erreur.
Retourner une liste vide et laisser le frontend afficher un message de félicitation
— c'est le cas d'une entreprise mature, et il doit être traité comme un succès.

---

## 5. Suivi d'avancement

`PATCH /api/diagnostics/{id}/recommendations/{recommendationCode}`

Corps : l'état d'achèvement. Bascule `is_completed` et renseigne ou efface
`completed_at`.

Rôles `Admin` et `User`. `Viewer` obtient 403.

Autorisé y compris sur un diagnostic `Completed` — c'est même le cas normal : le
plan d'actions se suit dans les mois qui suivent le diagnostic. C'est la seule
exception à l'immuabilité posée par `questionnaire.md` section 1, et elle est
cohérente : on modifie le suivi, pas les réponses ni le score.

**Cocher une action ne modifie jamais le score.** La tentation est réelle, elle
doit être écartée explicitement. Un score qui progresse sur déclaration sans
réévaluation devient de l'auto-évaluation non vérifiée, ce qui ruine la
crédibilité du diagnostic — et donc l'intérêt de le présenter à un donneur
d'ordres. Le score ne bouge qu'avec un nouveau diagnostic.

Ce point mérite une phrase dans l'interface : « cette action sera prise en compte
lors de votre prochain diagnostic ».

---

## 6. Contenu

Cible : 150+ recommandations réparties sur les cinq domaines, chacune rattachée à
une question déclencheuse.

Chaque question du référentiel doit avoir **au moins une** recommandation
déclenchable, sinon une entreprise peut obtenir un point faible sans se voir
proposer d'action correspondante. Ajouter un test de couverture sur le seed
vérifiant que chaque `Question` active est référencée par au moins une
`Recommendation` active.

Les recommandations doivent être actionnables par une PME de 20 salariés sans
direction RSE : une action concrète, un ordre de grandeur d'effort, et si possible
un renvoi vers un dispositif existant — ADEME, Bpifrance, CCI, France Num. Une
recommandation qui dit « mettre en place une politique RSE » n'aide personne.

Ce travail de rédaction ne se délègue pas : c'est lui qui sera examiné en
soutenance.

---

## Cas de test

**Déclenchement**

1. Réponse strictement inférieure au seuil → recommandation retenue.
2. Réponse **égale** au seuil → retenue (comparaison inclusive).
3. Réponse supérieure au seuil → non retenue.
4. Recommandation inactive → jamais retenue, quelle que soit la réponse.
5. Diagnostic sans aucun déclenchement → liste vide, complétion réussie malgré tout.

**Priorisation**

6. Deux recommandations, même impact et même effort, domaines de pondérations différentes → celle du domaine le plus lourd passe devant.
7. Même impact et même domaine, efforts `Low` et `High` → `Low` devant.
8. Priorités strictement égales → tri par `code` croissant.
9. Deux exécutions sur les mêmes données produisent un ordre identique.
10. `priority_rank` commence à 1 et ne comporte ni trou ni doublon.
11. La pondération utilisée est celle persistée dans `DomainScore`, pas une relecture de `SectorWeight` — vérifiable en modifiant `SectorWeight` après complétion et en constatant que l'ordre ne bouge pas.

**Transaction**

12. Échec de la sélection → complétion annulée intégralement, aucun `DomainScore`, statut resté `InProgress`.

**Consultation**

13. Liste triée par `priority_rank`.
14. Recommandation désactivée après complétion → toujours présente dans la liste.
15. Diagnostic d'une autre entreprise → 404.
16. `Viewer` en lecture → 200.

**Suivi**

17. Bascule à terminé → `is_completed` vrai, `completed_at` horodaté.
18. Bascule inverse → `is_completed` faux, `completed_at` effacé.
19. Bascule sur un diagnostic `Completed` → autorisée.
20. Bascule → `Diagnostic.global_score` **inchangé**.
21. `Viewer` tentant une bascule → 403.

**Seed**

22. Chaque `Question` active est référencée par au moins une `Recommendation` active.
23. Avertissement listant les recommandations dont `impact_points` dépasse le gain maximal théorique de leur question déclencheuse.

Les cas 9 et 11 sont ceux qui garantissent la régénération à l'identique du
rapport PDF. Le cas 20 est celui qui protège la crédibilité du score.
