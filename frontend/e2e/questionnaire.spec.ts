import { expect, test } from '@playwright/test'

// docs/specs/questionnaire.md. La création de diagnostic (POST /api/diagnostics) n'est pas
// câblée au frontend dans ce lot (seuls GET .../questions, PUT .../responses/{code},
// POST .../complete et GET .../current le sont) : ce test seed l'inscription, la connexion et
// le diagnostic directement via l'API (page.request partage les cookies du contexte
// navigateur, donc le cookie de refresh posé par /api/auth/login est bien présent quand la
// page se charge ensuite), puis exerce le vrai parcours utilisateur — jusqu'à la complétion —
// à travers l'interface.
const API_URL = 'http://localhost:5130'

test('parcours complet du questionnaire jusqu’à la complétion', async ({ page }) => {
  const suffix = Math.random().toString(36).slice(2, 10)
  const email = `e2e-questionnaire-${suffix}@maat-test.local`
  const password = 'MotDePasseValide2026!'

  await page.request.post(`${API_URL}/api/auth/register`, {
    data: {
      email,
      password,
      companyName: `Entreprise E2E ${suffix}`,
      sectorCode: '6201Z',
      sizeRange: 'Micro',
      region: 'Île-de-France',
    },
  })

  const loginResponse = await page.request.post(`${API_URL}/api/auth/login`, {
    data: { email, password },
  })
  const { accessToken } = (await loginResponse.json()) as { accessToken: string }

  await page.request.post(`${API_URL}/api/diagnostics`, {
    headers: { Authorization: `Bearer ${accessToken}` },
    data: {},
  })

  await page.goto('/questionnaire')

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
