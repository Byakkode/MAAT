import { describe, expect, it, vi } from 'vitest'
import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { MemoryRouter } from 'react-router-dom'
import { ActionPlanCard } from './ActionPlanCard'
import { makeActionPlan, makeRecommendation } from '../../test/dashboardFixtures'

function renderCard(canEdit: boolean, onToggle = vi.fn()) {
  const actionPlan = makeActionPlan({
    items: [
      makeRecommendation({ code: 'REC-ENV-01', actionText: 'Suivre les émissions.', priorityRank: 1, isCompleted: false }),
      makeRecommendation({ code: 'REC-SOC-01', actionText: 'Former les salariés.', priorityRank: 2, isCompleted: true }),
    ],
    totalCount: 24,
    completedCount: 3,
  })

  render(
    <MemoryRouter>
      <ActionPlanCard actionPlan={actionPlan} canEdit={canEdit} togglingCode={null} onToggle={onToggle} />
    </MemoryRouter>,
  )

  return onToggle
}

// docs/specs/dashboard.md, section 6.
describe('ActionPlanCard', () => {
  it('affiche le taux d’avancement global du plan, pas seulement des cinq éléments visibles', () => {
    renderCard(true)

    expect(screen.getByText(/3/)).toBeDefined()
    expect(screen.getByText(/24/)).toBeDefined()
  })

  it('propose un lien vers la liste complète', () => {
    renderCard(true)

    const link = screen.getByRole('link', { name: /plan d.actions|liste complète/i })
    expect(link.getAttribute('href')).toBe('/plan-actions')
  })

  it('Admin/User : les cases sont actionnables et déclenchent onToggle', async () => {
    const user = userEvent.setup()
    const onToggle = renderCard(true)

    const checkbox = screen.getByRole('checkbox', { name: /Suivre les émissions/ }) as HTMLInputElement
    expect(checkbox.disabled).toBe(false)

    await user.click(checkbox)

    expect(onToggle).toHaveBeenCalledWith('REC-ENV-01', true)
  })

  it('cas 19 : Viewer, les cases sont présentes mais non actionnables', async () => {
    const user = userEvent.setup()
    const onToggle = renderCard(false)

    const checkbox = screen.getByRole('checkbox', { name: /Suivre les émissions/ }) as HTMLInputElement
    expect(checkbox.disabled).toBe(true)

    await user.click(checkbox)

    expect(onToggle).not.toHaveBeenCalled()
  })

  it('rappelle qu’une case cochée ne modifie pas le score', () => {
    renderCard(true)

    expect(screen.getByText(/ne modifie pas le score|prochain diagnostic/i)).toBeDefined()
  })
})
