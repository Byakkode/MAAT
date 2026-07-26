# 0002 — Ne pas rétrograder react-router suite à GHSA-qwww-vcr4-c8h2

## Statut

Acceptée — 2026-07-27

## Contexte

Le 2026-07-24, GitHub a publié l'avis de sécurité
[GHSA-qwww-vcr4-c8h2](https://github.com/advisories/GHSA-qwww-vcr4-c8h2)
(« React Router: RSC Mode CSRF Bypass Allows Action Execution Before 400
Response », CWE-352, sévérité *high*, CVSS4 7.1). Aucun CVE n'a été attribué
à cet avis au moment de la rédaction — GitHub l'a directement publié comme
*reviewed advisory*, ce qui suffit à déclencher une alerte `npm audit` /
Dependabot.

Le paquet affecté est `react-router` (le cœur dont dépend `react-router-dom`)
pour les versions `>= 7.12.0, < 8.3.0`. Le lockfile du frontend résout
`react-router` et `react-router-dom` en `7.18.1` (`frontend/package-lock.json`) :
le projet est donc dans la plage vulnérable, sans version corrigée
disponible en 7.x — le correctif exige de passer en 8.x.

Cette ADR documente pourquoi l'équipe ne rétrograde ni ne bloque le build sur
cet avis, contrairement au réflexe par défaut face à une alerte de sécurité.

## Décision

**La CVE concernée.** GHSA-qwww-vcr4-c8h2 fait suite à
[CVE-2026-22030](https://github.com/remix-run/react-router/security/advisories/GHSA-h5cw-625j-3rxh)
(« CSRF issue in Action/Server Action Request Processing ») : elle traite des
scénarios de contournement CSRF restants dans les chemins de code RSC
*unstable* de React Router — c'est-à-dire une variante plus récente et plus
étroite du même problème que CVE-2026-22030 traitait déjà pour le Framework
Mode « stable ».

**Pourquoi elle ne s'applique pas à MAAT.** L'avis contient une note
explicite de portée : *« This only affects your application if you are using
the unstable RSC APIs »*. `frontend/src/App.tsx` initialise le routeur en
**Declarative Mode** :

```tsx
import { BrowserRouter, Link, Route, Routes } from 'react-router-dom'
```

Le `package.json` du frontend ne dépend que de `react-router-dom` — ni
`@react-router/dev`, ni `@remix-run/server-runtime`, ni aucun paquet RSC.
Le build est un SPA Vite pur (`vite build` / `vite preview`, sans serveur
Node de rendu), conforme à l'architecture retenue : le rendu est fait
côté client, l'API métier est le backend .NET (voir ADR 0001), et le seul
rendu serveur du produit est celui des PDF via QuestPDF, sans rapport avec
React Router. Il n'y a donc ni RSC, ni *server actions*, ni `action`
Framework Mode dans ce projet : la surface d'attaque décrite par l'avis
(un `action` RSC exécuté avant qu'une réponse 400 ne l'empêche) n'existe pas
dans le code de MAAT. Le même raisonnement s'applique à l'avis parent
CVE-2026-22030, dont la note de portée précise explicitement que le
Declarative Mode (`<BrowserRouter>`) et le Data Mode
(`createBrowserRouter`/`<RouterProvider>`) ne sont pas concernés.

**Pourquoi une rétrogradation réintroduirait des vulnérabilités
applicables.** La plage vulnérable de GHSA-qwww-vcr4-c8h2 commence à
`7.12.0` : descendre en dessous l'éviterait en théorie. Mais les versions
antérieures à `7.18.0` (notre version actuelle) contiennent d'autres
correctifs de sécurité, et au moins un est **directement applicable** à
MAAT puisqu'il touche les API de navigation utilisées telles quelles dans
`App.tsx` :

- [CVE-2026-53669](https://github.com/remix-run/react-router/security/advisories/GHSA-wrjc-x8rr-h8h6)
  (« Open redirect via backslash in `<Link>` and `useNavigate` ») — corrigé
  en `7.18.0`, sans note d'exemption de mode. `<Link>` est utilisé dans
  `App.tsx` : ce correctif est réel pour ce projet, contrairement à
  GHSA-qwww-vcr4-c8h2.

Les autres correctifs de la même plage (`< 7.18.0`) sont, eux, hors périmètre
pour la même raison que GHSA-qwww-vcr4-c8h2 — chacun porte une note de portée
équivalente confirmant qu'ils ne concernent que le Framework Mode, le SSR
manuel ou les API RSC *unstable*, absents de MAAT :
[CVE-2026-55685](https://github.com/remix-run/react-router/security/advisories/GHSA-chx6-hx7r-mcp5)
(DoS, *« only impacts Framework Mode »*),
[CVE-2026-53667](https://github.com/remix-run/react-router/security/advisories/GHSA-h8fp-f39c-q6mh)
(XSS, *« only affects ... unstable RSC APIs »*),
[CVE-2026-53666](https://github.com/remix-run/react-router/security/advisories/GHSA-337j-9hxr-rhxg)
(injection, *« does not impact ... Declarative Mode »*).

Rétrograder échangerait donc une CVE à haute sévérité mais **inapplicable**
(RSC) contre une CVE de sévérité moyenne mais **applicable** (redirection
ouverte sur `<Link>`/`useNavigate`) : un mauvais compromis, pris uniquement
pour faire taire une alerte automatisée sans lire son contexte.

**Condition de réévaluation.** Cette décision est valide tant que trois
conditions restent vraies : (1) le frontend reste un SPA Vite en Declarative
Mode, sans Framework Mode ni Data Mode à *loaders*/`action` serveur ; (2)
aucune dépendance RSC (`unstable_*` de React Router, `@react-router/dev`,
etc.) n'est introduite ; (3) aucun nouvel avis dont la plage vulnérable
touche `7.18.x` sans note d'exemption « RSC/Framework Mode uniquement »
n'est publié. La réintroduction de l'une de ces conditions — en particulier
toute évolution vers du rendu serveur React (SSR, RSC, *server actions*) —
doit rouvrir cette ADR avant d'être mergée. Le passage en 8.x (qui corrige
GHSA-qwww-vcr4-c8h2) reste souhaitable à terme mais suit la règle du
`CLAUDE.md` sur les versions figées : à discuter, pas à appliquer en
réaction à une alerte.

## Conséquences

- `react-router-dom` reste en `^7.18.1` : ne pas downgrader en réaction à
  GHSA-qwww-vcr4-c8h2, et ne pas upgrader vers 8.x sans discussion préalable
  (changement majeur, hors du cadre de cette ADR).
- Tout scanner de dépendances (`npm audit`, Dependabot, CI) continuera de
  signaler GHSA-qwww-vcr4-c8h2 comme non corrigée : documenter ce statut
  (référence à cette ADR) dans la configuration du scanner si le projet
  s'outille en CI de sécurité, pour éviter qu'un futur contributeur ne
  rétrograde par réflexe.
- Introduire du rendu serveur React (SSR, RSC, Framework Mode, *server
  actions*) — même partiellement, même pour une seule page — invalide
  l'analyse de portée ci-dessus et exige une nouvelle revue de sécurité
  avant merge.
