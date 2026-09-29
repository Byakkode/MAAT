# Spécification — Abonnements et paiement

Périmètre : les offres commerciales, leur choix par l'entreprise cliente, le paiement
en ligne via Stripe, le suivi de l'abonnement et les **droits que chaque offre ouvre**
(section 8), contrôlés côté serveur.

Dépendances : `auth-securite-rgpd.md` (rôles, droit à l'effacement, sous-traitants),
`modele-donnees.md` (table `Subscription`), `coquille-et-compte.md` (espace du compte),
ADR 0011 (choix de Stripe), ADR 0012 (limites contrôlées côté serveur).

---

## 1. Offres

| Offre | Prix HT | Souscription |
| --- | --- | --- |
| Starter | 0 €, à vie | libre, sans carte bancaire |
| Essential | 149 € / mois ou 1 490 € / an | paiement Stripe |
| Professional | 399 € / mois ou 3 990 € / an | paiement Stripe |
| Enterprise | 899 € / mois ou 8 990 € / an | **bientôt disponible** : ni inscription ni paiement |

Les prix affichés sont **hors taxes**. Stripe Tax ajoute la TVA au paiement d'après
l'adresse de facturation, et applique l'autoliquidation quand un numéro de TVA
intracommunautaire valide est saisi.

Le contenu des offres (prix, options incluses ou non) vit dans un seul fichier,
`frontend/src/billing/plans.ts`, lu à la fois par la section Tarifs de la page
d'accueil et par l'écran de sélection de l'application : les deux tableaux ne peuvent
pas diverger. Côté serveur, `SubscriptionPlanCatalog` (Domain) dit seulement quelles
offres sont payantes et lesquelles se souscrivent en ligne.

## 2. Parcours

### Depuis la page d'accueil

Chaque colonne du comparatif mène à l'inscription avec l'offre et la période choisies :
`/register?offre=essential&periode=annuelle`. L'écran d'inscription rappelle l'offre
choisie et l'envoie avec le compte (`RegisterRequest.Plan`, `BillingPeriod`). Une offre
inconnue ou pas encore ouverte (Enterprise) est ignorée côté client et refusée en `400`
côté serveur — ce refus a lieu **avant** la recherche de doublon d'adresse, sans quoi
il révélerait si l'adresse est déjà inscrite (même principe que le contrôle du mot de
passe, `auth-securite-rgpd.md` section 1).

- **Starter** : l'offre est active dès l'inscription ; la première connexion mène
  directement au tableau de bord.
- **Offre payante** : l'abonnement est créé en attente de paiement (`PendingPayment`) ;
  la première connexion mène **directement à la page de paiement Stripe**, sans
  repasser par la sélection.
- **Aucune offre** (bouton « Commencer le diagnostic », accès direct à `/register`) :
  la première connexion mène à l'écran de sélection.

### Écran de sélection (`/abonnement`)

Hors de la coquille de l'application. `SubscriptionGate`, placé entre `ProtectedRoute`
et `AppShell`, y renvoie toute entreprise dont l'abonnement est absent ou en attente de
paiement, quel que soit l'écran demandé. En cas d'erreur de chargement de l'abonnement,
l'application reste accessible : ce sont les droits vérifiés par l'API qui font foi
(section 8), l'écran ne fait que les refléter.

Même tableau que la page d'accueil, avec un sélecteur mensuel / annuel. Actions :

- Starter → `POST /api/billing/starter`, puis tableau de bord ;
- Essential ou Professional → `POST /api/billing/checkout`, puis redirection vers la
  page de paiement Stripe ;
- Enterprise → « Bientôt disponible », aucune action ;
- offre en cours → « Offre actuelle » ;
- si un abonnement payant est en cours, toutes les autres colonnes proposent
  « Changer d'offre », qui ouvre le portail client Stripe : un changement d'offre
  modifie l'abonnement existant, il n'en facture jamais un second.

Retour d'un paiement annulé (`/abonnement?paiement=annule`) : l'écran de sélection
s'affiche avec un message, **sans** relancer automatiquement le paiement — sinon
l'utilisateur y serait renvoyé en boucle.

Seuls les `Admin` choisissent ou paient une offre (endpoints `[Authorize(Roles =
"Admin")]`) ; les autres membres voient les offres et un message les renvoyant vers
leur administrateur.

### Retour de Stripe (`/abonnement/confirmation?session_id=cs_…`)

