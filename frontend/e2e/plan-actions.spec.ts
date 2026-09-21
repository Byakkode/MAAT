import { expect, test } from '@playwright/test'

// docs/specs/recommandations.md, section 4. Aucun parcours Playwright ne visitait jamais
// l'écran "Plan d'actions" avant ce test — l'entrée de navigation et le lien "Voir tout le
// plan d'actions" du tableau de bord y menaient, mais la page elle-même n'était qu'un
// placeholder muet, sans appel réseau ni résolution d'identifiant de diagnostic.

test('compte sans diagnostic complété → Plan d’actions affiche une invitation, pas un écran vide', async ({ page }) => {
  const suffix = Math.random().toString(36).slice(2, 10)
  const email = `e2e-plan-actions-vide-${suffix}@maat-test.local`
  const password = 'MotDePasseValide2026!'

  await page.goto('/register')
  await page.getByLabel('Adresse e-mail').fill(email)
  await page.getByLabel('Mot de passe').fill(password)
  await page.getByLabel("Nom de l'entreprise").fill(`Entreprise E2E ${suffix}`)
  await page.getByLabel('Code NAF').fill('6201Z')
  await page.getByRole('option', { name: /6201Z/ }).first().click()
  await page.getByLabel('Région').selectOption('Île-de-France')
  await page.getByRole('button', { name: "S'inscrire" }).click()
  await expect(page.getByRole('status')).toBeVisible()

  await page.getByRole('link', { name: 'Se connecter' }).click()
  await expect(page.getByRole('heading', { name: 'Connexion', exact: true })).toBeVisible()
  await page.getByLabel('Adresse e-mail').fill(email)
  await page.getByLabel('Mot de passe').fill(password)
  await page.getByRole('button', { name: 'Se connecter' }).click()
  await expect(page).toHaveURL('/')
  await expect(page.getByRole('heading', { name: 'Tableau de bord', exact: true })).toBeVisible()

  await page.getByRole('link', { name: "Plan d'actions", exact: true }).click()
  await expect(page.getByRole('heading', { name: "Plan d'actions" })).toBeVisible()
  await expect(page.getByText("Vous n'avez pas encore de diagnostic complété.")).toBeVisible()

  // Le lien fonctionne réellement, pas seulement un texte d'invitation décoratif.
  await page.getByRole('link', { name: 'Commencer le questionnaire' }).click()
  await expect(page.getByRole('heading', { name: 'Questionnaire' })).toBeVisible()
})

test('diagnostic complété avec des réponses faibles → recommandations visibles et actionnables', async ({ page }) => {
  // Même raison que questionnaire.spec.ts : 45 questions, chacune un cycle debounce (500 ms) +
  // aller-retour réseau ; ce test répond en plus à toutes, comme celui-là, puis exerce l'écran
  // Plan d'actions par-dessus.
  test.setTimeout(150_000)

  const suffix = Math.random().toString(36).slice(2, 10)
  const email = `e2e-plan-actions-${suffix}@maat-test.local`
  const password = 'MotDePasseValide2026!'

  await page.goto('/register')
  await page.getByLabel('Adresse e-mail').fill(email)
  await page.getByLabel('Mot de passe').fill(password)
  await page.getByLabel("Nom de l'entreprise").fill(`Entreprise E2E ${suffix}`)
  await page.getByLabel('Code NAF').fill('6201Z')
  await page.getByRole('option', { name: /6201Z/ }).first().click()
  await page.getByLabel('Région').selectOption('Île-de-France')
  await page.getByRole('button', { name: "S'inscrire" }).click()
  await expect(page.getByRole('status')).toBeVisible()

  await page.getByRole('link', { name: 'Se connecter' }).click()
  await expect(page.getByRole('heading', { name: 'Connexion', exact: true })).toBeVisible()
  await page.getByLabel('Adresse e-mail').fill(email)
  await page.getByLabel('Mot de passe').fill(password)
  await page.getByRole('button', { name: 'Se connecter' }).click()
  await expect(page).toHaveURL('/')

  await page.getByRole('link', { name: 'Diagnostic', exact: true }).click()
  await expect(page.getByRole('heading', { name: 'Questionnaire' })).toBeVisible()
  await expect(page.getByText("Vous n'avez pas de diagnostic en cours.")).toBeVisible()
  await page.getByRole('button', { name: 'Démarrer un diagnostic' }).click()
  const stepIndicator = page.getByRole('status').filter({ hasText: 'Étape' })
  await expect(stepIndicator).toBeVisible()

  // "Non, ce n'est pas en place" (valeur 0) est inférieur ou égal au seuil de déclenchement de
  // toute recommandation active (docs/specs/recommandations.md, section 1) : répondre ainsi à
  // chaque question garantit un plan d'actions non vide à l'arrivée, contrairement au parcours
  // de questionnaire.spec.ts qui répond au meilleur niveau partout et n'en déclenche aucune.
  const maxSteps = 5
  let completed = false
  for (let step = 0; step < maxSteps && !completed; step += 1) {
    const fieldsets = page.locator('fieldset')
    const count = await fieldsets.count()
    expect(count).toBeGreaterThan(0)

    for (let i = 0; i < count; i += 1) {
      const fieldset = fieldsets.nth(i)
      await fieldset.getByRole('radio', { name: "Non, ce n'est pas en place" }).check()
      await expect(fieldset.getByRole('status')).toHaveText('Enregistré', { timeout: 20_000 })
    }

    const completeButton = page.getByRole('button', { name: 'Terminer' })
    if (await completeButton.count()) {
      await expect(completeButton).toBeEnabled()
      await completeButton.click()
      completed = true
      continue
    }

    const previousStepLabel = (await stepIndicator.textContent()) ?? ''
    await page.getByRole('button', { name: 'Suivant' }).click()
    await expect(stepIndicator).not.toHaveText(previousStepLabel)
  }

  expect(completed).toBe(true)
  await expect(page.getByText('Diagnostic complété.')).toBeVisible()

  await page.getByRole('link', { name: "Plan d'actions", exact: true }).click()
  await expect(page.getByRole('heading', { name: "Plan d'actions" })).toBeVisible()

  // Écran non vide : au moins une recommandation, jamais un écran blanc pour ce compte.
  const checkboxes = page.getByRole('checkbox')
  await expect(checkboxes.first()).toBeVisible()
  const totalBefore = await checkboxes.count()
  expect(totalBefore).toBeGreaterThan(0)

  // Coche la première action et vérifie que l'état survit à un rechargement complet — pas
  // seulement un état local React qui disparaîtrait au premier F5. La case est contrôlée par
  // l'état serveur (planActionsStore.toggle : PATCH puis re-fetch) plutôt que par un état local
  // synchrone, donc .click() + assertion qui relance (toBeChecked), pas .check() qui vérifie
  // une seule fois juste après le clic et échouerait sur ce délai réseau.
  const firstItem = page.locator('li').filter({ has: checkboxes.first() })
  await checkboxes.first().click()
  await expect(checkboxes.first()).toBeChecked({ timeout: 10_000 })
  await expect(firstItem.getByText(/Terminée le/)).toBeVisible()

  await page.reload()
  await expect(page.getByRole('heading', { name: "Plan d'actions" })).toBeVisible()
  await expect(page.getByRole('checkbox').first()).toBeChecked()
})
