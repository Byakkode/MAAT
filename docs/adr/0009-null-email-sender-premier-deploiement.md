# 0009 — NullEmailSender pour le premier déploiement en Production

## Statut

Acceptée — 2026-08-17

## Contexte

Le garde-fou de `Program.cs` (ADR 0004, docs/specs/auth-securite-rgpd.md section 5)
fait volontairement échouer le démarrage hors Development si aucun `IEmailSender`
réel n'est enregistré : `LoggingEmailSender` journalise l'adresse e-mail de
l'utilisateur, une donnée personnelle, et n'a donc de sens qu'en développement local.

À ce stade du projet, aucun fournisseur transactionnel européen (Brevo, Scaleway TEM
— CLAUDE.md) n'est encore provisionné : ni compte créé, ni clé API, ni domaine
d'envoi validé (SPF/DKIM). Le VPS de production ne peut donc pas démarrer du tout,
ce qui bloque tout le reste du déploiement (`docs/deploiement/checklist.md`) — y
compris les vérifications qui n'ont rien à voir avec l'e-mail (santé de l'API, CORS,
en-têtes de durcissement, etc.).

## Décision

### NullEmailSender, activé uniquement par `Email__Provider=none`

`NullEmailSender` implémente `IEmailSender` sans effectuer d'envoi : il journalise
uniquement le fait qu'un envoi a été demandé et son type (vérification d'adresse,
notification de double inscription), jamais l'adresse e-mail ni le jeton — la même
contrainte de journalisation que `LoggingEmailSender` était censée respecter mais ne
respecte pas, ce qui la réserve à Development.

Il ne s'enregistre que si la variable `Email__Provider` vaut exactement `none`,
et seulement hors Development (en Development, `LoggingEmailSender` reste le seul
enregistré). L'absence de configuration continue de faire échouer le démarrage :
`Email__Provider=none` est un choix explicite et journalisé au déploiement, jamais
un état par défaut qu'on obtiendrait en oubliant de configurer un fournisseur.

### Ce que cette décision n'est pas

Ce n'est pas un contournement du garde-fou : le garde-fou vérifie qu'un
`IEmailSender` est *délibérément* choisi hors Development, pas qu'il envoie
réellement des e-mails. `NullEmailSender` satisfait cette exigence au sens littéral
tout en assumant, explicitement et par écrit, qu'aucun e-mail ne part.

### Conséquence assumée : vérification d'adresse inopérante

Avec `NullEmailSender`, l'e-mail de vérification n'est jamais envoyé. Un compte créé
dans cet état reste non vérifié indéfiniment : il peut se connecter mais ne peut pas
générer de rapport PDF (docs/specs/modele-donnees.md, section citée en ligne 97 ;
docs/specs/coquille-et-compte.md ligne 99) — le livrable central du produit reste
donc inaccessible tant qu'un fournisseur réel n'est pas configuré. C'est un état de
déploiement transitoire assumé, pas un correctif silencieux : la documentation
utilisateur et la checklist de déploiement doivent le signaler explicitement plutôt
que de laisser un utilisateur pilote découvrir qu'il ne peut jamais recevoir son
rapport.

## Conséquences

- `docs/specs/auth-securite-rgpd.md`, section 5, documente `NullEmailSender` et son
  activation par `Email__Provider=none`.
- `docker-compose.prod.yml` mappe `Email__Provider: ${EMAIL_PROVIDER}` sur le service
  `api` ; `.env.production.example` documente `EMAIL_PROVIDER` avec `none` comme
  valeur de repli explicite, à remplacer dès qu'un fournisseur réel est provisionné.
- `docs/deploiement/checklist.md`, section 5, mentionne cette option et rappelle sa
  conséquence (vérification d'adresse inopérante) plutôt que de présenter le
  fournisseur transactionnel comme la seule voie de démarrage.
- Quand un fournisseur réel (Brevo ou Scaleway TEM) est intégré, `Email__Provider`
  bascule sur sa valeur (ex. `brevo`), `NullEmailSender` cesse d'être sélectionné, et
  cette ADR peut être marquée Historique — sans qu'aucun changement de code ne soit
  requis dans `Program.cs` au-delà d'ajouter la branche du nouveau fournisseur.
