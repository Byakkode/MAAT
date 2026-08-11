# Spécification — Authentification, sécurité et RGPD

Périmètre : inscription, connexion, gestion des jetons, autorisation, cloisonnement
des données entre entreprises, et obligations RGPD portées par le code.

Ce module précède le questionnaire délibérément : le cloisonnement doit exister
avant qu'il y ait des données à cloisonner.

---

## 1. Inscription

`POST /api/auth/register`

Crée conjointement une `Company` et son premier `User`, qui reçoit le rôle `Admin`.

Entrées : email, mot de passe, nom de l'entreprise, code NAF, tranche d'effectif,
région. Le SIRET est optionnel à cette étape.

**Politique de mot de passe.** Longueur minimale 12 caractères. Aucune règle de
composition imposée — pas d'obligation de majuscule, chiffre ou caractère spécial.
Cette exigence est contraire aux recommandations actuelles de l'ANSSI et du NIST :
elle produit des mots de passe plus courts et plus prévisibles, et pousse à la
réutilisation.

En complément, rejeter les mots de passe compromis. L'API *Have I Been Pwned* en
mode k-anonymat n'envoie que les cinq premiers caractères du hash SHA-1 : aucune
donnée personnelle ne quitte le serveur, ce qui la rend compatible avec la
promesse de souveraineté du produit. Si l'équipe préfère éviter tout appel
externe, une liste locale des 100 000 mots de passe les plus courants est un
repli acceptable — le choix doit être tracé dans une ADR.

**Hachage.** bcrypt, facteur de coût 12. Étalonner sur le VPS cible : viser
environ 250 ms par hachage. Un coût trop faible affaiblit la protection, un coût
trop élevé transforme l'endpoint de connexion en vecteur de déni de service.

**Vérification d'adresse.** Un compte non vérifié peut se connecter mais ne peut
pas générer de rapport PDF. Le lien de vérification est un jeton à usage unique,
valable 24 heures, stocké haché.

Le fournisseur d'e-mails transactionnels doit être européen (Brevo, Scaleway TEM).
Un prestataire hors UE contredirait l'argument de souveraineté sur lequel repose
le positionnement du produit.

**Anti-énumération.** La réponse ne doit jamais révéler si une adresse est déjà
enregistrée. En cas de doublon, retourner la même réponse de succès et envoyer à
l'adresse concernée un message l'informant d'une tentative d'inscription.

---

## 2. Connexion

`POST /api/auth/login`

Retourne un access token dans le corps de la réponse, et pose le refresh token
en cookie.

| Jeton | Durée | Transport | Stockage client |
| --- | --- | --- | --- |
| Access | 15 min | Corps de réponse | Mémoire JS uniquement |
| Refresh | 7 jours | Cookie `HttpOnly` `Secure` `SameSite=Strict` | Inaccessible au JS |

L'access token ne va **jamais** dans `localStorage` ni `sessionStorage` : ces
emplacements sont lisibles par tout script injecté. Une perte de session au
rechargement de page est le comportement attendu — le refresh token la restaure
silencieusement.

**Claims du JWT** : `sub` (identifiant utilisateur), `company_id`, `role`, `exp`,
`iat`, `jti`. Rien d'autre. Pas d'email, pas de nom d'entreprise : un JWT est
signé, pas chiffré, et son contenu est lisible par quiconque l'intercepte.

Inclure `company_id` dans les claims évite une lecture en base à chaque requête.
Contrepartie assumée : un changement d'entreprise met jusqu'à 15 minutes à se
propager. Acceptable compte tenu de la durée de vie du jeton, mais à documenter.

`GET /api/auth/me` retourne ces mêmes identifiants (lus depuis les claims), plus
`emailVerified`, lui relu en base à chaque appel — jamais un claim, pour la
raison énoncée ci-dessus : un JWT est lisible par quiconque l'intercepte, et
l'état de vérification n'a pas besoin d'y figurer. Ajouté pour que le frontend
puisse désactiver le bouton de téléchargement du rapport avant même la
première tentative (`rapport-pdf.md`, section 6), plutôt que de le découvrir
au premier 403.

**Réponse en cas d'échec.** Message générique et identique, que l'adresse soit
inconnue ou le mot de passe erroné. Exécuter systématiquement la vérification
bcrypt, y compris lorsque l'utilisateur n'existe pas, contre un hash factice : sans
cela, l'écart de temps de réponse révèle l'existence du compte.