Stripe renvoie le navigateur avec l'identifiant de la session de paiement. La page l'envoie à
`POST /api/billing/checkout/confirm`, qui **relit la session chez Stripe** et active l'offre
si elle est payée (section 5), puis ouvre le tableau de bord **sans aucune action de
l'utilisateur**. Le retour du navigateur, à lui seul, ne prouve rien : c'est la réponse de
Stripe qui fait foi.

Si la session n'est pas encore payée (authentification 3-D Secure en cours, par exemple) ou
si Stripe ne répond pas, la page se replie sur l'attente du webhook : elle interroge
`GET /api/billing/subscription` toutes les 2 s, 30 s au plus. Au-delà, elle indique que le
paiement a été transmis et qu'aucun second paiement n'est nécessaire, avec un bouton pour
vérifier de nouveau.

Le jeton d'accès vit en mémoire (`auth-securite-rgpd.md` section 2) et disparaît avec
la navigation vers Stripe : au retour, la session est restaurée par le refresh token.
Ce retour est une navigation depuis un autre site, mais l'appel de rafraîchissement part
ensuite de la page MAAT elle-même, donc le cookie `SameSite=Strict` est bien envoyé.

### Espace du compte

Bloc « Abonnement » dans `/compte`, relu chez Stripe à chaque affichage
(`POST /api/billing/refresh`) — c'est la page de retour du portail client, un changement
d'offre ou une résiliation qui vient d'y être faite s'affiche donc aussitôt. Il montre l'offre
et la période en cours, alerte si le dernier
paiement a échoué (`PastDue`), bouton « Gérer mon abonnement » (portail Stripe : factures,
moyen de paiement, changement d'offre, résiliation) dès qu'un client Stripe existe, et
« Voir les offres » tant que l'offre n'est pas payante.

## 3. Modèle

Une ligne `Subscription` par entreprise (`modele-donnees.md`), absente tant que
l'entreprise n'a rien choisi.

| Statut | Signification |
| --- | --- |
| `PendingPayment` | offre payante choisie, paiement pas encore reçu |
| `Active` | offre en vigueur (toujours le cas de Starter) |
| `PastDue` | échéance impayée, Stripe relance le paiement |

Pas de statut « résilié » : un abonnement payant qui se termine (résiliation, impayé
non régularisé) **ramène l'entreprise sur Starter**, qui reste actif à vie. Le client
Stripe est conservé pour regrouper les factures d'un éventuel nouvel abonnement.

Règles portées par l'entité (`Subscription`, Domain, testées sans base de données) :

- une offre payante choisie à l'inscription sans période est mensuelle ;
- revenir à Starter est refusé tant qu'un abonnement payant est en cours : il se
  résilie depuis le portail Stripe, sinon la facturation continuerait sans contrepartie
  visible ;
- un nouveau paiement est refusé (`409`) tant qu'un abonnement payant est en cours ;
- la fin d'un abonnement Stripe qui n'est plus l'abonnement courant de l'entreprise
  (webhook arrivé en retard) est ignorée.

## 4. Prestataire de paiement

`IPaymentGateway` (Application) décrit ce que MAAT demande au prestataire, sans rien de
propre à Stripe : ouvrir une page de paiement, ouvrir le portail client, lire un webhook,
résilier un abonnement. `StripePaymentGateway` (Infrastructure) est le seul fichier du
backend qui dépend du SDK `Stripe.net`.

**Stripe Checkout, page hébergée par Stripe** : aucune donnée de carte ne transite par
MAAT (conformité PCI DSS au niveau le plus léger, SAQ A). Le frontend n'a besoin d'aucune
bibliothèque Stripe : l'API renvoie une URL, le navigateur y est redirigé.

Les prix sont retrouvés par leur **clé de recherche** (lookup key) :
`essential_monthly`, `essential_yearly`, `professional_monthly`, `professional_yearly`.
Aucun identifiant `price_…` dans la configuration : les mêmes clés servent en test et en
production.

Chaque abonnement créé porte la métadonnée `maat_company_id` : c'est elle qui relie un
webhook à une entreprise. Stripe ne reçoit rien d'autre de MAAT que cet identifiant,
l'adresse e-mail de l'administrateur qui paie, et ce que l'utilisateur saisit lui-même
sur la page Stripe (adresse de facturation, numéro de TVA). **Aucune donnée de diagnostic**
(`auth-securite-rgpd.md`, section 6, « Sous-traitants »).

## 5. Synchronisation avec Stripe

**Règle : l'état d'une offre payante vient toujours de Stripe, relu côté serveur avec la clé
secrète, jamais de ce que le navigateur affirme.** Trois chemins l'appliquent, tous par
`SubscriptionSynchronizer` ; appliquer deux fois le même état ne change rien, donc ils
peuvent se recouper sans risque.

| Chemin | Déclencheur | Ce qui est relu chez Stripe |
| --- | --- | --- |
| Retour de paiement | `POST /api/billing/checkout/confirm` | la session Checkout, puis son abonnement |
| Retour du portail | `POST /api/billing/refresh` | l'abonnement enregistré de l'entreprise |
| Webhook | `POST /api/billing/webhook` | l'abonnement désigné par l'événement |

Les deux premiers donnent un résultat immédiat à l'utilisateur, **sans dépendre de la
livraison du webhook** (poste de développement sans `stripe listen`, webhook retardé). Le
webhook, lui, voit ce que le navigateur ne voit pas : renouvellements, impayés, résiliation
en fin de période, onglet fermé avant le retour de Stripe.

**Retour de paiement.** L'identifiant de session vient de l'URL, donc du client : tout ce qui
n'a pas la forme `cs_…` est écarté sans interroger Stripe ; une session inconnue ou pas encore
payée (`status` différent de `complete`) n'active rien ; une session payée par **une autre
entreprise** est refusée en `404`, sans rien modifier. Ouvert à tout membre de l'entreprise,
puisqu'il ne fait que relire un paiement déjà effectué.

**Webhook.** Anonyme, authentifié par la signature de l'en-tête `Stripe-Signature`
(HMAC-SHA256 du corps brut avec le secret de l'endpoint, horodatage de moins de 5 minutes
contre le rejeu). Signature invalide → `400`.

Événements traités : `checkout.session.completed` et `customer.subscription.created`,
`.updated`, `.deleted`, `.paused`, `.resumed`. Pour chacun, l'événement ne sert qu'à
savoir **quel** abonnement a changé : son état est **relu chez Stripe** au moment du
traitement. Deux raisons :

- les webhooks peuvent arriver en retard, en double ou dans le désordre : l'état relu
  est toujours le plus récent, et l'appliquer deux fois ne change rien ;
- le contenu d'un événement suit la version d'API de l'endpoint configurée dans le
  tableau de bord Stripe, pas celle du SDK : seul l'identifiant est lu dans le JSON brut.

Traduction des statuts Stripe : `active`, `trialing` → `Active` ; `past_due`, `unpaid`,
`paused` → `PastDue` ; `incomplete` → `PendingPayment` ; `canceled`,
`incomplete_expired` → retour à Starter. Statut inconnu, prix hors catalogue ou
abonnement sans `maat_company_id` : journalisé et ignoré.

Tout événement sans effet (autre type, entreprise supprimée entre-temps) est acquitté
en `200` : Stripe rejoue pendant trois jours tout webhook qui ne reçoit pas de `2xx`.
Une erreur de lecture chez Stripe, elle, produit une réponse d'erreur, pour que Stripe
rejoue l'événement.

Le webhook n'a pas d'utilisateur authentifié : il est traité par
`BillingWebhookHandler`, qui ne dépend pas d'`ICurrentUserContext` (ADR 0005), et
`ISubscriptionRepository` prend l'entreprise en paramètre explicite. Les endpoints
appelés par l'utilisateur lui passent toujours `currentUser.CompanyId`, jamais une
valeur reçue du client.

## 6. RGPD

- **Effacement** (`DELETE /api/me` par le dernier `Admin`) : l'abonnement Stripe payant
  est résilié immédiatement, **avant** la purge. Si Stripe refuse ou ne répond pas,
  rien n'est supprimé (`502`) : mieux vaut un compte à supprimer de nouveau qu'un
  prélèvement qui continue pour une entreprise disparue. La ligne `Subscription` part
  ensuite en cascade avec `Company`.
- Le client Stripe et ses factures ne sont pas supprimés : les données de facturation
  relèvent de l'obligation comptable de 10 ans (`auth-securite-rgpd.md`, section 6,
  « Conservation »).
