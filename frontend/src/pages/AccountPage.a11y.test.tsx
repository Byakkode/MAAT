import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { render, screen } from '@testing-library/react'
import { axe } from 'vitest-axe'

const accountApi = vi.hoisted(() => ({ getCurrentUser: vi.fn() }))
vi.mock('../api/accountApi', () => accountApi)

import { AccountPage } from './AccountPage'
import { useAuthStore } from '../store/authStore'
import { makeCurrentUser, resetCurrentUserStore } from '../test/currentUserFixtures'

describe('AccountPage (axe)', () => {
  beforeEach(() => {
    resetCurrentUserStore()
    accountApi.getCurrentUser.mockReset().mockResolvedValue(makeCurrentUser())
    useAuthStore.setState({ status: 'authenticated', user: { userId: 'u-1', companyId: 'c-1', role: 'Admin' }, error: null })
  })

  afterEach(() => {
    vi.restoreAllMocks()
  })

  it('aucune violation détectable', async () => {
    const { container } = render(<AccountPage />)
    await screen.findByRole('heading', { name: 'Identité' })

    expect(await axe(container)).toHaveNoViolations()
  })
})
