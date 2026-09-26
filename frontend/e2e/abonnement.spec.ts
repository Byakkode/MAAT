import { expect, test, type Page } from '@playwright/test'

// docs/specs/abonnement.md, section 2. Parcours réels dans le navigateur : inscription,
// connexion, écran de sélection. Le paiement lui-même se déroule sur la page hébergée par
// Stripe : seul le dernier test y va, et seulement si l'API tourne avec une clé Stripe de test
// (STRIPE_E2E=1) — sans quoi il n'y aurait rien à observer.

const PASSWORD = 'MotDePasseValide2026!'

async function registerAndLogin(page: Page, registerUrl: string): Promise<void> {
  const suffix = Math.random().toString(36).slice(2, 10)
  const email = `e2e-abonnement-${suffix}@maat-test.local`

  await page.goto(registerUrl)
  await expect(page.getByRole('heading', { name: 'Créer un compte' })).toBeVisible()
  await page.getByLabel('Adresse e-mail').fill(email)
  await page.getByLabel('Mot de passe').fill(PASSWORD)
  await page.getByLabel("Nom de l'entreprise").fill(`Entreprise E2E ${suffix}`)
  await page.getByLabel('Code NAF').fill('6201Z')
  await page.getByRole('option', { name: /6201Z/ }).first().click()
  await page.getByLabel('Région').selectOption('Île-de-France')
  // bcrypt WorkFactor=12 : attendre la réponse réseau plutôt qu'un délai arbitraire.
  await Promise.all([
    page.waitForResponse((resp) => resp.url().includes('/api/auth/register') && resp.request().method() === 'POST'),
    page.getByRole('button', { name: "S'inscrire" }).click(),
  ])
  await expect(page.getByRole('status').filter({ hasText: 'Vérifiez votre boîte mail' })).toBeVisible()

  // Ancre sur le titre de la page de connexion avant toute saisie (CLAUDE.md, section Frontend) :
  // les champs e-mail et mot de passe existent sur les deux écrans.
  await page.getByRole('link', { name: 'Se connecter' }).click()
  await expect(page.getByRole('heading', { name: 'Connexion', exact: true })).toBeVisible()
  await page.getByLabel('Adresse e-mail').fill(email)
  await page.getByLabel('Mot de passe').fill(PASSWORD)
  await Promise.all([
    page.waitForResponse((resp) => resp.url().includes('/api/auth/login') && resp.request().method() === 'POST'),
    page.getByRole('button', { name: 'Se connecter' }).click(),
  ])
}

async function skipLoginIntro(page: Page): Promise<void> {
  const intro = page.getByRole('status', { name: /connexion réussie/i })
  await expect(intro).toBeVisible()
  await intro.getByRole('button', { name: 'Passer' }).click()
  await expect(intro).toBeHidden()
}

test('un compte sans offre choisit Starter sur l’écran de sélection avant d’accéder à l’application', async ({ page }) => {
  await registerAndLogin(page, '/register')

  await expect(page).toHaveURL(/\/abonnement$/)
  await expect(page.getByRole('heading', { name: 'Choisissez votre offre' })).toBeVisible()
  await expect(page.getByRole('columnheader', { name: /enterprise/i })).toContainText('Bientôt disponible')

  await Promise.all([
    page.waitForResponse((resp) => resp.url().includes('/api/billing/starter') && resp.request().method() === 'POST'),
    page.getByRole('button', { name: 'Commencer gratuitement' }).click(),
  ])

  await expect(page).toHaveURL(/\/tableau-de-bord$/)
  await skipLoginIntro(page)

  // L'offre choisie est visible dans « Mon compte ».
  await page.goto('/compte')
  const card = page.getByRole('region', { name: 'Abonnement' })
  await expect(card).toContainText('Offre Starter')
  await expect(card.getByRole('link', { name: 'Voir les offres' })).toBeVisible()
})

test('Starter choisi sur la page d’accueil : l’écran de sélection est sauté', async ({ page }) => {
  await page.goto('/#tarifs')
  const pricing = page.getByRole('region', { name: /commencez gratuitement/i })
  await pricing.getByRole('link', { name: 'Commencer gratuitement' }).click()
  await expect(page).toHaveURL(/\/register\?offre=starter$/)
  await expect(page.getByText(/offre choisie/i)).toContainText('Starter')

  await registerAndLogin(page, '/register?offre=starter')

  await expect(page).toHaveURL(/\/tableau-de-bord$/)
  await skipLoginIntro(page)
  await expect(page.getByRole('heading', { name: 'Tableau de bord', exact: true })).toBeVisible()
})

// S'arrête sur la page Stripe, sans payer : Stripe détecte les navigateurs automatisés sur
// Checkout et y retient le paiement (bouton bloqué sur « En cours de traitement »), et
// déconseille d'automatiser sa page. Ce qui suit le paiement — activation au retour sans
// attendre le webhook, puis ouverture du tableau de bord — est vérifié par
// BillingTests (cas 16 et 17) et SubscriptionConfirmationPage.test.tsx.
test('offre payante choisie sur la page d’accueil : la première connexion mène au paiement Stripe', async ({ page }) => {
  test.skip(!process.env.STRIPE_E2E, 'Nécessite une API démarrée avec une clé Stripe de test (STRIPE_E2E=1).')

  await registerAndLogin(page, '/register?offre=essential&periode=annuelle')

  await page.waitForURL(/checkout\.stripe\.com/, { timeout: 30_000 })
  // Le montant figure plusieurs fois (ligne, sous-total, total) : la première occurrence suffit.
  // Pas de sélecteur sur les identifiants internes de la page Stripe, qui peuvent changer.
  await expect(page.getByText(/1\s?490,00/).first()).toBeVisible({ timeout: 30_000 })
})
