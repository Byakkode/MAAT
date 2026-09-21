import { apiFetch } from './httpClient'
import { ApiError } from './authApi'

export type TicketType = 'bug' | 'database' | 'feature' | 'question'

export interface CreateTicketPayload {
  type: TicketType
  title: string
  description: string
  attachments: File[]
}

export interface TicketSummary {
  id: string
  title: string
  ticketType: TicketType
  githubIssueNumber: number
  createdAt: string
  state: 'open' | 'closed' | null
  commentsCount: number
}

export interface TicketComment {
  authorLogin: string
  body: string
  createdAt: string
}

export interface TicketDetail {
  id: string
  title: string
  ticketType: TicketType
  description: string
  githubIssueNumber: number
  createdAt: string
  state: 'open' | 'closed' | null
  comments: TicketComment[]
}

async function readErrorMessage(response: Response, fallback: string): Promise<string> {
  const body = await response.json().catch(() => null)
  return (body as { message?: string } | null)?.message ?? fallback
}

export async function createTicket(payload: CreateTicketPayload): Promise<{ ticketId: string }> {
  const form = new FormData()
  form.append('type', payload.type)
  form.append('title', payload.title)
  form.append('description', payload.description)
  for (const file of payload.attachments) {
    form.append('attachments', file)
  }

  // Pas de Content-Type explicite : httpClient.ts détecte FormData et laisse le navigateur
  // poser lui-même l'en-tête multipart avec la bonne frontière (boundary).
  const response = await apiFetch('/api/support/tickets', {
    method: 'POST',
    body: form,
  })

  if (!response.ok) {
    throw new ApiError(await readErrorMessage(response, 'Impossible de créer le ticket.'), response.status)
  }

  return response.json() as Promise<{ ticketId: string }>
}

export async function getTickets(): Promise<TicketSummary[]> {
  const response = await apiFetch('/api/support/tickets')
  if (!response.ok) {
    throw new ApiError(await readErrorMessage(response, 'Impossible de charger les tickets.'), response.status)
  }
  return response.json() as Promise<TicketSummary[]>
}

export async function getTicketDetail(id: string): Promise<TicketDetail> {
  const response = await apiFetch(`/api/support/tickets/${id}`)
  if (!response.ok) {
    throw new ApiError(await readErrorMessage(response, 'Impossible de charger le ticket.'), response.status)
  }
  return response.json() as Promise<TicketDetail>
}
