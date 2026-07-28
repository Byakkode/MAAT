# MAAT — Plateforme SaaS de diagnostic RSE pour PME

Diagnostic RSE en moins de 30 min : questionnaire 45 questions → score pondéré
par secteur NAF → recommandations → tableau de bord → rapport PDF conforme VSME.

## Stack (versions figées, ne pas mettre à jour sans discussion)

- Backend : .NET 10 (C#), Clean Architecture, EF Core 10, API REST
- Frontend : React 19, Vite, Tailwind CSS, Zustand, Recharts
- BDD : PostgreSQL 18
- PDF : QuestPDF + SkiaSharp (rendu **serveur** uniquement)
- Tests : xUnit (back), Vitest (front), Playwright (E2E)

## Commandes

```bash
podman compose up -d db                    # PostgreSQL local
dotnet build backend/MAAT.slnx
dotnet test  backend/MAAT.slnx             # DOIT passer avant tout commit
dotnet run --project backend/MAAT.Api      # http://localhost:5130
cd frontend && npm run dev                 # http://localhost:5173
cd frontend && npm run build
cd frontend && npm run test                # Vitest + jsdom + React Testing Library + vitest-axe, une passe
cd frontend && npm run test:watch          # idem, en mode watch
cd frontend && npm run test:e2e            # Playwright ; démarre npm run dev tout seul (playwright.config.ts)

# Migrations
dotnet ef migrations add <Nom> \
  --project backend/MAAT.Infrastructure --startup-project backend/MAAT.Api
```

## Environnement de développement

Poste de dev : Fedora + Podman (pas Docker). Utiliser `podman compose`
dans les commandes et la documentation.

Volumes nommés par défaut (voir `postgres_data` dans docker-compose.yml), pas de
bind mount : un bind mount sur un répertoire hôte échoue en Podman rootless à
cause du mappage d'UID entre l'utilisateur du conteneur et l'utilisateur hôte —
un problème que le suffixe SELinux `:Z` ne corrige pas, puisqu'il relabellise
le contexte SELinux mais ne remappe aucun UID. N'ajouter `:Z` que si un bind
mount est réellement inévitable, et seulement sur ce montage-là.

L'image postgres:18 attend le point de montage sur `/var/lib/postgresql`
(et non `/var/lib/postgresql/data` comme en 16/17). Vérifié empiriquement.

Port hôte de la base configurable via `POSTGRES_PORT` (défaut 5433).

### Tests d'intégration (Testcontainers + Podman rootless)

`MAAT.IntegrationTests` démarre un vrai conteneur `postgres:18` via
`Testcontainers.PostgreSql` (pas de provider InMemory : il ignore types,
longueurs et contraintes de colonnes, donc ne détecte pas les erreurs de
schéma). Chaque run applique la migration EF Core dans le conteneur avant les
tests.

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

## Sécurité

- Access token JWT : 15 min, gardé **en mémoire JS** côté client. Jamais dans localStorage.
- Refresh token : 7 jours, cookie `HttpOnly` + `Secure` + `SameSite=Strict`.
- Mots de passe : bcrypt. Jamais de secret en dur : `appsettings.json` local +
  variables d'environnement en production. Ne jamais committer de `.env`.

## Frontend

- Les couleurs, typographies et espacements viennent du skill `charte-maat`.
  Ne jamais inventer de valeur hexadécimale : si un token manque, le demander.
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
