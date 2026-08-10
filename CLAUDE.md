# MAAT — Plateforme SaaS de diagnostic RSE pour PME

Diagnostic RSE en moins de 30 min : questionnaire 45 questions → score pondéré
par secteur NAF → recommandations → tableau de bord → rapport PDF conforme VSME.

## Spécifications — source de vérité

Les fichiers sous `docs/specs/` font autorité sur le comportement attendu :
`modele-donnees.md`, `scoring.md`, `auth-securite-rgpd.md`, `questionnaire.md`,
`recommandations.md`, `dashboard.md`.

Le rapport de projet sous `docs/source/` est un livrable académique, pas un
contexte de travail. Il est rédigé au passé, comme si le produit existait déjà,
et il contredit les specs sur plusieurs points (nombre de domaines RSE, stockage
des PDF, bibliothèque de graphiques). Ne jamais le lire comme une référence
d'implémentation.

Quand le code diverge d'une spec, corriger la spec **dans le même commit** —
jamais la laisser dériver.

Les décisions d'architecture argumentées vivent dans `docs/adr/`.

## Stack (versions figées, ne pas mettre à jour sans discussion)

- Backend : .NET 10 (C#), Clean Architecture, EF Core 10, API REST
- Frontend : React 19, Vite, Tailwind CSS, Zustand, Recharts, React Router
- BDD : PostgreSQL 18
- PDF : QuestPDF + SkiaSharp (rendu **serveur** uniquement)
- Tests : xUnit (back), Vitest (front), Playwright (E2E)
- Lint frontend : oxlint

## Commandes

```bash
podman compose up -d db                    # PostgreSQL local
dotnet build backend/MAAT.slnx
dotnet test  backend/MAAT.slnx             # DOIT passer avant tout commit
dotnet run --project backend/MAAT.Api      # http://localhost:5130
cd frontend && npm run dev                 # http://localhost:5173
cd frontend && npm run build
cd frontend && npm run lint                # oxlint (frontend/.oxlintrc.json)
cd frontend && npm run test                # Vitest + jsdom + React Testing Library + vitest-axe, une passe
cd frontend && npm run test:watch          # idem, en mode watch
cd frontend && npm run test:e2e            # Playwright ; démarre npm run dev tout seul (playwright.config.ts)

# Migrations
dotnet ef migrations add <Nom> \
  --project backend/MAAT.Infrastructure --startup-project backend/MAAT.Api
dotnet ef database update \
  --project backend/MAAT.Infrastructure --startup-project backend/MAAT.Api

# Données de référence (questions, recommandations, pondérations sectorielles) : chargées
# depuis backend/MAAT.Infrastructure/Seed/*.csv, jamais depuis les migrations. Automatique
# au démarrage en Development ; commande explicite ailleurs (docs/specs/modele-donnees.md).
dotnet run --project backend/MAAT.Api -- seed

# Relit les CSV et signale les problèmes sans base de données et sans rien écrire — à
# lancer avant de proposer une modification d'un fichier sous backend/MAAT.Infrastructure/Seed/.
dotnet run --project backend/MAAT.Api -- seed --validate

# Jeu de démonstration (backend/MAAT.Infrastructure/Seed/demo/) : 3 entreprises fictives
# avec diagnostics complétés, nécessaire pour voir le tableau de bord rempli.
# Development UNIQUEMENT. Ne jamais l'exécuter en production, ne jamais le confondre
# avec les fichiers de référence.
dotnet run --project backend/MAAT.Api -- seed --demo
```

## Environnement de développement

Poste de dev : Fedora + Podman (pas Docker). Utiliser `podman compose`
dans les commandes et la documentation.

Volumes nommés par défaut (`postgres_data` dans docker-compose.yml), jamais de
bind mount : en Podman rootless, le mappage d'UID entre conteneur et hôte le
fait échouer, et le suffixe SELinux `:Z` ne corrige pas ce problème-là.
N'ajouter `:Z` que si un bind mount est réellement inévitable, et seulement
sur ce montage.

L'image postgres:18 attend le point de montage sur `/var/lib/postgresql`
(et non `/var/lib/postgresql/data` comme en 16/17). Vérifié empiriquement.

Port hôte de la base configurable via `POSTGRES_PORT` (défaut 5433).

Dépendances npm : `npm ci` uniquement, jamais `npm install`, et jamais
`npm update`. Toute nouvelle dépendance est ajoutée à une version figée,
proposée à l'utilisateur avant installation. Ne jamais relever une version
existante sans demande explicite.

### Tests d'intégration (Testcontainers + Podman rootless)

`MAAT.IntegrationTests` démarre un vrai conteneur `postgres:18` via
`Testcontainers.PostgreSql` (pas de provider InMemory : il ignore types,
longueurs et contraintes de colonnes, donc ne détecte pas les erreurs de
schéma). Chaque run applique la migration EF Core dans le conteneur avant les
tests.

Une fixture qui raccourcit `Jwt:AccessTokenLifetimeSeconds` ne sert qu'aux
tests d'expiration de jeton. Tout autre test doit utiliser une fixture
dédiée à durée de vie par défaut : sous charge, un enchaînement de bcrypt
dépasse la fenêtre et produit un 401 qui masque le comportement testé.

Prérequis, une fois par poste :

```bash
systemctl --user enable --now podman.socket
```

Variables d'environnement requises pour lancer `dotnet test` (Podman n'écoute
pas sur le socket Docker par défaut) :