- **Export** (`POST /api/me/export`) : offre, période, statut et dates de l'abonnement.
  Les identifiants Stripe, références techniques, n'y figurent pas ; les factures se
  téléchargent depuis le portail client.

## 7. Configuration

| Clé | Développement | Production |
| --- | --- | --- |
| `Stripe:SecretKey` | user-secrets (`sk_test_…`) | `STRIPE_SECRET_KEY` (`sk_live_…`) |
| `Stripe:WebhookSecret` | user-secrets (`whsec_…` affiché par `stripe listen`) | `STRIPE_WEBHOOK_SECRET` |
| `Stripe:ReturnBaseUrl` | `appsettings.Development.json` (`http://localhost:5173`) | `FRONTEND_ORIGIN` |

Clés absentes : l'application démarre, seuls le paiement et le portail répondent `503`
(même principe que le support sans jeton GitHub).

À préparer dans le tableau de bord Stripe, une fois en mode test puis une fois en
production :

1. deux produits (Essential, Professional), chacun avec un prix mensuel et un prix
   annuel récurrents, **taxe non incluse dans le prix**, portant les quatre clés de
   recherche de la section 4 ;
2. Stripe Tax activé, avec l'adresse d'origine de MAAT ;
3. le portail client : mise à jour du moyen de paiement, historique des factures,
   résiliation, changement d'offre entre les quatre prix ;
