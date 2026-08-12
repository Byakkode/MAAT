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

  // React Router bascule côté client : le clic déclenche la navigation, mais la page
  // précédente (Inscription) ne se démonte qu'au rendu suivant. Une saisie qui arrive
  // avant ce rendu remplit silencieusement le champ homonyme de l'écran sur le point de
  // disparaître (Adresse e-mail/Mot de passe existent sur les deux écrans) — d'où l'ancre
  // sur le titre "Connexion", absent de la page d'inscription, avant toute interaction.
  // Même principe à chaque transition d'écran ci-dessous (CLAUDE.md, section Frontend).
  await page.getByRole('link', { name: 'Se connecter' }).click()
  await expect(page.getByRole('heading', { name: 'Connexion' })).toBeVisible()
  await page.getByLabel('Adresse e-mail').fill(email)
  await page.getByLabel('Mot de passe').fill(password)
  await page.getByRole('button', { name: 'Se connecter' }).click()
  await expect(page).toHaveURL(/\/dashboard$/)
  // exact: true — "Tableau de bord" est autrement un sous-texte de "Bienvenue sur votre
  // tableau de bord" (h2 affiché pour un compte sans diagnostic), qui ferait échouer le
  // localisateur en mode strict (deux titres correspondraient).
  await expect(page.getByRole('heading', { name: 'Tableau de bord', exact: true })).toBeVisible()

  await page.getByRole('link', { name: 'Questionnaire', exact: true }).click()
  await expect(page.getByRole('heading', { name: 'Questionnaire' })).toBeVisible()

  // docs/specs/questionnaire.md, section 5 : un compte neuf n'a aucun diagnostic en cours —
  // le 404 de GET /current doit produire cette invitation, jamais un message d'erreur. Le
  // titre "Questionnaire" ci-dessus ne s'affiche qu'une fois ce GET résolu (QuestionnairePage
  // rend un simple indicateur de chargement tant que loadStatus vaut "idle"/"loading"), donc
  // attendre ce titre prouve aussi que la réponse de GET /api/diagnostics/current est arrivée
  // avant de cliquer sur « Démarrer un diagnostic » ci-dessous.
  await expect(page.getByText("Vous n'avez pas de diagnostic en cours.")).toBeVisible()

  await page.getByRole('button', { name: 'Démarrer un diagnostic' }).click()
  const stepIndicator = page.getByRole('status').filter({ hasText: 'Étape' })
  await expect(stepIndicator).toBeVisible()

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

    // Pas de changement de route ici (QuestionStep se re-rend dans la même page), mais le
    // même risque : "Suivant" ne démonte l'étape courante qu'au rendu suivant, et le libellé
    // du bouton radio ("Pleinement en place et suivi") est identique sur toutes les étapes —
    // un simple `toBeVisible()` sur l'indicateur d'étape resterait vrai avant ET après (même
    // élément, texte pas encore mis à jour), donc ne prouverait rien. Attendre que son texte
    // change force à attendre la vraie étape suivante avant que la boucle ne requête ses
    // fieldsets (sans quoi elle risque de cocher les radios de l'étape sur le point de
    // disparaître).
    const previousStepLabel = (await stepIndicator.textContent()) ?? ''
    await page.getByRole('button', { name: 'Suivant' }).click()
    await expect(stepIndicator).not.toHaveText(previousStepLabel)
  }

  expect(completed).toBe(true)
  await expect(page.getByText('Diagnostic complété.')).toBeVisible()
})