```bash
export DOCKER_HOST="unix:///run/user/$(id -u)/podman/podman.sock"
export TESTCONTAINERS_RYUK_DISABLED=true   # ryuk (reaper) pose problème en rootless ; le fixture ferme le conteneur explicitement
```

## Règles d'architecture

- Les couches ne remontent jamais : Domain ← Application ← Infrastructure ← Api.
  Le Domain ne référence aucun paquet externe.
- Les entités EF ne sortent jamais d'Infrastructure. Les contrôleurs échangent des DTO.
- Le calcul du score RSE vit dans un service **pur** du Domain, sans I/O ni dépendance
  EF, pour rester testable unitairement. C'est le cœur du produit : toute modification
  de cette logique exige un test qui échoue d'abord.
- Dans `MAAT.Api/Program.cs`, ne jamais lire `builder.Configuration` au niveau du
  script avant `builder.Build()` : en test, `WebApplicationFactory` ne fusionne la
  configuration injectée par `WithWebHostBuilder` qu'après `Build()`, donc une
  lecture antérieure ne la verra jamais. Passer par
  `AddOptions<T>().Configure<IConfiguration>(...)`, ou par le delegate d'options
  du service concerné (ex. `AddJwtBearer`), résolus paresseusement au moment de
  l'utilisation plutôt qu'à l'enregistrement.
- Tests : Assert natif de xUnit et NSubstitute. Ne jamais introduire
  FluentAssertions — licence propriétaire Xceed depuis la v8, payante en usage
  commercial. `MAAT.Domain.Tests` ne référence ni EF Core ni `MAAT.Infrastructure`.
- Paquets : gestion centralisée via `backend/Directory.Packages.props`. Les
  `.csproj` ne portent jamais d'attribut `Version` sur un `PackageReference`.
- L'énumération des domaines RSE s'appelle `RseDomain`, pas `Domain` : collision
  avec le namespace `MAAT.Domain` (CS0118). Ne pas la renommer.

## Hébergement — contrainte non négociable

VPS OVHcloud (France) uniquement. La souveraineté des données est un argument
commercial du produit.

- **NE JAMAIS** introduire AWS S3, Render, Vercel, Netlify ou un hébergeur non-UE.
- Les PDF ne sont pas persistés : ils sont régénérés à la demande depuis le diagnostic.
  Ne stocker que les métadonnées (`generated_at`, `format`).
- Tout fichier servi passe par un endpoint authentifié, jamais par une URL publique.
- Le refresh token en `SameSite=Strict` exige que le frontend et l'API partagent le
  même domaine enregistrable en production (`app.maat.fr` et `api.maat.fr`
  conviennent ; `maat.fr` et `maat-api.io` non). Deux domaines distincts
  imposeraient `SameSite=None`, donc une révision complète de la stratégie de
  jetons — à trancher avant de réserver les noms de domaine.

## Sécurité

- Access token JWT : 15 min, gardé **en mémoire JS** côté client. Jamais dans localStorage.
- Refresh token : 7 jours, cookie `HttpOnly` + `Secure` + `SameSite=Strict`.
- Mots de passe : bcrypt. Jamais de secret en dur : `appsettings.json` local +
  variables d'environnement en production. Ne jamais committer de `.env`.

## Frontend

- Les couleurs, typographies et espacements viennent du skill `charte-maat`.
  Ne jamais inventer de valeur hexadécimale : si un token manque, le demander.
- Le linter est **oxlint**. Ne jamais introduire ESLint ni ses plugins : sa chaîne
  de dépendances tire `flat-cache` et `file-entry-cache`, compromis lors de
  l'attaque de chaîne d'approvisionnement npm du 4 août 2026.
- `recharts` requiert `react-is` à la **même version majeure que React**.
- État global du questionnaire : Zustand. Mémoïser les composants de question
  (`React.memo`) : 45 questions, les re-renders en cascade sont un problème connu.

## Git

- Branches : `feat/<module>`, `fix/<sujet>`. Jamais de commit direct sur `main`.
- Commits conventionnels : `feat(scoring): ...`, `fix(auth): ...`.
- Toujours proposer le diff avant de committer.

## Vérification

Ne déclare pas une tâche terminée sans avoir lancé `dotnet test` et
`npm run build`, et sans montrer la sortie. « Ça devrait marcher » n'est pas
une vérification.

## Contexte projet

Projet de fin d'études Master ESI, soutenance septembre 2026. Le code doit
pouvoir être expliqué et justifié à l'oral : privilégier une solution simple
et lisible à une solution astucieuse. Si un choix mérite d'être argumenté
devant un jury, l'expliquer en commentaire ou dans une ADR sous `docs/adr/`.
