import { afterEach, describe, expect, it, vi } from 'vitest'
import { render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { ApiError } from '../../api/authApi'

const accountApi = vi.hoisted(() => ({ getCurrentUser: vi.fn() }))
vi.mock('../../api/accountApi', () => accountApi)

const reportApi = vi.hoisted(() => ({ downloadReport: vi.fn() }))
vi.mock('../../api/reportApi', () => reportApi)

import { ReportDownloadButton } from './ReportDownloadButton'

// docs/specs/rapport-pdf.md, section 6.
describe('ReportDownloadButton', () => {
  afterEach(() => {
    vi.restoreAllMocks()
    accountApi.getCurrentUser.mockReset()
    reportApi.downloadReport.mockReset()
  })

  it('déclenche le téléchargement authentifié quand le compte est vérifié', async () => {
    accountApi.getCurrentUser.mockResolvedValue({ userId: 'u1', companyId: 'c1', role: 'Admin', emailVerified: true })
    reportApi.downloadReport.mockResolvedValue(undefined)

    render(<ReportDownloadButton diagnosticId="diag-1" />)

    const button = (await screen.findByRole('button', { name: /télécharger le rapport pdf/i })) as HTMLButtonElement
    expect(button.disabled).toBe(false)

    await userEvent.click(button)

    await waitFor(() => expect(reportApi.downloadReport).toHaveBeenCalledWith('diag-1'))
    await waitFor(() => expect(button.disabled).toBe(false))
  })

  it('désactive le bouton et explique la démarche quand le compte n’est pas vérifié', async () => {
    accountApi.getCurrentUser.mockResolvedValue({ userId: 'u1', companyId: 'c1', role: 'Admin', emailVerified: false })

    render(<ReportDownloadButton diagnosticId="diag-1" />)

    const button = (await screen.findByRole('button', { name: /télécharger le rapport pdf/i })) as HTMLButtonElement
    await waitFor(() => expect(button.disabled).toBe(true))
    expect(screen.getByText(/adresse e-mail doit être vérifiée/i)).toBeDefined()
    expect(reportApi.downloadReport).not.toHaveBeenCalled()
  })

  it('affiche un message d’erreur exploitable si la génération échoue', async () => {
    accountApi.getCurrentUser.mockResolvedValue({ userId: 'u1', companyId: 'c1', role: 'Admin', emailVerified: true })
    reportApi.downloadReport.mockRejectedValue(new ApiError('Ce diagnostic n’est plus en cours.', 409))

    render(<ReportDownloadButton diagnosticId="diag-1" />)

    const button = (await screen.findByRole('button', { name: /télécharger le rapport pdf/i })) as HTMLButtonElement
    await userEvent.click(button)

    const alert = await screen.findByRole('alert')
    expect(alert.textContent).toMatch(/n’est plus en cours/i)
    expect(button.disabled).toBe(false)
  })
})
