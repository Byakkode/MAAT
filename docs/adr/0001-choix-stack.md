# 0001 — Choix de la stack technique

## Statut

Acceptée — 2026-07-26

## Contexte

MAAT est un projet de fin d'études (Master ESI, soutenance septembre 2026) :
plateforme SaaS de diagnostic RSE pour PME, avec vocation à être commercialisé.
Chaque choix de stack doit pouvoir être expliqué et justifié devant un jury.

## Décision

- **Backend : .NET 10 (C#), Clean Architecture, EF Core 10.**
  Framework long terme (support LTS), typage fort adapté à un calcul de score
  métier critique, écosystème mature pour l'authentification et l'accès aux
  données. Clean Architecture (Domain / Application / Infrastructure / Api)
  isole le calcul du score RSE — cœur du produit — de toute dépendance à
  l'EF Core ou à l'infrastructure, pour le rendre testable unitairement et
  facile à faire évoluer sans casser les couches supérieures.

- **Frontend : React 19 + Vite + TypeScript + Tailwind CSS.**
  React reste le choix le plus employable pour un jeune diplômé et dispose
  d'un écosystème de composants et de data-visualisation (Recharts) mature.
  Vite offre un temps de démarrage et de build nettement inférieur à
  Create React App ou Webpack. Tailwind permet d'imposer une charte
  graphique cohérente (`charte-maat`) sans dérive de valeurs ad-hoc.

- **Base de données : PostgreSQL 18.**
  SGBD relationnel open-source, robuste, avec un support natif du JSON
  (utile pour stocker les réponses du questionnaire) et un excellent
  support par EF Core via Npgsql. Alternative sérieuse à MySQL mais avec
  de meilleures garanties de conformité SQL et d'extensibilité.

- **Data visualisation : Recharts.**
  Bibliothèque de graphiques basée sur React et SVG, déclarative, avec un
  bon support de l'accessibilité (titres, alternatives textuelles) exigé
  par la charte graphique du produit.

- **Hébergement : VPS OVHcloud (France), aucun service tiers hors UE.**
  La souveraineté des données est un argument commercial du produit
  (PME françaises, diagnostic RSE). Un hébergeur européen soumis au droit
  français/européen renforce la crédibilité de ce positionnement et évite
  toute dépendance à un Cloud Act américain. Conséquence directe : aucun
  AWS S3, Render, Vercel ou Netlify dans l'architecture ; les PDF sont
  régénérés à la demande plutôt que stockés, pour limiter la surface de
  données personnelles hébergées.

## Conséquences

- Le calcul du score RSE (`Domain`) ne doit jamais référencer de paquet
  externe : toute tentation d'y injecter de l'EF Core ou du HTTP doit être
  refusée en revue de code.
- Toute introduction future d'un hébergeur ou service tiers hors UE
  nécessite une nouvelle ADR, car cela contredirait cette décision.
- Les versions de la stack sont figées (voir `CLAUDE.md`) ; toute montée
  de version majeure doit être discutée avant d'être appliquée.
