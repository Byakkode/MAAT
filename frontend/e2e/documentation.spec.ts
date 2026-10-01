import { expect, test } from '@playwright/test'

// docs/specs/documentation.md. Parcours réel dans un navigateur, avec les articles réellement
// embarqués par l'API : entrée « Documentation » de la navigation, recherche, puis ouverture
// d'un article. Compte Starter (seule offre atteignable ici sans Stripe) : le sommaire et la
// recherche sont ouverts, la lecture propose l'offre Essential. La lecture complète est
// couverte par DocumentationApiTests (API) et DocumentationArticlePage.test.tsx (écran).
test('compte Starter → Documentation : recherche dans la base, lecture réservée à Essential', async ({ page }) => {
  const suffix = Math.random().toString(36).slice(2, 10)
  const email = `e2e-documentation-${suffix}@maat-test.local`
  const password = 'MotDePasseValide2026!'

  await page.goto('/register?offre=starter')
  await page.getByLabel('Adresse e-mail').fill(email)
  await page.getByLabel('Mot de passe').fill(password)
  await page.getByLabel("Nom de l'entreprise").fill(`Entreprise E2E ${suffix}`)
  await page.getByLabel('Code NAF').fill('6201Z')
  await page.getByRole('option', { name: /6201Z/ }).first().click()
  await page.getByLabel('Région').click()
  await page.getByRole('option', { name: 'Île-de-France' }).click()
  // bcrypt WorkFactor=12 : attendre la réponse réseau plutôt qu'un délai arbitraire (même
  // raison que plan-actions.spec.ts).
  await Promise.all([
    page.waitForResponse((resp) => resp.url().includes('/api/auth/register') && resp.request().method() === 'POST'),
    page.getByRole('button', { name: "S'inscrire" }).click(),
  ])
  await expect(page.getByRole('status').filter({ hasText: 'Vérifiez votre boîte mail' })).toBeVisible()

  await page.getByRole('link', { name: 'Se connecter' }).click()
  await expect(page.getByRole('heading', { name: 'Connexion', exact: true })).toBeVisible()
  await page.getByLabel('Adresse e-mail').fill(email)
  await page.getByLabel('Mot de passe').fill(password)
  await Promise.all([
    page.waitForResponse((resp) => resp.url().includes('/api/auth/login') && resp.request().method() === 'POST'),
    page.getByRole('button', { name: 'Se connecter' }).click(),
  ])
  await expect(page).toHaveURL('/tableau-de-bord')
  const intro = page.getByRole('status', { name: /connexion réussie/i })
  await expect(intro).toBeVisible()
  await intro.getByRole('button', { name: 'Passer' }).click()
  await expect(intro).toBeHidden()

  await page.getByRole('link', { name: 'Documentation', exact: true }).click()
  await expect(page.getByRole('heading', { name: 'Documentation', exact: true })).toBeVisible()
  await expect(page.getByText('Lecture des articles', { exact: true })).toBeVisible()

  // Recherche sans accents : « responsabilite societale » doit trouver l'article de définition.
  await page.getByLabel('Rechercher dans la documentation').fill('responsabilite societale')
  const definition = page.getByRole('link', { name: "Qu'est-ce que la RSE ?" })
  await expect(definition).toBeVisible()

  await definition.click()
  await expect(page).toHaveURL('/documentation/quest-ce-que-la-rse')
  await expect(page.getByText('Cet article est réservé aux offres payantes')).toBeVisible()

  await page.getByRole('link', { name: 'Toute la documentation' }).click()
  await expect(page.getByRole('heading', { name: 'Documentation', exact: true })).toBeVisible()
})
