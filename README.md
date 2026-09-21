# MAAT — Diagnostic RSE pour PME

Plateforme SaaS qui guide une PME à travers un diagnostic de Responsabilité
Sociétale des Entreprises (RSE) en moins de 30 minutes. Elle produit un score
pondéré par secteur d'activité NAF, des recommandations priorisées, un plan
d'actions, des indicateurs quantitatifs et un rapport PDF conforme au standard
VSME.

> Projet de fin d'études — Master ESI, soutenance septembre 2026.

---

## Fonctionnalités

| Module | Description |
|---|---|
| **Questionnaire** | 45 questions réparties sur 5 domaines RSE, auto-sauvegarde, reprise possible |
| **Score RSE** | Formule pondérée par domaine et par secteur NAF (150 jeux de pondérations) |
| **Recommandations** | Sélection et priorisation automatique selon le score et les réponses |
| **Tableau de bord** | Radar, historique, comparatif sectoriel, plan d'actions résumé |
| **Plan d'actions** | Suivi par action : 4 états, responsable, échéance, notes auto-sauvegardées |
| **Indicateurs** | Saisie d'indicateurs quantitatifs RSE par année, comparaison N-1 |
| **Rapport PDF** | Génération à la demande, déterministe, rendu radar serveur (QuestPDF) |
| **Support** | Tickets intégrés via GitHub Issues, suivi de résolution |
| **Compte** | Export RGPD, changement de mot de passe, suppression de compte |

---

## Stack technique

| Couche | Technologie |
|---|---|
| Backend | .NET 10 (C#), Clean Architecture, EF Core 10 |
| Frontend | React 19, Vite, TypeScript, Tailwind CSS, Zustand, Recharts |
| Base de données | PostgreSQL 18 |
| Génération PDF | QuestPDF + SkiaSharp (rendu serveur uniquement) |
| Tests | xUnit + Testcontainers (backend), Vitest + RTL (frontend), Playwright (E2E) |
| Linter | oxlint (frontend) |
| Hébergement cible | VPS OVHcloud — France uniquement (souveraineté des données) |

Les décisions d'architecture argumentées sont dans [`docs/adr/`](docs/adr/).

---

## Architecture backend

```
MAAT.Domain          — entités, énumérations, services purs (ScoringService)
MAAT.Application     — cas d'usage, DTOs, interfaces de dépôt
MAAT.Infrastructure  — EF Core, migrations, PDF, email, GitHub, jobs, seed
MAAT.Api             — contrôleurs REST, middleware, configuration
```

La dépendance ne remonte jamais : `Domain ← Application ← Infrastructure ← Api`.
Le `Domain` ne référence aucun paquet externe.

Le `ScoringService` — cœur du produit — est un service **pur** : aucune I/O,
aucun accès base. Il reçoit des réponses et une pondération sectorielle, et
retourne un score global + le détail par domaine. Deux entreprises avec des
réponses identiques mais des codes NAF différents obtiennent des scores
différents parce que la pondération sectorielle diffère.
Voir [`docs/adr/0003-algorithme-score-rse.md`](docs/adr/0003-algorithme-score-rse.md)
et [`docs/specs/scoring.md`](docs/specs/scoring.md).

---

## Prérequis

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- [Node.js ≥ 22](https://nodejs.org/) avec npm
- [Podman](https://podman.io/) (ou Docker — remplacer `podman` par `docker` dans les commandes)

---

## Démarrage rapide

```bash
# 1. Base de données PostgreSQL
cp .env.example .env          # renseigner POSTGRES_USER, POSTGRES_PASSWORD, POSTGRES_DB
podman compose up -d db

# 2. Migrations et données de référence
dotnet ef database update \
  --project backend/MAAT.Infrastructure --startup-project backend/MAAT.Api
dotnet run --project backend/MAAT.Api -- seed

# (optionnel) Données de démonstration — Development uniquement
dotnet run --project backend/MAAT.Api -- seed --demo

# 3. API
dotnet run --project backend/MAAT.Api
# → http://localhost:5130

# 4. Frontend
cd frontend && npm ci && npm run dev
# → http://localhost:5173
```

---

## Variables d'environnement

| Variable | Exemple | Obligatoire |
|---|---|---|
| `POSTGRES_USER` | `maat` | Oui |
| `POSTGRES_PASSWORD` | `secret` | Oui |
| `POSTGRES_DB` | `maat` | Oui |
| `POSTGRES_PORT` | `5433` | Non (défaut : 5433) |
| `Jwt__Secret` | chaîne 32+ caractères | Oui |
| `GitHub__Token` | `ghp_…` | Non (module Support) |

En développement, les secrets JWT et GitHub passent par les
[User Secrets .NET](https://learn.microsoft.com/fr-fr/aspnet/core/security/app-secrets).
Ne jamais committer de `.env` ni de secrets dans `appsettings.json`.

---

## Tests

```bash
# Backend — unitaires + intégration (Testcontainers, requiert Podman rootless)
export DOCKER_HOST="unix:///run/user/$(id -u)/podman/podman.sock"
export TESTCONTAINERS_RYUK_DISABLED=true
dotnet test backend/MAAT.slnx

# Frontend — composants (Vitest + jsdom)
cd frontend && npm run test

# E2E — navigateur réel (Playwright, démarre le frontend automatiquement)
cd frontend && npm run test:e2e
```

> Les quatre suites (`dotnet test`, `npm run build`, `npm run test`,
> `npm run test:e2e`) doivent passer avant tout commit.

Pour les tests d'intégration backend, activer le socket Podman une fois par poste :

```bash
systemctl --user enable --now podman.socket
```

---

## Données de référence

Les questions, recommandations et pondérations sectorielles sont chargées
depuis des fichiers CSV sous `backend/MAAT.Infrastructure/Seed/`, jamais via
les migrations EF. L'opération est idempotente (upsert).

```bash
# Valider les CSV sans base de données
dotnet run --project backend/MAAT.Api -- seed --validate
```

---

## Migrations EF Core

```bash
dotnet ef migrations add <Nom> \
  --project backend/MAAT.Infrastructure --startup-project backend/MAAT.Api

dotnet ef database update \
  --project backend/MAAT.Infrastructure --startup-project backend/MAAT.Api
```

---

## Spécifications

Les fichiers sous [`docs/specs/`](docs/specs/) font autorité sur le
comportement attendu. Le rapport académique sous `docs/source/` est un
livrable de soutenance rédigé au passé — ne pas l'utiliser comme référence
d'implémentation.

| Spec | Sujet |
|---|---|
| [`scoring.md`](docs/specs/scoring.md) | Formule et 11 cas de test du moteur de score |
| [`questionnaire.md`](docs/specs/questionnaire.md) | Cycle de vie du diagnostic |
| [`recommandations.md`](docs/specs/recommandations.md) | Déclencheurs et priorisation |
| [`dashboard.md`](docs/specs/dashboard.md) | Données et structure du tableau de bord |
| [`rapport-pdf.md`](docs/specs/rapport-pdf.md) | Génération et déterminisme du PDF |
| [`auth-securite-rgpd.md`](docs/specs/auth-securite-rgpd.md) | Auth JWT, sécurité HTTP, RGPD |
| [`modele-donnees.md`](docs/specs/modele-donnees.md) | Entités, relations, format CSV |

---

## Sécurité

- Access token JWT 15 min — gardé **en mémoire JS**, jamais dans `localStorage`.
- Refresh token 7 jours — cookie `HttpOnly` + `Secure` + `SameSite=Strict`.
- Mots de passe hachés avec bcrypt.
- Vérification d'email obligatoire à l'inscription.