**Limitation de débit.** Middleware natif de rate limiting d'ASP.NET Core, fenêtre
fixe : 5 tentatives par tranche de 15 minutes, par couple adresse IP + email.
Réponse `429` au-delà. La limitation par IP seule est contournable, la limitation
par email seule permet le déni de service ciblé.

---

## 3. Rotation et détection de réutilisation

`POST /api/auth/refresh`

À chaque rafraîchissement : émettre un nouveau couple de jetons, marquer l'ancien
refresh token comme révoqué (`revoked_at`), ne jamais le supprimer.

**Détection de réutilisation.** Si un refresh token déjà révoqué est présenté,
c'est qu'il a été volé — soit par l'attaquant, soit par l'utilisateur légitime,
et il est impossible de savoir lequel présente le jeton. Révoquer alors **toute la
famille** de jetons de cet utilisateur et forcer une reconnexion. C'est le
comportement recommandé par l'OWASP et la seule raison pour laquelle on conserve
les jetons révoqués en base.

`POST /api/auth/logout` révoque le refresh token courant et efface le cookie.

Une tâche de purge supprime les jetons expirés depuis plus de 30 jours.

---

## 4. Autorisation

### Rôles

| Rôle | Diagnostics | Questionnaire | Utilisateurs | Facturation |
| --- | --- | --- | --- | --- |
| `Admin` | lecture, création, suppression | répondre | inviter, révoquer | gérer |
| `User` | lecture, création | répondre | — | — |
| `Viewer` | lecture | — | — | — |

**`POST /api/users/invite` — ne pas exposer en production.** L'implémentation
actuelle (`UsersController.Invite` / `UserService.InviteAsync`) applique
correctement la restriction de rôle (`Admin` seul, 403 sinon) et le
cloisonnement (le compte créé appartient à l'entreprise du principal, jamais à
une entreprise fournie par le client), mais crée le compte invité avec un hash
de mot de passe aléatoire inutilisable, sans aucun flux permettant à l'invité
de définir son propre mot de passe. Le compte créé est donc inutilisable par
son destinataire tel quel. Cet endpoint ne doit pas être exposé en production
tant que ce flux d'acceptation d'invitation n'est pas implémenté — il n'a été
construit que pour vérifier le contrôle de rôle du cas de test 17.

### Cloisonnement par entreprise — section critique

**Toute** requête portant sur `Diagnostic`, `Response`, `DomainScore`,
`DiagnosticRecommendation` ou `Report` est filtrée par le `company_id` du principal
authentifié. Sans exception.

Le contrôle ne peut pas reposer sur le seul identifiant fourni par le client.
Récupérer un diagnostic par son `id` puis vérifier son appartenance après coup
fonctionne, mais la première ligne oubliée crée la faille. Le filtrage doit être
appliqué **à la construction de la requête**, pas après.

Deux approches acceptables : un filtre de requête global EF Core alimenté par le
contexte de la requête HTTP, ou un dépôt à portée de requête recevant le
`company_id` par injection. La seconde est plus verbeuse mais plus lisible en
soutenance — un filtre global est invisible dans le code appelant, ce qui est
précisément sa force et sa faiblesse.

Un accès à une ressource d'une autre entreprise retourne **`404`**, jamais `403` :
un `403` confirme l'existence de la ressource et permet d'énumérer les
identifiants des concurrents.

---

## 5. Durcissement HTTP

**CORS** : liste blanche explicite d'origines, `AllowCredentials` activé — le
cookie de refresh en dépend. Jamais de joker `*`, incompatible avec les
identifiants de toute façon.

**En-têtes** : `Strict-Transport-Security` (avec `includeSubDomains`),
`X-Content-Type-Options: nosniff`, `Content-Security-Policy` avec
`frame-ancestors 'none'`, `Referrer-Policy: strict-origin-when-cross-origin`.

**Secrets** : jamais dans le dépôt. `appsettings.Development.json` en local,
variables d'environnement en production. La clé de signature JWT fait au minimum
32 octets aléatoires et est distincte entre environnements.