4. un endpoint de webhook vers `https://<API_DOMAIN>/api/billing/webhook` écoutant les
   événements de la section 5. En développement, la Stripe CLI en tient lieu ; ses
   versions récentes exigent la liste explicite des événements :

   ```bash
   stripe listen --events checkout.session.completed,customer.subscription.created,customer.subscription.updated,customer.subscription.deleted,customer.subscription.paused,customer.subscription.resumed --forward-to localhost:5130/api/billing/webhook
   ```

   Le secret `whsec_…` qu'elle affiche est stable d'un lancement à l'autre sur un même
   poste : il ne s'enregistre qu'une fois dans les user-secrets.

## 8. Limites par offre

### Offre effective

Les droits dépendent de l'**offre effective**, déduite de la ligne `Subscription` :

| Abonnement | Offre effective |
| --- | --- |
| absent, ou `PendingPayment` | Starter |
| `Active` | l'offre souscrite |
| `PastDue` | l'offre souscrite, avec un bandeau d'alerte dans la coquille |

`PastDue` garde les droits payants pendant les relances de Stripe : une carte expirée ne
doit pas couper l'accès du jour au lendemain. Si Stripe abandonne, le webhook de fin
d'abonnement ramène l'entreprise sur Starter (section 5), et les droits suivent sans
autre mécanisme. Enterprise, pas encore commercialisée, a les droits de Professional.

### Tableau des droits

| Droit | Starter | Essential | Professional |
| --- | --- | --- | --- |
| Diagnostics complétés | 1 au total | illimité | illimité |
| Score global et son historique | ✅ | ✅ | ✅ |
| Scores par domaine (radar, barres, détail) | ❌ | ✅ | ✅ |
| Recommandations visibles | 3 premières | 12 premières | toutes |
| Cocher une action (`is_completed`) | ❌ | ✅ | ✅ |
| Plan d'actions enrichi : modifier statut, responsable, échéance, notes | ❌ | ❌ | ✅ |
| Historique du suivi des actions (`recommandations.md`, section 4 bis) | ❌ | ❌ | ✅ |
| Base documentaire : lire les articles (sommaire et recherche ouverts à tous, `documentation.md`) | ❌ | ✅ | ✅ |
| Indicateurs RSE : saisie | ❌ | ❌ | ✅ |
| Benchmark sectoriel | ❌ | ❌ | ✅ |
| Support : ouvrir un ticket | ❌ | ✅ | ✅ |
| Rapport PDF | page de garde réduite | complet | complet |
| Logo de l'entreprise sur le rapport (`rapport-pdf.md`, section 7) | ❌ | ✅ | ✅ |

Ces droits s'ajoutent aux rôles, ils ne les remplacent pas : un `Viewer` reste en
lecture seule quelle que soit l'offre (`auth-securite-rgpd.md`).

**Diagnostics.** Seuls les diagnostics `Completed` comptent : un diagnostic abandonné
ne consomme pas l'unique évaluation du Starter. Le contrôle a lieu à la **création**
(`POST /api/diagnostics`) : dès qu'un diagnostic complété existe, quelle que soit
l'offre sous laquelle il l'a été, la création est refusée. Un diagnostic déjà en cours
au moment d'un retour à Starter peut être terminé.

