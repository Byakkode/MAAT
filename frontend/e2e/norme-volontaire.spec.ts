import { expect, test } from '@playwright/test'

// docs/specs/norme-volontaire.md, cas 17. Seule l'offre Starter est atteignable ici sans Stripe
// (abonnement.spec.ts) : la saisie, réservée à Essential, est couverte par VsmeApiTests (API
// contre PostgreSQL) et IndicatorsPage.test.tsx (écran). Ce parcours traverse le vrai
// navigateur et la vraie API : les trois lectures de l'écran (déclarations, sites, complétude)
// passent la politique CORS et la sérialisation JSON, et l'écran montre les onze informations
// de la norme, en lecture seule, avec l'offre qui ouvre la saisie.
test('compte Starter → l’écran Indicateurs présente B1 à B11 en lecture seule', async ({ page }) => {
  const suffix = Math.random().toString(36).slice(2, 10)
  const email = `e2e-vsme-${suffix}@maat-test.local`
  const password = 'MotDePasseValide2026!'

  await page.goto('/register?offre=starter')
  await page.getByLabel('Adresse e-mail').fill(email)
  await page.getByLabel('Mot de passe').fill(password)
  await page.getByLabel("Nom de l'entreprise").fill(`Entreprise E2E ${suffix}`)
  await page.getByLabel('Code NAF').fill('6201Z')
  await page.getByRole('option', { name: /6201Z/ }).first().click()
  await page.getByLabel('Région').selectOption('Île-de-France')
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

  const completeness = page.waitForResponse((resp) => /\/api\/vsme\/\d+\/completeness$/.test(resp.url()))
  await page.getByRole('link', { name: 'Indicateurs', exact: true }).click()
  expect((await completeness).status()).toBe(200)
  await expect(page.getByRole('heading', { name: 'Informations de durabilité (norme volontaire)' })).toBeVisible()

  // Rien de saisi, et l'inscription déclare par défaut une micro-entreprise : B4 (pas
  // d'obligation de déclaration) et B11 (aucune condamnation) sont complètes par le principe
  // « si applicable », B3, B6 et B7 parce qu'elles sont facultatives jusqu'à 10 salariés (§8).
  const year = new Date().getFullYear()
  const banner = page.getByRole('status').filter({ hasText: 'informations sur 11' })
  await expect(banner.getByText(`5 informations sur 11 complètes pour ${year}`)).toBeVisible()
  await expect(banner.getByText(/sont facultatifs/)).toBeVisible()
  await expect(banner.getByRole('link')).toHaveText([
    "B1 · Base d'établissement du rapport",
    'B2 · Pratiques, politiques et initiatives futures pour une économie plus durable',
    'B5 · Biodiversité',
    'B8 · Effectifs : caractéristiques générales',
    'B9 · Effectifs : santé et sécurité',
    'B10 · Effectifs : rémunération, négociation collective et formation',
  ])
  await expect(page.getByRole('heading', { level: 3 })).toHaveCount(11 + 4)
  await expect(page.getByRole('heading', { name: 'Condamnations et amendes pour corruption' })).toBeVisible()

  await expect(page.getByText(/Inclus à partir de l.offre Essential/)).toBeVisible()
  await expect(page.getByLabel('Forme juridique')).toHaveJSProperty('readOnly', true)
  await expect(page.getByRole('button', { name: 'Enregistrer' })).toHaveCount(0)
  await expect(page.getByText('Aucun site déclaré.').first()).toBeVisible()
})