**Journalisation** : aucune donnée personnelle, aucun jeton, aucun mot de passe,
aucun corps de requête d'authentification. Les tentatives de connexion échouées
sont journalisées avec l'adresse IP et l'horodatage, sans l'adresse email testée.
Même règle pour les échecs de reconfirmation de mot de passe sur `POST
/api/me/export` et `DELETE /api/me` (section 6) : IP et horodatage, jamais le
mot de passe testé.

**Un mot de passe ne transite jamais par un en-tête ni une query string.**
Contrairement au corps d'une requête, les en-têtes et les paramètres d'URL sont
couramment journalisés par les reverse proxies, CDN et load balancers — hors du
contrôle du code applicatif. Toute reconfirmation de mot de passe (section 6)
passe donc par le corps JSON, jamais par un en-tête dédié ni un paramètre de
requête, même sur un endpoint en lecture seule.

---

## 6. RGPD

### Bases légales et minimisation

Le traitement repose sur l'exécution du contrat (art. 6.1.b). Les données
collectées se limitent au strict nécessaire : identité professionnelle, données
d'entreprise, réponses au diagnostic. Aucune catégorie particulière au sens de
l'article 9 n'est collectée — et le libellé des 45 questions doit être rédigé pour
qu'il en reste ainsi.

Point de vigilance : les questions du domaine Social peuvent facilement dériver
vers des données de santé ou d'appartenance syndicale. Elles doivent porter sur
l'existence de dispositifs, jamais sur des situations individuelles.

### Droits des personnes

`POST /api/me/export` — droit d'accès et portabilité (art. 15 et 20). Export JSON
structuré de l'intégralité des données du compte et de son entreprise : `User`,
`Company`, `Diagnostic`, `Response`, `DomainScore`, `DiagnosticRecommendation`
et `Report`. `DiagnosticRecommendation` doit y figurer avec ses champs
`is_completed` et `completed_at` : ce sont des données saisies par l'utilisateur
(cases à cocher du tableau de bord), pas des valeurs dérivées recalculables — les
omettre rendrait l'export incomplet au regard des art. 15 et 20, contrairement
par exemple à `Response.score_contribution` (délibérément absent du modèle,
voir `modele-donnees.md`), qui n'a jamais existé nulle part parce qu'il est
recalculable à tout moment.

En `POST`, pas en `GET` : la reconfirmation de mot de passe (voir plus bas) doit
passer par le corps de la requête, qu'un `GET` ne porte pas de façon fiable côté
navigateur.

`DELETE /api/me` — droit à l'effacement (art. 17). Purge en cascade de `User`,
`RefreshToken`, `EmailVerificationToken`, `Company`, `Diagnostic`, `Response`,
`DomainScore`, `DiagnosticRecommendation` et `Report`. Suppression réelle, pas
de suppression logique : un enregistrement marqué supprimé reste une donnée
conservée.

Les deux opérations exigent une reconfirmation du mot de passe, transmise dans
le corps JSON de la requête (jamais en en-tête ni en query string — section 5).

**Limitation de débit.** Ces deux endpoints acceptent un mot de passe en clair
et le vérifient : sans limitation, ils constituent un oracle de mot de passe
hors du chemin `/api/auth/login`, avec ni les protections de la section 2 ni sa
journalisation. Même mécanisme que la limitation de la connexion (middleware
natif de rate limiting d'ASP.NET Core, fenêtre fixe, 5 tentatives par tranche de
15 minutes, réponse `429` au-delà), mais partitionné par utilisateur authentifié
plutôt que par IP + email : l'identité est déjà connue via le JWT, il n'y a rien
à énumérer. Les deux endpoints partagent le même compteur par utilisateur, pas
un quota séparé chacun — sans quoi alterner entre eux doublerait le nombre de
mots de passe testables.

### Conservation

| Donnée | Durée |
| --- | --- |
| Compte actif | durée de la relation contractuelle |
| Compte inactif | 3 ans après le dernier accès, puis suppression après relance |
| Jetons révoqués | 30 jours |
| Journaux de connexion | 12 mois |
| Données de facturation | 10 ans (obligation comptable) |

Ces durées doivent figurer au registre des traitements et dans la politique de
confidentialité. La suppression des comptes inactifs est une tâche planifiée, pas
une intention.

### Sous-traitants

OVHcloud (hébergement, France) et Stripe (paiement, États-Unis, clauses
contractuelles types). Un contrat de sous-traitance conforme à l'article 28 est
requis pour chacun. Aucune donnée de diagnostic ne transite par Stripe : seules
les données de facturation sont concernées, et cette séparation doit être
vérifiable dans le code.

