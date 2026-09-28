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

**Limite par offre.** Seules les 3 (Starter) ou 12 (Essential) premières par
`priority_rank` sont renvoyées, toutes en Professional, avec le nombre total déclenché (`abonnement.md`,
section 8), le total passant par l'en-tête `X-Total-Count`. Toutes restent persistées : la limite porte sur la consultation, jamais
sur le calcul, et un changement d'offre les rend visibles sans rien recalculer.

**Aucune recommandation déclenchée** est un résultat valide, pas une erreur.
Retourner une liste vide et laisser le frontend afficher un message de félicitation
— c'est le cas d'une entreprise mature, et il doit être traité comme un succès.

---

## 4 bis. Historique du suivi des actions

Offre Professional (`abonnement.md`, section 8). Chaque modification du plan d'actions
enrichi (`PATCH /api/diagnostics/{id}/action-plan/{code}`) laisse une trace par champ
modifié : statut, responsable, échéance, notes. Une ligne porte le champ, l'ancienne et
la nouvelle valeur, la date et l'auteur. Un enregistrement qui ne change rien n'en laisse
aucune. La trace est écrite dans la même transaction que le suivi : l'un sans l'autre
n'existe jamais.

**Notes : « notes modifiées », jamais leur contenu.** Les notes sont internes à
l'entreprise et peuvent contenir n'importe quoi ; l'historique dit seulement qu'elles ont
changé.

**Rien ne s'enregistre à la frappe.** Chaque ligne d'historique correspond à une action
volontaire de l'utilisateur, ce qui rend tout regroupement inutile :

- **Statut** : un clic sur l'étiquette ouvre un menu des quatre statuts (Radix Dropdown
  Menu, clavier et lecteurs d'écran compris) ; le statut choisi s'enregistre aussitôt, seul.
  Aller de Planifié à Terminé donne une ligne — l'ancien clic cyclique passait par En cours
  et Bloqué, et en laissait trois.
- **Responsable, échéance, notes** : formulaire du détail de l'action, enregistré par
  « Enregistrer » (désactivé tant que rien n'a changé), « Annuler » revenant aux valeurs
  enregistrées. L'ancien enregistrement automatique écrivait les états intermédiaires : une
  échéance en « an 2 », « an 20 », « an 200 » pendant qu'on tapait 2003, ou une ligne de
  notes par pause de frappe.

Changer le statut n'envoie jamais une saisie en cours du formulaire : les autres champs
partent avec leur valeur enregistrée.

**Ce qui n'est pas tracé.** La case « terminée » cochée depuis le tableau de bord
(section 5, offre Essential) : elle n'est pas une modification du suivi enrichi.

**Échéance.** Un jour du calendrier, conservé tel que choisi à minuit UTC
(`ActionItemProgress.Update`). L'écran envoie `2026-11-15` ; sur un serveur réglé sur
Paris, la valeur arrivait à `+01:00`, que PostgreSQL refuse (`timestamptz` n'accepte
qu'un décalage nul), et une conversion en UTC l'aurait ramenée au 14.

**Lecture.** `GET /api/diagnostics/{id}/action-plan/{code}/history`, du plus récent au
plus ancien ; à date égale, dans l'ordre statut, responsable, échéance, notes. Tous les
rôles, `Viewer` compris ; `403 plan_required` hors Professional ; `404` pour le
diagnostic d'une autre entreprise. L'écran charge l'historique d'une action à la
demande (« Voir l'historique » dans son détail), jamais les quarante-cinq à l'affichage
de la page, et le recharge après chaque enregistrement tant qu'il est ouvert.

**Effacement** (`auth-securite-rgpd.md`, section 6). L'historique part avec le diagnostic,
donc avec l'entreprise. Un compte supprimé seul laisse ses lignes, sans auteur (« Compte
supprimé ») : l'historique de l'entreprise reste lisible sans garder l'identité de
quelqu'un qui a exercé son droit à l'effacement.

---

## 5. Suivi d'avancement

`PATCH /api/diagnostics/{id}/recommendations/{recommendationCode}`

Corps : l'état d'achèvement. Bascule `is_completed` et renseigne ou efface
`completed_at`.

Rôles `Admin` et `User`. `Viewer` obtient 403. En Starter, refusé pour tous
(`abonnement.md`, section 8).

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

**Échelle d'`effort_level`, explicitée.** La colonne est le seul levier
éditorial qui distingue deux recommandations de même poids, tant que les
seuils de déclenchement sont uniformes. Elle ne se note pas à l'encouragement.

- `Low` — une personne, moins d'une journée cumulée, sans dépense ni décision
  engageant l'entreprise.
- `Medium` — plusieurs demi-journées, ou une dépense modérée, ou plusieurs
  personnes à mobiliser.
- `High` — change une manière de travailler, engage une dépense significative
  ou une négociation avec un tiers, s'étale sur plusieurs mois.

Relire cette colonne **seule, sans les textes**, après chaque lot de
rédaction.

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

**Historique (section 4 bis)**

24. Chaque champ modifié donne une ligne, avec ancienne et nouvelle valeur et auteur ;
    lecture du plus récent au plus ancien.
25. Enregistrement sans changement → aucune ligne.
26. Notes : une ligne par enregistrement qui les modifie, jamais leur texte (ni en base ni
    dans la réponse). Écran : aucun envoi pendant la saisie ; un seul envoi sur
    « Enregistrer » ; le menu de statut va directement au statut choisi, en un envoi, sans
    emporter la saisie en cours du formulaire.
27. Lecture en Starter ou Essential → `403 plan_required` ; `Viewer` en Professional →
    200 ; diagnostic d'une autre entreprise → 404.
28. Compte supprimé → lignes conservées, auteur absent ; entreprise supprimée → historique
    supprimé.
29. Échéance reçue avec un décalage horaire → conservée au jour choisi, à minuit UTC.

Les cas 9 et 11 sont ceux qui garantissent la régénération à l'identique du
rapport PDF. Le cas 20 est celui qui protège la crédibilité du score.
