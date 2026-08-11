import { expect, test } from '@playwright/test'

// docs/specs/questionnaire.md, sections 1 et 5. Compte neuf de bout en bout à travers
// l'interface réelle : inscription, connexion, démarrage du diagnostic (POST /api/diagnostics,
// câblé au bouton de l'écran « aucun diagnostic en cours ») puis parcours jusqu'à la
// complétion. Aucune étape ne passe par page.request : le seeder direct via l'API masquerait
// une régression dans le formulaire d'inscription, de connexion, ou dans le bouton de
// démarrage lui-même — précisément ce que ce test doit prouver.
test('un compte neuf s’inscrit, démarre un diagnostic et le complète — le tout via l’interface', async ({ page }) => {
  const suffix = Math.random().toString(36).slice(2, 10)
  const email = `e2e-questionnaire-${suffix}@maat-test.local`
  const password = 'MotDePasseValide2026!'

  await page.goto('/register')
  await page.getByLabel('Adresse e-mail').fill(email)
  await page.getByLabel('Mot de passe').fill(password)
  await page.getByLabel("Nom de l'entreprise").fill(`Entreprise E2E ${suffix}`)
  await page.getByLabel('Code NAF').fill('6201Z')
  await page.getByLabel('Région').fill('Île-de-France')
  await page.getByRole('button', { name: "S'inscrire" }).click()
  await expect(page.getByRole('status')).toBeVisible()

  await page.getByRole('link', { name: 'Se connecter' }).click()
  await page.getByLabel('Adresse e-mail').fill(email)
  await page.getByLabel('Mot de passe').fill(password)
  await page.getByRole('button', { name: 'Se connecter' }).click()
  await expect(page).toHaveURL(/\/dashboard$/)

  await page.getByRole('link', { name: 'Questionnaire', exact: true }).click()

  // docs/specs/questionnaire.md, section 5 : un compte neuf n'a aucun diagnostic en cours —
  // le 404 de GET /current doit produire cette invitation, jamais un message d'erreur.
  await expect(page.getByText("Vous n'avez pas de diagnostic en cours.")).toBeVisible()

  await page.getByRole('button', { name: 'Démarrer un diagnostic' }).click()
  await expect(page.getByRole('status').filter({ hasText: 'Étape' })).toBeVisible()

  // Répond à toutes les questions de l'étape courante, avance, jusqu'à atteindre la dernière
  // étape et pouvoir terminer. Ne présuppose ni le nombre d'étapes ni le nombre de questions
  // par étape : seul le jeu de données seedé (docs/specs/modele-donnees.md, 3 questions de
  // démonstration en Environnement) le détermine.
  const maxSteps = 5
  let completed = false
  for (let step = 0; step < maxSteps && !completed; step += 1) {
    const fieldsets = page.locator('fieldset')
    const count = await fieldsets.count()
    expect(count).toBeGreaterThan(0)

    for (let i = 0; i < count; i += 1) {
      const fieldset = fieldsets.nth(i)
      await fieldset.getByRole('radio', { name: 'Pleinement en place et suivi' }).check()
      await expect(fieldset.getByRole('status')).toHaveText('Enregistré', { timeout: 10_000 })
    }

    const completeButton = page.getByRole('button', { name: 'Terminer' })
    if (await completeButton.count()) {
      await expect(completeButton).toBeEnabled()
      await completeButton.click()
      completed = true
      continue
    }

    await page.getByRole('button', { name: 'Suivant' }).click()
  }

  expect(completed).toBe(true)
  await expect(page.getByText('Diagnostic complété.')).toBeVisible()
})
