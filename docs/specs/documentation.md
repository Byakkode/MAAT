# Spécification — Base documentaire RSE

Périmètre : la base de connaissances accessible par l'entrée « Documentation » de la
navigation — articles, rubriques, niveaux de lecture, recherche.

Deux publics : le **dirigeant de PME** qui découvre la RSE et doit savoir, en quelques
minutes, ce qui le concerne ; le **professionnel RSE** qui cherche la précision
réglementaire ou méthodologique. La base est indépendante du plan d'actions : elle ne
dépend d'aucun diagnostic et s'adresse à tout utilisateur connecté.

Dépendances : `abonnement.md` (section 8, droit de lecture), `coquille-et-compte.md`
(navigation), skill `charte-maat`.

---

## 1. Rubriques et niveaux

| Rubrique (`KnowledgeCategory`) | Libellé |
| --- | --- |
| `GettingStarted` | Premiers pas |
| `Regulation` | Réglementation |
| `Environment` | Environnement |
| `Social` | Social |
| `BusinessEthics` | Éthique des affaires |
| `Procurement` | Achats responsables |
| `Governance` | Gouvernance et stratégie |
| `Standards` | Référentiels et labels |
| `Funding` | Financer sa démarche |

L'ordre de l'énumération est l'ordre d'affichage, du plus accessible au plus spécialisé.

Niveaux (`KnowledgeLevel`) : `Essentials` (« L'essentiel ») pour le dirigeant qui
découvre, `Expert` pour le professionnel.

La base se remplit rubrique par rubrique, un commit par rubrique, chacune relue avant la
suivante. Une rubrique sans article n'apparaît pas dans les filtres.

---

## 2. Articles : des fichiers Markdown dans le dépôt

Chaque article est un fichier `backend/MAAT.Infrastructure/Knowledge/Articles/<slug>.md`,
embarqué dans l'assembly comme les polices du rapport, et chargé une fois au démarrage
(`EmbeddedKnowledgeBase`). Pas de base de données, pas d'écran d'administration : un
article se relit dans la pull request qui l'ajoute, et sa moindre correction est tracée par
git. Pour quelques dizaines d'articles écrits par l'équipe, c'est le moyen le plus simple
et le plus sûr de garantir leur qualité.

En-tête obligatoire, entre deux lignes `---` :

```
slug: quest-ce-que-la-rse        identique au nom du fichier
title: Qu'est-ce que la RSE ?
summary: Une phrase, affichée dans la liste et les résultats.
category: GettingStarted
level: Essentials
order: 1                        rang de lecture dans la rubrique
tags: définition, ISO 26000      séparés par des virgules
updated: 2026-09-28              date de vérification des sources
source: Titre | https://…        une ligne par source, au moins une
```

Lu à la main (`KnowledgeArticleParser`) : cinq champs plats ne justifient pas une
bibliothèque YAML. **Strict** : un champ manquant, inconnu ou mal formé, un slug différent
du nom du fichier, une source sans URL absolue en `https`, un corps vide ou un slug en double
**font échouer le démarrage de l'API** avec le nom du fichier. Un article cassé ne disparaît
jamais en silence.

**Règles de rédaction.**

- **Toute affirmation factuelle est sourcée** : chiffre, seuil, date, obligation légale. Les
  sources privilégiées sont officielles (Légifrance, EUR-Lex, ministères, Portail RSE,
  ADEME, Service Public). Une source d'analyse (cabinet d'avocats, organisme professionnel)
  est admise quand elle recoupe ou complète une source officielle.
- **La date `updated` est celle de la vérification des sources**, affichée au lecteur : la
  réglementation RSE change vite.
- Le titre n'est pas répété dans le corps (pas de `# Titre`) : l'écran l'affiche en h1.
- Les liens vers un autre article prennent la forme `/documentation/<slug>` et doivent viser
  un article existant (vérifié par les tests).
