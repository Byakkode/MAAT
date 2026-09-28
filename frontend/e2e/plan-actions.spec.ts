import { expect, test } from '@playwright/test'

// docs/specs/recommandations.md, section 4. Aucun parcours Playwright ne visitait jamais
// l'écran "Plan d'actions" avant ce test — l'entrée de navigation et le lien "Voir tout le
// plan d'actions" du tableau de bord y menaient, mais la page elle-même n'était qu'un
// placeholder muet, sans appel réseau ni résolution d'identifiant de diagnostic.

test('compte sans diagnostic complété → Plan d’actions affiche une invitation, pas un écran vide', async ({ page }) => {
  const suffix = Math.random().toString(36).slice(2, 10)
  const email = `e2e-plan-actions-vide-${suffix}@maat-test.local`
  const password = 'MotDePasseValide2026!'

  // Offre Starter choisie d'avance, comme depuis la page d'accueil : ce parcours porte sur
  // l'application, pas sur l'écran de sélection d'abonnement (voir abonnement.spec.ts).
  await page.goto('/register?offre=starter')
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
  await expect(page).toHaveURL('/tableau-de-bord')
  // Animation d'arrivée après connexion (LoginIntro.tsx) : plein écran, elle intercepte les
  // clics pendant sa lecture. On vérifie qu'elle s'affiche puis qu'elle se ferme sur « Passer ».
  const intro = page.getByRole('status', { name: /connexion réussie/i })
  await expect(intro).toBeVisible()
  await intro.getByRole('button', { name: 'Passer' }).click()
  await expect(intro).toBeHidden()
  await expect(page.getByRole('heading', { name: 'Tableau de bord', exact: true })).toBeVisible()

  await page.getByRole('link', { name: "Plan d'actions", exact: true }).click()
  await expect(page.getByRole('heading', { name: "Plan d'actions" })).toBeVisible()
  await expect(page.getByText("Vous n'avez pas encore de diagnostic complété.")).toBeVisible()

  // Le lien fonctionne réellement, pas seulement un texte d'invitation décoratif.
  await page.getByRole('link', { name: 'Commencer le questionnaire' }).click()
  await expect(page.getByRole('heading', { name: 'Questionnaire' })).toBeVisible()
})

test('Starter : diagnostic complété avec des réponses faibles → trois actions en lecture seule, nouveau diagnostic bloqué', async ({ page }) => {
  // Même raison que questionnaire.spec.ts : 45 questions, chacune un cycle debounce (500 ms) +
  // aller-retour réseau ; ce test répond en plus à toutes, comme celui-là, puis exerce l'écran
  // Plan d'actions par-dessus.
  test.setTimeout(150_000)

  const suffix = Math.random().toString(36).slice(2, 10)
  const email = `e2e-plan-actions-${suffix}@maat-test.local`
  const password = 'MotDePasseValide2026!'

  // Offre Starter choisie d'avance, comme depuis la page d'accueil : ce parcours porte sur
  // l'application, pas sur l'écran de sélection d'abonnement (voir abonnement.spec.ts).
  await page.goto('/register?offre=starter')
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
  await expect(page).toHaveURL('/tableau-de-bord')
  // Animation d'arrivée après connexion : voir le premier test de ce fichier.
  await page.getByRole('button', { name: 'Passer' }).click()
  await expect(page.getByRole('status', { name: /connexion réussie/i })).toBeHidden()

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

  // docs/specs/abonnement.md, section 8 : compte Starter. Les trois premières actions du
  // classement, en lecture seule (le suivi est réservé aux offres payantes), et l'invitation à
  // l'offre supérieure qui annonce les actions masquées. Le suivi enrichi (menu de statut,
  // formulaire du détail) relève de Professional, que ce parcours ne peut pas atteindre sans
  // paiement Stripe : il est couvert par PlanLimitsTests (API), planLimits.test.tsx et
  // PlanActionsPage.test.tsx (écran). Ici, l'étiquette de statut s'affiche seule, sans menu.
  // timeout: 15 000 ms — la page attend la réponse réseau du GET action-plan avant d'afficher
  // les actions ; count() est volontairement absent (snapshot non-retrying).
  // Texte complet de l'étiquette, préfixe réservé aux lecteurs d'écran compris : Playwright
  // compare le texte entier de l'élément (« Statut : Planifié »), pas son seul texte propre.
  const statusLabels = page.getByText('Statut : Planifié', { exact: true })
  await expect(statusLabels.first()).toBeVisible({ timeout: 15_000 })
  await expect(statusLabels).toHaveCount(3)
  await expect(page.getByRole('button', { name: /^Statut/ })).toHaveCount(0)
  await expect(page.getByText(/autres actions recommandées pour votre entreprise/)).toBeVisible()
  await expect(page.getByText(/Inclus à partir de l'offre Essential/).first()).toBeVisible()

  // docs/specs/recommandations.md, section 4 bis : l'historique du suivi est réservé à
  // Professional — absent du détail d'une action en Starter.
  await page.getByRole('button', { name: 'Voir les détails' }).first().click()
  await expect(page.getByRole('button', { name: 'Réduire les détails' })).toBeVisible()
  await expect(page.getByRole('button', { name: "Voir l'historique" })).toHaveCount(0)

  // L'unique évaluation du Starter est consommée : le questionnaire n'en propose plus.
  await page.getByRole('link', { name: 'Diagnostic', exact: true }).click()
  await expect(page.getByRole('heading', { name: 'Questionnaire RSE' })).toBeVisible()
  await expect(page.getByText('Vous avez réalisé votre diagnostic')).toBeVisible()
  await expect(page.getByRole('button', { name: 'Démarrer un diagnostic' })).toHaveCount(0)
})
