# 0012 — Limites d'offre contrôlées côté serveur, par une table de droits unique

## Statut

Acceptée — 2026-09-27

## Contexte

Les trois offres commercialisées (Starter, Essential, Professional) promettent des
contenus différents, mais jusqu'ici toutes les fonctionnalités restaient ouvertes
quel que soit l'abonnement. Il faut désormais les limiter (`docs/specs/abonnement.md`,
section 8) : nombre de diagnostics, nombre de recommandations visibles, scores par
domaine, suivi des actions, indicateurs, benchmark, support, contenu du rapport PDF.

Trois questions se posent : **où** vérifier un droit, **comment** éviter que la règle
se disperse dans une dizaine d'endpoints, et **que faire des données** d'une entreprise
dont l'offre baisse.

## Options étudiées

**Masquer dans le frontend seulement.** Le plus rapide : l'écran cache ce que l'offre
n'inclut pas. Écarté : l'API reste appelable directement avec un jeton valide, et les
données masquées transitent quand même dans les réponses. Une limite commerciale qui se
contourne avec les outils de développement du navigateur n'en est pas une.

**Tester l'offre dans chaque endpoint.** Chaque contrôleur compare l'offre de
l'entreprise à celle qu'il exige. Écarté : la même règle (par exemple « Starter voit 3
recommandations ») serait recopiée dans le tableau de bord, la liste des
recommandations, le plan d'actions et le rapport, et finirait par diverger.

**Une table de droits pure dans le Domain.** `PlanEntitlements` reçoit l'offre et le
statut de l'abonnement, et rend l'offre effective et ses droits. Les services
Application la consultent ; le frontend reçoit ces droits calculés par l'API.

## Décision

La troisième option. `PlanEntitlements` est le seul endroit du code qui compare des
offres. Sans I/O ni dépendance EF, elle se teste unitairement ligne par ligne du
tableau de la spec, comme `ScoringService`. Une action hors de l'offre répond `403`
avec `{ "code": "plan_required", "requiredPlan": ... }`, ce qui permet à l'écran de
distinguer un manque de droit d'offre (proposer l'offre supérieure) d'un manque de rôle.

Le frontend ne recopie aucune règle : `GET /api/billing/subscription` lui renvoie les
droits déjà calculés, dont `canStartDiagnostic`, qui dépend aussi du nombre de
diagnostics complétés.

Deux choix de comportement accompagnent la décision :

- **Une baisse d'offre n'efface rien.** Les données restent en base et s'affichent selon
  les droits de la nouvelle offre ; un nouvel abonnement les rend de nouveau visibles.
  Les limites portent sur la consultation et l'écriture, jamais sur le calcul : les
  recommandations sont toujours toutes persistées. Supprimer des données au moment d'une
  résiliation serait irréversible, et pénaliserait une entreprise qui revient.
- **Un impayé (`PastDue`) garde les droits payants** pendant les relances de Stripe.
  Couper l'accès dès le premier échec de prélèvement sanctionnerait une simple carte
  expirée. Si Stripe abandonne, le webhook de fin d'abonnement ramène l'entreprise sur
  Starter, et les droits suivent sans mécanisme supplémentaire.

## Conséquences

- Chaque service concerné dépend de l'offre de l'entreprise : une lecture de plus
  (`ISubscriptionRepository`) par requête sur ces endpoints, négligeable à l'échelle
  du produit.
- Le rapport PDF dépend de l'offre au moment de la génération. La règle de déterminisme
  (`rapport-pdf.md`, section 3) intègre donc l'offre effective parmi les données
  d'entrée.
- Les fonctionnalités annoncées mais pas encore construites restent visibles dans le
  comparatif avec la mention « Bientôt », plutôt que d'être promises sans exister.
