import { expect, test } from '@playwright/test'

// Preuve que la chaîne Playwright fonctionne réellement contre `npm run dev`
// (playwright.config.ts démarre le serveur de dev lui-même, cf. webServer) : un vrai
// navigateur charge la page et lit le DOM rendu, pas jsdom.
//
// docs/specs/coquille-et-compte.md, section 1 : la coquille (et sa navigation) n'existe
// qu'à l'intérieur des routes protégées — un visiteur sans session ne la voit jamais et
// est redirigé vers /login, contrairement à l'ancienne navigation rendue hors de
// ProtectedRoute.
test('la page charge et redirige vers la connexion sans session', async ({ page }) => {
  await page.goto('/')

  await expect(page).toHaveURL(/\/login$/)
  await expect(page.getByRole('heading', { name: 'Connexion', exact: true })).toBeVisible()
})
