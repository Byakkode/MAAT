import { expect, test } from '@playwright/test'

// docs/specs/rapport-pdf.md, section 7. Seule l'offre Starter est atteignable ici sans Stripe
// (abonnement.spec.ts) : l'envoi du logo, réservé à Essential, est couvert par
// CompanyLogoTests (API) et ReportLogoCard.test.tsx (écran). Ce parcours vérifie ce qu'un vrai
// navigateur montre à un compte Starter : l'offre qui inclut le logo, et aucun champ d'envoi.
test('compte Starter → la page Rapports propose le logo avec l’offre Essential', async ({ page }) => {
  const suffix = Math.random().toString(36).slice(2, 10)
  const email = `e2e-logo-${suffix}@maat-test.local`
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

  await page.getByRole('link', { name: 'Rapports', exact: true }).click()
  await expect(page.getByRole('heading', { name: 'Rapports' })).toBeVisible()

  await expect(page.getByText('Votre logo sur le rapport')).toBeVisible()
  await expect(page.getByText(/Inclus à partir de l.offre Essential/).last()).toBeVisible()
  await expect(page.getByLabel('Fichier du logo')).toHaveCount(0)
})