**Recommandations.** Le contenu d'une recommandation est le même pour tous les
secteurs ; c'est son **rang** qui dépend du secteur (`recommandations.md`, section 2).
La limite retient donc les N premières par `priority_rank` (3, 12, puis toutes : un palier
par offre), et le comparatif parle de
recommandations « priorisées selon votre secteur », jamais « personnalisées ».
`GET /api/diagnostics/{id}/recommendations`, `GET .../action-plan` et le tableau de
bord ne renvoient que les recommandations visibles, avec le **nombre total** déclenché,
pour que l'écran annonce ce que l'offre supérieure débloquerait : en-tête
`X-Total-Count` sur les deux premiers (la réponse reste une liste, l'en-tête est exposé
par la politique CORS), champ `actionPlan.triggeredCount` sur le tableau de bord.
L'avancement (« 3 actions terminées sur 12 ») se calcule sur les recommandations
visibles, dans l'écran comme dans le rapport. Une écriture sur une recommandation
au-delà de la limite répond `404`, comme un code inconnu : ce que l'offre ne montre pas
n'existe pas pour l'appelant.

**Scores par domaine.** En Starter, le tableau de bord renvoie une liste
`domainScores` vide et `GET .../domain-scores/{domain}` est refusé ; le benchmark
(Professional) est `null` hors de son offre. Le score global reste visible partout, historique compris.

**Rapport PDF Starter.** Une page de garde réduite : identité de l'entreprise (raison
sociale, code NAF, effectif, région, date de complétion), score global sur sa jauge
avec son libellé qualitatif, puis le bloc Mentions, **obligatoire** quelle que soit
l'offre (`rapport-pdf.md`, section 4). Ni profil des domaines, ni cartes de synthèse,
qui en dérivent. L'offre effective fait partie des données d'entrée du document
(`rapport-pdf.md`, section 3).

**Retour à Starter.** Rien n'est effacé : diagnostics, suivi des actions et indicateurs
restent en base et s'affichent selon les droits du Starter. Tout réapparaît en cas de
nouvel abonnement.

### Mise en œuvre

**Une seule table de droits**, `PlanEntitlements` (Domain, service pur, sans I/O) :
à partir de l'offre et du statut, elle rend l'offre effective et ses droits. C'est le
seul endroit du code qui compare des offres ; les services Application la consultent,
aucun contrôleur ne teste une offre lui-même.

**Refus.** Une action hors de l'offre répond `403` avec un corps identifiable, pour
que l'écran distingue un manque de droit d'offre d'un manque de rôle :

```json
{ "code": "plan_required", "requiredPlan": "Essential" }
```

**Exposition au frontend.** `GET /api/billing/subscription` renvoie, en plus de
l'offre, l'offre effective et ses droits (`canViewDomainScores`,
`visibleRecommendations` (`null` : toutes), `canTrackActions`, `canEditActionPlan`,
`canEditIndicators`, `canViewBenchmark`, `canOpenSupportTickets`, `fullReport`,
`canCustomizeReportLogo`, `canViewActionHistory`, `canReadDocumentation`, et
`canStartDiagnostic`, calculé avec le nombre de diagnostics complétés). L'écran s'en
sert pour masquer, désactiver et proposer l'offre supérieure ; il ne décide jamais seul.
Les règles ne sont pas recopiées dans le frontend.

### Comparatif

Les fonctionnalités annoncées mais pas encore construites restent dans le comparatif
(`frontend/src/billing/plans.ts`) avec la mention « Bientôt » : générateur VSME,
préparation EcoVadis et B Corp, module CSRD, reporting
multi-référentiels, CDP et SFDR, alertes de conformité, collecte collaborative, API d'intégration, et toutes les options propres à Enterprise. Une fonctionnalité perd cette mention dans
le commit qui la livre.

---

## 9. Cas de test

Domain (`SubscriptionTests`) : les règles de la section 3.

Intégration (`BillingTests`, Stripe remplacé par `FakePaymentGateway`) :

1. Compte sans offre → statut nul.
2. Inscription avec Starter → `Active`.
3. Inscription avec une offre payante → `PendingPayment`, période transmise.
4. Inscription avec Enterprise → `400`, aucun compte créé.
5. Choix de Starter → `Active`.
6. Paiement : session ouverte pour l'entreprise du jeton, offre inchangée tant que le
   webhook n'est pas arrivé.
