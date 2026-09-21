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
  // Même raison que pour le login ci-dessous : bcrypt WorkFactor=12 peut dépasser les 5 s
  // de timeout par défaut de toBeVisible() sous charge parallèle. On attend la réponse réseau
  // pour garantir que l'utilisateur est en base avant de tenter la connexion.
  await Promise.all([
    page.waitForResponse(
      (resp) => resp.url().includes('/api/auth/register') && resp.request().method() === 'POST',
    ),
    page.getByRole('button', { name: "S'inscrire" }).click(),
  ])
  await expect(page.getByRole('status').filter({ hasText: 'Vérifiez votre boîte mail' })).toBeVisible()

  await page.getByRole('link', { name: 'Se connecter' }).click()
  await expect(page.getByRole('heading', { name: 'Connexion', exact: true })).toBeVisible()
  await page.getByLabel('Adresse e-mail').fill(email)
  await page.getByLabel('Mot de passe').fill(password)
  // bcrypt WorkFactor=12 peut prendre plusieurs secondes sous charge parallèle — attendre la
  // réponse réseau avant de vérifier la navigation évite de dépendre d'un timeout arbitraire.
  await Promise.all([
    page.waitForResponse(
      (resp) => resp.url().includes('/api/auth/login') && resp.request().method() === 'POST',
    ),
    page.getByRole('button', { name: 'Se connecter' }).click(),
  ])
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
  // Même raison que pour le premier test.
  await Promise.all([
    page.waitForResponse(
      (resp) => resp.url().includes('/api/auth/register') && resp.request().method() === 'POST',
    ),
    page.getByRole('button', { name: "S'inscrire" }).click(),
  ])
  await expect(page.getByRole('status').filter({ hasText: 'Vérifiez votre boîte mail' })).toBeVisible()

  await page.getByRole('link', { name: 'Se connecter' }).click()
  await expect(page.getByRole('heading', { name: 'Connexion', exact: true })).toBeVisible()
  await page.getByLabel('Adresse e-mail').fill(email)
  await page.getByLabel('Mot de passe').fill(password)
  // Même raison que ci-dessus : bcrypt sous charge parallèle peut dépasser 5 s.
  await Promise.all([
    page.waitForResponse(
      (resp) => resp.url().includes('/api/auth/login') && resp.request().method() === 'POST',
    ),
    page.getByRole('button', { name: 'Se connecter' }).click(),
  ])
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
      // Même raison que questionnaire.spec.ts : contrôle React, check() échoue sur le re-render async.
      await fieldset
        .getByRole('radio', { name: "Non, ce n'est pas en place" })
        .evaluate((el) => (el as HTMLInputElement).click())
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
  // Les actions utilisent un bouton de statut cyclique (4 états : Planifié → En cours →
  // Bloqué → Terminé), pas de case à cocher binaire.
  // timeout: 15 000 ms — en CI la page attend la réponse réseau du GET action-plan avant
  // d'afficher les boutons ; count() est volontairement absent : c'est un snapshot
  // non-retrying, flaky quand React est encore en train de réconcilier au même moment.
  const statusButtons = page.getByRole('button', { name: /Statut :/ })
  await expect(statusButtons.first()).toBeVisible({ timeout: 15_000 })

  // Fait passer la première action jusqu'à "Terminé" (3 clics) et vérifie que l'état survit
  // à un rechargement complet — persisté côté serveur (PATCH + re-fetch), pas état React local.
  // toHaveAccessibleName attend le re-rendu après chaque sauvegarde avant le clic suivant.
  const firstItem = page.locator('li').filter({ has: statusButtons.first() })
  for (const expectedLabel of [/Statut : En cours/, /Statut : Bloqué/, /Statut : Terminé/]) {
    await statusButtons.first().click()
    await expect(statusButtons.first()).toHaveAccessibleName(expectedLabel, { timeout: 10_000 })
  }
  await expect(firstItem.getByText(/Terminée le/)).toBeVisible()

  await page.reload()
  await expect(page.getByRole('heading', { name: "Plan d'actions" })).toBeVisible()
  await expect(page.getByRole('button', { name: /Statut : Terminé/ }).first()).toBeVisible()
})
