# 0011 — Stripe pour les abonnements, via Checkout hébergé

## Statut

Acceptée — 2026-09-26

## Contexte

MAAT passe d'un produit gratuit à trois offres, dont deux payantes par abonnement
mensuel ou annuel (`docs/specs/abonnement.md`). Il faut encaisser des paiements
récurrents par carte, émettre des factures conformes, appliquer la TVA (prix affichés
hors taxes, clients B2B dont certains dans d'autres États membres), relancer les
échecs de paiement et laisser le client gérer seul son abonnement.

La souveraineté des données est un argument commercial du produit (CLAUDE.md,
« Hébergement »). Mais la règle qui en découle vise l'**hébergement** : aucun
hébergeur ni stockage hors UE. Un prestataire de paiement n'héberge pas l'application ;
`auth-securite-rgpd.md` (section 6, « Sous-traitants ») prévoyait déjà Stripe, sous
clauses contractuelles types, à condition qu'aucune donnée de diagnostic n'y transite.

## Options étudiées

**Stripe.** Société américaine (entité contractante européenne : Stripe Payments
Europe, Irlande), donc soumise au CLOUD Act. Abonnements, factures, Stripe Tax (TVA
et autoliquidation), relances, portail client prêt à l'emploi, mode test complet, SDK
.NET officiel (licence MIT) et documentation abondante.

**Mollie.** Société néerlandaise, API d'abonnements et page de paiement hébergée.
Argument de souveraineté plus fort, mais pas d'équivalent au portail client ni au
calcul automatique de la TVA : ces deux écrans et leurs règles seraient à développer
dans MAAT, pour un projet dont le cœur est le diagnostic RSE.

**Intégration du paiement dans nos propres formulaires.** Écartée d'emblée : les
données de carte transiteraient par l'API, ce qui ferait passer MAAT à un niveau de
conformité PCI DSS hors de portée d'un projet de cette taille.

## Décision

Stripe, uniquement à travers ses **pages hébergées** : Checkout pour le paiement,
portail client pour la gestion de l'abonnement. Le navigateur est redirigé vers
Stripe puis revient sur MAAT ; aucune donnée de carte ne touche nos serveurs.

Ce que Stripe reçoit de MAAT se limite à l'identifiant de l'entreprise (métadonnée
`maat_company_id`) et à l'adresse e-mail de l'administrateur qui paie. Aucune donnée
de diagnostic, et ce cloisonnement est vérifiable : `StripePaymentGateway` est le
seul fichier du backend qui référence le SDK, et l'interface `IPaymentGateway` qu'il
implémente ne transporte aucune donnée RSE.

L'état d'une offre payante vient **toujours de Stripe**, relu côté serveur avec la clé
secrète — jamais de ce qu'affirme le navigateur, qu'un utilisateur pourrait forger. Au
retour de la page de paiement, l'API relit la session Checkout désignée dans l'URL et
active l'offre aussitôt ; le webhook signé couvre le reste (renouvellements, impayés,
résiliation). L'activation ne dépend donc pas de la livraison du webhook : une première
version qui attendait uniquement celui-ci restait bloquée sur l'écran de confirmation
dès que le webhook tardait ou n'était pas relayé (poste de développement), ce qui
rendait la démonstration impossible sans intervention.

## Conséquences

- Un sous-traitant hors UE de plus au registre des traitements, avec contrat
  article 28 et clauses contractuelles types. À présenter tel quel en soutenance :
  les **données RSE** restent en France ; seules les données de facturation passent
  par Stripe.
- Remplacer Stripe par Mollie (ou un autre prestataire) ne toucherait qu'un fichier
  d'Infrastructure et la configuration : Domain, Application, contrôleurs et frontend
  ne connaissent que `IPaymentGateway` et une URL de redirection. Le portail client
  et le calcul de TVA seraient en revanche à reconstruire.
- La réforme de la facturation électronique impose aux PME d'**émettre** des factures
  électroniques via une plateforme agréée à partir de septembre 2027. Les factures
  Stripe n'en passent pas par une : point à réévaluer avant cette échéance.
- Les tests automatisés n'appellent jamais Stripe (`FakePaymentGateway`) : le
  parcours de paiement réel se vérifie en mode test Stripe, à la main ou avec
  `STRIPE_E2E=1`.
