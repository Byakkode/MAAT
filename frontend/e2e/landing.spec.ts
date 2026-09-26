import { expect, test } from '@playwright/test'

// Page d'accueil publique (route « / »). Seul un vrai navigateur prouve que les ancres
// défilent et que l'appel à l'action mène réellement à l'inscription.
test('la page d’accueil s’affiche sur « / », répond au choix de secteur et mène à l’inscription', async ({ page }) => {
  await page.goto('/')

  await expect(page.getByRole('heading', { level: 1 })).toContainText('trente minutes')

  const sectors = page.getByRole('region', { name: /le même effort/i })
  await sectors.getByRole('button', { name: /commerce/i }).click()
  await expect(sectors.getByRole('button', { name: /commerce/i })).toHaveAttribute('aria-pressed', 'true')
  // Le score apparaît deux fois : le chiffre animé (aria-hidden) et sa valeur finale pour les
  // lecteurs d'écran. Seule la seconde est stable ; viser le texte « 60 » tout court échoue
  // en mode strict dès que l'animation a fini de défiler, c'est-à-dire sous charge.
  await expect(sectors.locator('.sr-only').filter({ hasText: /^60$/ })).toBeAttached()

  await page.getByRole('main').getByRole('link', { name: 'Commencer le diagnostic' }).click()

  await expect(page).toHaveURL(/\/register$/)
  await expect(page.getByRole('heading', { name: 'Créer un compte' })).toBeVisible()
})

test('les liens de navigation font défiler jusqu’à leur section', async ({ page }) => {
  await page.goto('/')

  await page.getByRole('navigation', { name: 'Sections de la page' }).getByRole('link', { name: 'Sécurité' }).click()

  await expect(page).toHaveURL(/#securite$/)
  await expect(page.getByRole('heading', { name: /vos données restent en france/i })).toBeInViewport()
})

test('la section Tarifs compare quatre offres et mène à l’inscription', async ({ page }) => {
  await page.goto('/')

  await page.getByRole('navigation', { name: 'Sections de la page' }).getByRole('link', { name: 'Tarifs' }).click()

  await expect(page).toHaveURL(/#tarifs$/)
  const table = page.getByRole('table', { name: /fonctionnalités incluses/i })
  await expect(table).toBeInViewport()
  await expect(table.getByRole('columnheader')).toHaveCount(4)
  await expect(table.getByRole('columnheader', { name: /enterprise/i })).toContainText('Bientôt disponible')

  await table.getByRole('link', { name: 'Choisir Essential' }).click()

  await expect(page).toHaveURL(/\/register$/)
  await expect(page.getByRole('heading', { name: 'Créer un compte' })).toBeVisible()
})
