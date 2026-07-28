import { expect, test } from '@playwright/test'

// Preuve que la chaîne Playwright fonctionne réellement contre `npm run dev`
// (playwright.config.ts démarre le serveur de dev lui-même, cf. webServer) : un vrai
// navigateur charge la page et lit le DOM rendu, pas jsdom.
test('la page charge et affiche la navigation principale', async ({ page }) => {
  await page.goto('/')

  await expect(page.getByRole('link', { name: 'Questionnaire' })).toBeVisible()
  await expect(page.getByRole('link', { name: 'Tableau de bord' })).toBeVisible()
  await expect(page.getByRole('link', { name: 'Rapport' })).toBeVisible()
})
