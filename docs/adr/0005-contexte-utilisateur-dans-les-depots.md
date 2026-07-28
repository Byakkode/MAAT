# 0005 — `ICurrentUserContext` injecté dans les dépôts à portée d'entreprise

## Statut

Acceptée — 2026-07-27

## Contexte

La section 4 de `docs/specs/auth-securite-rgpd.md` (cloisonnement par
entreprise) impose que toute requête portant sur `Diagnostic`, `Response`,
`DomainScore` ou `Report` soit filtrée par le `company_id` du principal
authentifié, **à la construction de la requête**, jamais après récupération.
Elle propose explicitement deux approches acceptables :

1. un filtre de requête global EF Core (`HasQueryFilter`), alimenté par le
   contexte de la requête HTTP ;
2. un dépôt à portée de requête recevant le `company_id` par injection.

La spec ajoute : « la seconde est plus verbeuse mais plus lisible en
soutenance — un filtre global est invisible dans le code appelant, ce qui est
précisément sa force et sa faiblesse. »

## Décision

L'option 2 est retenue. `ICurrentUserContext` (`MAAT.Application/Interfaces`)
expose `UserId`, `CompanyId` et `Role`, résolus depuis les claims du JWT via
`HttpCurrentUserContext` (`MAAT.Api/Security`), qui lit `IHttpContextAccessor`.
`DiagnosticRepository`, `ResponseRepository`, `DomainScoreRepository` et
`ReportRepository` (`MAAT.Infrastructure/Repositories`) reçoivent
`ICurrentUserContext` par injection au constructeur et filtrent par
`currentUser.CompanyId` dans le corps de chaque requête LINQ. Aucune méthode
de ces dépôts n'accepte de `company_id` en paramètre : il est structurellement
impossible d'en fournir un autre que celui du principal authentifié.

**Pourquoi pas un filtre global EF Core.** `HasQueryFilter` aurait éliminé le
risque d'oubli (le filtre s'applique à toute requête sur le `DbSet`, y
compris celles écrites par erreur sans y penser) et évité de répéter la
jointure vers `Diagnostic` dans `ResponseRepository`, `DomainScoreRepository`
et `ReportRepository`. Mais un filtre global est invisible au point d'appel :
un contrôleur qui lit `context.Diagnostics.Where(...)` ne laisse rien voir du
cloisonnement appliqué derrière lui. Pour un projet dont le code doit être
expliqué et justifié à l'oral (CLAUDE.md, section « Contexte projet »), le
compromis inverse — un filtre visible dans chaque méthode de dépôt, quitte à
le répéter quatre fois — est jugé préférable. C'est un choix de lisibilité
pédagogique assumé, pas un jugement que les filtres globaux seraient
incorrects en général.

## Conséquences

**Ces dépôts sont inutilisables hors d'une requête HTTP authentifiée.**
`HttpCurrentUserContext` lève `InvalidOperationException` si
`IHttpContextAccessor.HttpContext` est `null` ou si les claims `sub` /
`company_id` / `role` sont absentes. Concrètement : une tâche planifiée
(`RefreshTokenPurgeService` et tout futur `BackgroundService`), une commande
d'administration exécutée en CLI, ou un script de migration de données ne
peuvent pas injecter `IDiagnosticRepository` (ou les trois autres) — ils
plantent au premier accès à `currentUser.CompanyId`. `RefreshTokenPurgeService`
n'est pas concerné : `IRefreshTokenRepository` n'a jamais été scopé par
entreprise (les refresh tokens sont rattachés à un `user_id`, pas filtrés par
`company_id`), donc il ne dépend pas d'`ICurrentUserContext`. Le problème ne
s'est donc pas encore posé en pratique, mais il se posera dès qu'un traitement
hors requête devra lire ou écrire un `Diagnostic`, une `Response`, un
`DomainScore` ou un `Report` — par exemple un job nocturne de recalcul de
scores, ou une commande d'administration purgeant les diagnostics d'une
entreprise de démonstration.

**Porte de sortie prévue — à appliquer quand ce besoin apparaît, pas avant :**

- **Ne jamais** rendre `ICurrentUserContext.CompanyId` nullable, lui donner
  une valeur par défaut (`Guid.Empty`), ou faire en sorte que
  `HttpCurrentUserContext` renvoie silencieusement une valeur factice hors
  requête HTTP. Cela affaiblirait le cloisonnement pour les 100 % de sites
  d'appel qui, eux, sont bien des requêtes HTTP — un contournement ponctuel
  deviendrait une faiblesse permanente et invisible.
- **Ne jamais** injecter `MaatDbContext` directement dans un job ou une
  commande pour contourner les dépôts scopés. Rien ne l'empêche
  *techniquement* : `MAAT.Api` référence `MAAT.Infrastructure` en tant que
  projet, donc le type `MaatDbContext` y est visible. C'est une discipline de
  revue de code à faire respecter, pas une contrainte que le compilateur
  impose de lui-même.
- **Accès système à une seule entreprise connue** (ex. régénérer les scores
  d'une entreprise précise depuis une commande d'administration) : introduire
  une implémentation explicite d'`ICurrentUserContext` — par exemple
  `SystemCurrentUserContext(Guid companyId)` — construite avec un
  `company_id` réel et déjà légitimement connu de l'appelant, enregistrée
  dans un scope DI créé manuellement pour ce traitement. Les dépôts existants
  n'ont besoin d'aucune modification : ils continuent de filtrer par
  `currentUser.CompanyId`, qui est maintenant fourni par un contexte système
  explicite plutôt que par le JWT.
- **Accès système multi-entreprises** (ex. un job nocturne qui recalcule les
  scores de tous les diagnostics, toutes entreprises confondues) : ce n'est
  **pas** un cas d'usage d'`ICurrentUserContext`, qui porte un seul
  `company_id` par construction. Deux options légitimes : itérer entreprise
  par entreprise en construisant un `SystemCurrentUserContext` par itération
  (le traitement reste alors soumis au même filtre que n'importe quelle
  requête HTTP, une entreprise à la fois) ; ou, si cela n'a pas de sens
  métier de traiter une entreprise à la fois, exposer une méthode de dépôt
  distincte et explicitement nommée pour cet usage (ex.
  `IDiagnosticRepository.FindAllForSystemRecalculationAsync(...)`), review
  obligatoire, qui ne prend délibérément aucun `ICurrentUserContext` et le
  signale par son nom.
- Dans tous les cas, le signal doit être visible dans le code et dans la
  revue — jamais un `try/catch` qui avale l'exception d'`ICurrentUserContext`
  hors requête HTTP, jamais un flag de configuration qui désactive le
  filtrage « temporairement ».

Cette ADR ne crée pas encore `SystemCurrentUserContext` ni les méthodes
`*ForSystem*` : elles n'ont pas de consommateur aujourd'hui. Elle documente la
conception à suivre le jour où un premier traitement hors requête HTTP en aura
besoin, pour que ce jour-là ne soit pas l'occasion d'un contournement
ponctuel non tracé.
