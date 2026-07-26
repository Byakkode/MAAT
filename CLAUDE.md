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
docker compose up -d db                    # PostgreSQL local
dotnet build backend/MAAT.sln
dotnet test  backend/MAAT.sln              # DOIT passer avant tout commit
dotnet run --project backend/MAAT.Api      # http://localhost:5000
cd frontend && npm run dev                 # http://localhost:5173
cd frontend && npm run build && npm test

# Migrations
dotnet ef migrations add <Nom> \
  --project backend/MAAT.Infrastructure --startup-project backend/MAAT.Api
```

## Environnement de développement

Poste de dev : Fedora + Podman (pas Docker). Utiliser `podman compose`
dans les commandes et la documentation.

SELinux est en mode enforcing : tout montage de volume dans
docker-compose.yml DOIT porter le suffixe `:Z`, sinon le conteneur
échoue avec « permission denied ».


## Règles d'architecture

- Les couches ne remontent jamais : Domain ← Application ← Infrastructure ← Api.
  Le Domain ne référence aucun paquet externe.
- Les entités EF ne sortent jamais d'Infrastructure. Les contrôleurs échangent des DTO.
- Le calcul du score RSE vit dans un service **pur** du Domain, sans I/O ni dépendance
  EF, pour rester testable unitairement. C'est le cœur du produit : toute modification
  de cette logique exige un test qui échoue d'abord.

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