7. Paiement de Starter ou d'Enterprise → `400`.
8. Non-`Admin` → `403` sur le choix et le paiement, lecture autorisée.
9. Webhook à signature invalide → `400`.
10. Webhook de paiement reçu → offre active, portail accessible, second paiement et
    retour à Starter refusés (`409`).
11. Webhook de fin d'abonnement → retour à Starter.
12. Webhook pour une entreprise inconnue ou sans effet → `200`.
13. Portail sans abonnement payant → `409`.
14. Suppression du compte → abonnement Stripe résilié, entreprise et abonnement purgés.
15. Suppression du compte, résiliation impossible → `502`, rien n'est supprimé.
16. Retour de paiement, session payée → offre active sans webhook.
17. Retour de paiement, session inconnue → aucun changement ; session d'une autre
    entreprise → `404`, aucun changement.
18. Retour du portail après résiliation → Starter, sans webhook.

Adaptateur Stripe sans réseau (`StripePaymentGatewayTests`) : signature d'un autre
secret, corps modifié et signature trop ancienne refusés ; identifiant de session mal
formé écarté sans appel à Stripe ; identifiant d'abonnement lu
selon le type d'événement ; aller-retour des clés de recherche ; traduction des statuts.

Domain (`PlanEntitlementsTests`) : offre effective pour chaque statut (absent,
`PendingPayment`, `Active`, `PastDue`) ; droits de chaque offre, ligne par ligne du
tableau de la section 8 ; Enterprise aligné sur Professional.

Intégration (`PlanLimitsTests`, section 8) :

19. Starter avec un diagnostic complété → création refusée (`403`, `plan_required`) ;
    avec seulement un diagnostic abandonné → création acceptée.
20. Essential avec un diagnostic complété → création acceptée.
21. Starter : recommandations limitées aux 3 premières par `priority_rank`, nombre total
    renvoyé ; Essential : 12 premières ; Professional : toutes.
22. Essential : écriture sur une recommandation hors des 12 visibles → `404` ;
    Professional : acceptée.
23. Starter : cocher une action → `403` ; Essential → accepté.
24. Essential : modifier le plan d'actions enrichi ou saisir des indicateurs → `403` ;
    Professional → accepté.
25. Starter : scores par domaine absents du tableau de bord, `domain-scores` → `403`.
26. Benchmark absent en Starter et Essential, présent en Professional.
27. Starter : ouvrir un ticket de support → `403` ; lecture des tickets existants
    autorisée.
28. `PastDue` : droits de l'offre souscrite conservés.
29. Retour à Starter après résiliation : données conservées, affichées selon les droits
    du Starter ; nouvel abonnement → tout redevient visible.
30. `GET /api/billing/subscription` expose l'offre effective et ses droits, dont
    `canStartDiagnostic`.
31. Rapport Starter : page de garde réduite et Mentions présentes, aucun score de
    domaine ; même diagnostic, même offre → même document.

Frontend (Vitest) : éléments masqués ou désactivés selon les droits reçus, invitation à
l'offre supérieure, bandeau `PastDue`, mention « Bientôt » dans le comparatif.

E2E : un compte Starter qui a complété son diagnostic voit le lancement d'un nouveau
diagnostic bloqué avec l'invitation à passer à Essential, et seulement trois
recommandations.

Frontend (Vitest) : écran de sélection (Starter, paiement, Enterprise inerte, reprise
automatique du paiement, paiement annulé, portail, non-`Admin`), `SubscriptionGate`,
page de confirmation, inscription avec offre.

Frontend : la page de confirmation active via l'API sans attendre le webhook, et ne se
replie sur l'attente que si la session n'est pas payée ou si l'API échoue.

E2E (`abonnement.spec.ts`) : sélection de Starter après la première connexion ; Starter
choisi sur la page d'accueil qui saute la sélection ; offre payante qui mène à la page
Stripe au bon montant (seulement avec `STRIPE_E2E=1` et une clé de test côté API). Le test
s'arrête là : Stripe détecte les navigateurs automatisés sur sa page Checkout et y retient
le paiement. Le paiement lui-même se vérifie à la main, carte `4242 4242 4242 4242` —
c'est aussi le parcours de démonstration, qui doit aboutir au tableau de bord sans
`stripe listen`.