- Les liens externes du corps (organismes publics, guichets d'aide) sont admis, en `https`
  uniquement, comme les sources : tout lien du corps vise soit un article, soit une adresse
  `https://` (vérifié par les tests).
- Deux articles d'une même rubrique n'ont jamais le même rang de lecture (vérifié par les
  tests).
- Aucun HTML dans le Markdown : il ne serait pas interprété (section 5).
- **Aucun tiret cadratin (—)**, règle éditoriale de tout le site : deux-points, virgule ou
  parenthèses à la place (vérifié par les tests, sur tout le texte affiché de l'article).

---

## 3. Recherche

Service **pur** du Domain (`KnowledgeSearch`), en mémoire : la base compte quelques
dizaines d'articles, chargés une fois. Un moteur de recherche (full-text PostgreSQL,
Elasticsearch) serait disproportionné à cette taille et ajouterait de l'infrastructure au
VPS pour un gain nul.

- Accents et majuscules ignorés : « decarbonation » trouve « Décarbonation ».
- Un début de mot suffit (« décarbo »), pour chercher au fil de la frappe.
- Singulier et pluriel en « s » ou « x » équivalents.
- Mots vides ignorés (« le », « de », « la »…).
- **Tous** les mots significatifs doivent être trouvés dans l'article.
- Classement par pertinence : un mot trouvé dans le titre (10) pèse plus que dans les
  mots-clés (6), le résumé (4) ou le corps (1 par occurrence, 5 au plus — un mot répété
  dans un long article n'écrase pas un titre exact). À score égal, ordre alphabétique.
- Sans mot significatif : tous les articles, par rubrique puis par **rang de lecture**
  (`order`) : une rubrique se parcourt comme un chemin, pas dans l'ordre alphabétique.
- Filtres par rubrique et par niveau, cumulables avec la recherche.

---

## 4. Endpoints

Authentifiés, tous les rôles.

`GET /api/documentation?q=&category=&level=` — sommaire et recherche, **toutes offres** :
nombre d'articles par rubrique (sur toute la base, indépendant de la recherche) et articles
trouvés, **sans leur contenu**. `q` limité à 200 caractères (400 au-delà).

`GET /api/documentation/{slug}` — un article complet (contenu Markdown, sources, date de
vérification). `404` pour un slug inconnu, vérifié **avant** le droit : une adresse erronée
n'a pas à proposer un changement d'offre. `403 plan_required` (Essential) hors offre.

---

## 5. Écrans

**Entrée de navigation** « Documentation » (icône `BookOpen`), dans une section
« Ressources » de la barre latérale.

**`/documentation`** : champ de recherche (requête 250 ms après la dernière frappe, la
précédente étant annulée pour qu'une réponse lente n'écrase jamais une recherche plus
récente), rubriques avec leur nombre d'articles, filtre de niveau, résultats en cartes
(rubrique, niveau, titre, résumé, temps de lecture). Aucun résultat : un message et une
piste, jamais une page vide.

**`/documentation/:slug`** : rubrique, niveau, titre, résumé, temps de lecture (200 mots
par minute), date de vérification des sources ; contenu ; sommaire des titres de niveau 2
(ancres) sur grand écran ; sources en liens externes (nouvel onglet, `noopener
noreferrer`) ; mention que le contenu ne constitue pas un avis juridique.

Le Markdown est rendu par `react-markdown` (tableaux par `remark-gfm`) en composants React
stylés selon la charte, **sans jamais interpréter de HTML brut** : le contenu ne peut pas
injecter de script. Liens internes par le routeur (sans rechargement), liens externes dans
un nouvel onglet (`noopener noreferrer`), signalés par une icône et annoncés aux lecteurs
d'écran (« nouvel onglet »), comme les sources.

**Offre Starter** (`abonnement.md`, section 8) : sommaire et recherche visibles, avec une
invitation compacte à l'offre Essential ; l'ouverture d'un article affiche l'invitation à
la place du contenu.

---

## Cas de test

**Recherche (Domain, `KnowledgeSearchTests`)**

1. Sans recherche : tous les articles, par rubrique puis rang de lecture.
2. Accents et majuscules ignorés ; début de mot ; singulier et pluriel.
3. Tous les mots exigés ; mots vides ignorés ; recherche faite seulement de mots vides →
   tout.
4. Titre et mots-clés classés avant le corps.
5. Filtres par rubrique et par niveau.
6. Temps de lecture : minute supérieure, au moins une minute.

**Contenu (`KnowledgeBaseTests`)**

7. Tous les articles embarqués se chargent ; slugs uniques.
8. Chaque article a au moins une source en `https`, un résumé et des mots-clés.
9. Les liens internes visent des articles existants ; les autres liens du corps sont en
   `https` ; pas de titre de niveau 1 dans le corps ; rangs de lecture uniques dans chaque
   rubrique.
10. En-tête invalide (source absente ou en `http`, slug différent du fichier ou mal formé,
    rubrique ou niveau inconnus, rang de lecture non entier positif, date mal formée, champ
    inconnu ou manquant, corps vide) →
    refusé avec le nom du fichier.

**API (`DocumentationApiTests`)**

11. Sans authentification → 401.
12. Starter : sommaire et recherche → 200, sans contenu d'article ; lecture → `403
    plan_required` (Essential).
13. Essential : lecture → 200 avec contenu, sources et date.
14. Slug inconnu → 404, quelle que soit l'offre.
15. Recherche sans accents, filtre par rubrique ; recherche de plus de 200 caractères → 400.

**Écrans (Vitest) et E2E**

16. Liste, recherche après la frappe, filtres, aucun résultat, erreur, invitation Starter,
    accessibilité.
17. Article : Markdown rendu (titres ancrés, tableaux), liens internes et externes (nouvel
    onglet annoncé dans le nom accessible), aucun
    HTML brut interprété, invitation Starter, article inconnu, accessibilité.
18. E2E : compte Starter → entrée Documentation → recherche sans accents → article →
    invitation à Essential.