### Anonymat du benchmark

Le calcul de position sectorielle n'expose jamais un score individuel et n'est
affiché qu'à partir de 5 entreprises dans le secteur. En deçà, les scores
redeviennent ré-identifiables par recoupement.

Cette règle est une exigence RGPD d'anonymisation, pas une simple préférence
d'affichage — elle doit être vérifiée par le code, pas seulement respectée par
convention dans le frontend. Sa vérification revient au module tableau de bord,
où le calcul de benchmark sera implémenté (aucun code de benchmark n'existe à
ce jour) ; elle ne figure donc pas parmi les cas de test de ce module-ci.

### Violation de données

Procédure de notification à la CNIL sous 72 heures documentée dans
`docs/securite/procedure-violation.md`. Une procédure écrite avant l'incident vaut
mieux qu'une improvisation pendant.

---

## Ce qu'il ne faut pas faire

**Ne pas implémenter la cryptographie soi-même.** bcrypt via une bibliothèque
éprouvée, JWT via `Microsoft.AspNetCore.Authentication.JwtBearer`. Aucun hachage,
aucune signature, aucune comparaison de secret écrits à la main.

**Ne pas comparer les jetons en clair avec `==`.** Une comparaison caractère par
caractère sur le secret en clair fuit une information temporelle exploitable. La
parade recommandée n'est pas nécessairement une comparaison à temps constant :
hacher le jeton présenté (SHA-256 suffit, l'entropie vient du jeton, pas de
l'algorithme) et rechercher ce hash en base via un index revient au même
résultat — la comparaison porte sur un condensé imprévisible, pas sur le secret,
et le canal temporel n'est plus exploitable. C'est le choix retenu pour les
refresh tokens et les jetons de vérification d'e-mail (voir
`TokenHasher`/ADR 0004).

**Ne pas faire confiance au `company_id` transmis par le client**, sous quelque
forme que ce soit — corps de requête, paramètre d'URL, en-tête. La seule source
autorisée est le principal authentifié.

**Ne pas ajouter d'authentification multifacteur au MVP.** C'est une bonne idée,
mais hors périmètre, et une implémentation bâclée est pire que son absence. À
inscrire à la feuille de route.

---

## Cas de test

Tests d'intégration sous `MAAT.IntegrationTests`, contre PostgreSQL réel.

**Authentification**

1. Inscription valide → 201, `Company` et `User` créés, rôle `Admin`.
2. Inscription avec adresse existante → réponse identique au succès, aucun compte créé.
3. Mot de passe de moins de 12 caractères → 400.
4. Mot de passe figurant dans la liste des compromis → 400.
5. Connexion valide → access token en corps, cookie refresh présent avec `HttpOnly`, `Secure`, `SameSite=Strict`.
6. Connexion avec adresse inconnue et connexion avec mot de passe erroné → réponses strictement identiques.
7. Sixième tentative en moins de 15 minutes → 429.

**Jetons**

8. Access token expiré → 401.
9. Refresh valide → nouveau couple émis, ancien jeton marqué révoqué.
10. Refresh déjà révoqué présenté à nouveau → toute la famille est révoquée, 401.
11. JWT à signature altérée → 401.
12. JWT ne contenant ni email ni nom d'entreprise — vérification du contenu des claims.

**Cloisonnement — les plus importants**

13. Utilisateur de l'entreprise A demandant un diagnostic de l'entreprise B par son identifiant → **404**.
14. Même scénario sur `Response`, `DomainScore`, `Report` → 404.
15. Requête portant un `company_id` falsifié dans le corps → ignoré, seul le principal fait foi.
16. `Viewer` tentant de créer un diagnostic → 403.
17. `User` tentant d'inviter un utilisateur → 403.

**RGPD**

18. Export → contient l'intégralité des données du compte, dans un format exploitable.
19. Suppression de compte → aucune ligne résiduelle dans les neuf tables concernées.
20. Suppression de compte → les tables de référence restent intactes.

Les cas 13 à 15 sont ceux à montrer en soutenance. Ce sont eux qui prouvent que
la confidentialité vendue par le produit est vérifiée par le code, et non promise
par une phrase de rapport.
