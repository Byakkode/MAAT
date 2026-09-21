import {
  type ChangeEvent,
  type DragEvent,
  type FormEvent,
  useEffect,
  useRef,
  useState,
} from 'react'
import {
  Bug,
  CheckCircle2,
  ChevronDown,
  ChevronUp,
  Clock,
  Database,
  HelpCircle,
  Lightbulb,
  MessageCircle,
  Paperclip,
  X,
} from 'lucide-react'
import { ApiError } from '../api/authApi'
import * as supportApi from '../api/supportApi'
import type { TicketDetail, TicketSummary, TicketType } from '../api/supportApi'
import { Card } from '../components/ui/Card'
import { PageHeader } from '../components/ui/PageHeader'

// ─── Constantes ───────────────────────────────────────────────────────────────

const TICKET_TYPES: { type: TicketType; label: string; description: string; icon: typeof Bug }[] = [
  { type: 'bug', label: 'Bug', description: 'Un comportement inattendu ou une erreur', icon: Bug },
  {
    type: 'database',
    label: 'Correction de données',
    description: 'Une inexactitude dans les questions ou recommandations',
    icon: Database,
  },
  {
    type: 'feature',
    label: 'Évolution',
    description: "Une amélioration ou nouvelle fonctionnalité à suggérer",
    icon: Lightbulb,
  },
  { type: 'question', label: 'Question', description: "Une question sur l'utilisation de MAAT", icon: HelpCircle },
]

const TYPE_LABELS: Record<TicketType, string> = {
  bug: 'Bug',
  database: 'Correction de données',
  feature: 'Évolution',
  question: 'Question',
}

const MAX_FILES = 5
const MAX_SIZE_BYTES = 5 * 1024 * 1024
const ALLOWED_EXTENSIONS = new Set([
  '.txt', '.log', '.json', '.csv', '.xml', '.yaml', '.yml',
  '.png', '.jpg', '.jpeg', '.gif', '.webp',
])

// ─── Utilitaires ──────────────────────────────────────────────────────────────

function formatBytes(bytes: number): string {
  if (bytes < 1024) return `${bytes} o`
  if (bytes < 1024 * 1024) return `${(bytes / 1024).toFixed(1)} Ko`
  return `${(bytes / (1024 * 1024)).toFixed(1)} Mo`
}

function getExtension(name: string): string {
  const dot = name.lastIndexOf('.')
  return dot >= 0 ? name.slice(dot).toLowerCase() : ''
}

function formatDate(iso: string): string {
  return new Intl.DateTimeFormat('fr-FR', {
    day: 'numeric',
    month: 'short',
    year: 'numeric',
    hour: '2-digit',
    minute: '2-digit',
  }).format(new Date(iso))
}

// ─── Badge de statut ──────────────────────────────────────────────────────────

function StatusBadge({ state }: { state: 'open' | 'closed' | null }) {
  if (state === 'open') {
    return (
      <span className="inline-flex items-center gap-1 rounded-full bg-blue-maat/10 px-2 py-0.5 text-[11px] font-medium text-blue-maat">
        <Clock size={10} aria-hidden="true" />
        Ouvert
      </span>
    )
  }
  if (state === 'closed') {
    return (
      <span className="inline-flex items-center gap-1 rounded-full bg-green-maat/10 px-2 py-0.5 text-[11px] font-medium text-green-maat-text">
        <CheckCircle2 size={10} aria-hidden="true" />
        Résolu
      </span>
    )
  }
  return (
    <span className="inline-flex items-center rounded-full bg-bg px-2 py-0.5 text-[11px] font-medium text-text-muted">
      Inconnu
    </span>
  )
}

// ─── Vue détail d'un ticket ───────────────────────────────────────────────────

function TicketDetailView({
  ticketId,
  onClose,
}: {
  ticketId: string
  onClose: () => void
}) {
  const [detail, setDetail] = useState<TicketDetail | null>(null)
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState<string | null>(null)

  useEffect(() => {
    let cancelled = false
    setLoading(true)
    setError(null)
    supportApi
      .getTicketDetail(ticketId)
      .then((d) => { if (!cancelled) { setDetail(d); setLoading(false) } })
      .catch((err: unknown) => {
        if (!cancelled) {
          setError(err instanceof ApiError ? err.message : 'Erreur de chargement.')
          setLoading(false)
        }
      })
    return () => { cancelled = true }
  }, [ticketId])

  return (
    <div className="flex flex-col gap-4">
      <div className="flex items-center justify-between">
        <button
          type="button"
          onClick={onClose}
          className="flex items-center gap-1.5 text-[13px] text-text-muted transition-colors hover:text-text"
        >
          <ChevronUp size={14} aria-hidden="true" />
          Retour aux tickets
        </button>
        {detail && (
          <StatusBadge state={detail.state} />
        )}
      </div>

      {loading && (
        <Card>
          <div className="flex items-center justify-center py-10">
            <span className="text-[13px] text-text-muted">Chargement…</span>
          </div>
        </Card>
      )}

      {error && (
        <p role="alert" className="text-[13px] text-red">{error}</p>
      )}

      {detail && (
        <>
          <Card>
            <div className="flex flex-col gap-3">
              <div className="flex items-start justify-between gap-4">
                <h2 className="text-[15px] font-semibold text-text">{detail.title}</h2>
                <span className="shrink-0 text-[11.5px] text-text-muted">
                  #{detail.githubIssueNumber}
                </span>
              </div>
              <div className="flex items-center gap-2">
                <span className="rounded-full border border-border px-2 py-0.5 text-[11px] text-text-muted">
                  {TYPE_LABELS[detail.ticketType]}
                </span>
                <span className="text-[11.5px] text-text-muted">{formatDate(detail.createdAt)}</span>
              </div>
              <div className="rounded-xl bg-bg px-4 py-3">
                <p className="whitespace-pre-wrap text-[13px] leading-relaxed text-text">
                  {detail.description}
                </p>
              </div>
            </div>
          </Card>

          <Card>
            <h3 className="mb-4 text-[11px] font-semibold uppercase tracking-[0.08em] text-text-muted">
              Réponses de l'équipe MAAT
              {detail.comments.length > 0 && (
                <span className="ml-2 rounded-full bg-blue-maat/10 px-1.5 py-0.5 text-[10px] text-blue-maat">
                  {detail.comments.length}
                </span>
              )}
            </h3>

            {detail.comments.length === 0 ? (
              <div className="flex flex-col items-center gap-2 py-6 text-center">
                <MessageCircle size={20} className="text-border" aria-hidden="true" />
                <p className="text-[13px] text-text-muted">Aucune réponse pour l'instant.</p>
                <p className="text-[12px] text-text-muted/70">
                  L'équipe MAAT vous répondra dans les meilleurs délais.
                </p>
              </div>
            ) : (
              <ul className="flex flex-col gap-3">
                {detail.comments.map((comment, i) => (
                  <li key={i} className="flex flex-col gap-1.5 rounded-xl border border-border p-3">
                    <div className="flex items-center justify-between">
                      <span className="text-[12px] font-medium text-text">
                        Équipe MAAT
                        <span className="ml-1.5 text-[11px] font-normal text-text-muted/60">
                          @{comment.authorLogin}
                        </span>
                      </span>
                      <span className="text-[11px] text-text-muted">{formatDate(comment.createdAt)}</span>
                    </div>
                    <p className="whitespace-pre-wrap text-[13px] leading-relaxed text-text">
                      {comment.body}
                    </p>
                  </li>
                ))}
              </ul>
            )}
          </Card>
        </>
      )}
    </div>
  )
}

// ─── Liste des tickets ────────────────────────────────────────────────────────

function TicketListView({ onNewTicket }: { onNewTicket: () => void }) {
  const [tickets, setTickets] = useState<TicketSummary[]>([])
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState<string | null>(null)
  const [selectedId, setSelectedId] = useState<string | null>(null)

  useEffect(() => {
    setLoading(true)
    setError(null)
    supportApi
      .getTickets()
      .then((t) => { setTickets(t); setLoading(false) })
      .catch((err: unknown) => {
        setError(err instanceof ApiError ? err.message : 'Erreur de chargement.')
        setLoading(false)
      })
  }, [])

  if (selectedId) {
    return <TicketDetailView ticketId={selectedId} onClose={() => setSelectedId(null)} />
  }

  if (loading) {
    return (
      <Card>
        <div className="flex items-center justify-center py-10">
          <span className="text-[13px] text-text-muted">Chargement…</span>
        </div>
      </Card>
    )
  }

  if (error) {
    return <p role="alert" className="text-[13px] text-red">{error}</p>
  }

  if (tickets.length === 0) {
    return (
      <Card>
        <div className="flex flex-col items-center gap-3 py-10 text-center">
          <MessageCircle size={24} className="text-border" aria-hidden="true" />
          <p className="text-[14px] font-medium text-text">Aucun ticket pour le moment</p>
          <p className="text-[13px] text-text-muted">
            Votre équipe n'a soumis aucune demande de support.
          </p>
          <button
            type="button"
            onClick={onNewTicket}
            className="mt-1 rounded-xl bg-blue-maat px-4 py-2 text-[13px] font-semibold text-white transition-opacity hover:opacity-90"
          >
            Soumettre un ticket
          </button>
        </div>
      </Card>
    )
  }

  return (
    <Card>
      <ul className="flex flex-col divide-y divide-border">
        {tickets.map((ticket) => (
          <li key={ticket.id}>
            <button
              type="button"
              onClick={() => setSelectedId(ticket.id)}
              className="flex w-full items-start gap-3 py-3.5 text-left transition-colors first:pt-0 last:pb-0 hover:opacity-80"
            >
              <div className="flex flex-1 flex-col gap-1 min-w-0">
                <span className="truncate text-[13.5px] font-medium text-text">
                  {ticket.title}
                </span>
                <div className="flex items-center gap-2">
                  <span className="text-[11px] text-text-muted">
                    {TYPE_LABELS[ticket.ticketType]}
                  </span>
                  <span className="text-[11px] text-text-muted/50">·</span>
                  <span className="text-[11px] text-text-muted">{formatDate(ticket.createdAt)}</span>
                  {ticket.commentsCount > 0 && (
                    <>
                      <span className="text-[11px] text-text-muted/50">·</span>
                      <span className="flex items-center gap-0.5 text-[11px] text-text-muted">
                        <MessageCircle size={10} aria-hidden="true" />
                        {ticket.commentsCount}
                      </span>
                    </>
                  )}
                </div>
              </div>
              <div className="flex shrink-0 items-center gap-2">
                <StatusBadge state={ticket.state} />
                <ChevronDown size={14} className="text-text-muted" aria-hidden="true" />
              </div>
            </button>
          </li>
        ))}
      </ul>
    </Card>
  )
}

// ─── Formulaire de création ───────────────────────────────────────────────────

type FormStatus = 'idle' | 'submitting' | 'error'

function NewTicketForm({ onSuccess }: { onSuccess: () => void }) {
  const [type, setType] = useState<TicketType>('bug')
  const [title, setTitle] = useState('')
  const [description, setDescription] = useState('')
  const [files, setFiles] = useState<File[]>([])
  const [status, setStatus] = useState<FormStatus>('idle')
  const [error, setError] = useState<string | null>(null)
  const [dragOver, setDragOver] = useState(false)
  const fileInputRef = useRef<HTMLInputElement>(null)

  function addFiles(incoming: FileList | File[]) {
    const filtered = Array.from(incoming).filter(
      (f) => ALLOWED_EXTENSIONS.has(getExtension(f.name)) && f.size <= MAX_SIZE_BYTES,
    )
    setFiles((prev) => [...prev, ...filtered].slice(0, MAX_FILES))
  }

  function handleFileChange(e: ChangeEvent<HTMLInputElement>) {
    if (e.target.files) addFiles(e.target.files)
    e.target.value = ''
  }

  function handleDrop(e: DragEvent<HTMLDivElement>) {
    e.preventDefault()
    setDragOver(false)
    if (e.dataTransfer.files) addFiles(e.dataTransfer.files)
  }

  function removeFile(index: number) {
    setFiles((prev) => prev.filter((_, i) => i !== index))
  }

  async function handleSubmit(e: FormEvent<HTMLFormElement>) {
    e.preventDefault()
    setStatus('submitting')
    setError(null)
    try {
      await supportApi.createTicket({ type, title, description, attachments: files })
      onSuccess()
    } catch (err) {
      setError(err instanceof ApiError ? err.message : 'Une erreur inattendue est survenue.')
      setStatus('error')
    }
  }

  return (
    <form onSubmit={(e) => void handleSubmit(e)} noValidate>
      <div className="flex flex-col gap-4">
        <Card as="section" aria-labelledby="ticket-type-heading">
          <h2
            id="ticket-type-heading"
            className="mb-3 text-[11px] font-semibold uppercase tracking-[0.08em] text-text-muted"
          >
            Type de demande
          </h2>
          <div className="grid grid-cols-2 gap-2 sm:grid-cols-4">
            {TICKET_TYPES.map(({ type: t, label, description: desc, icon: Icon }) => (
              <button
                key={t}
                type="button"
                aria-pressed={type === t}
                onClick={() => setType(t)}
                className={`flex flex-col gap-1.5 rounded-xl border p-3 text-left transition-colors focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-blue-maat/40 ${
                  type === t
                    ? 'border-blue-maat bg-blue-maat/5'
                    : 'border-border bg-white text-text-muted hover:border-border-strong hover:text-text'
                }`}
              >
                <Icon
                  size={18}
                  strokeWidth={1.75}
                  aria-hidden="true"
                  className={type === t ? 'text-blue-maat' : 'text-text-muted'}
                />
                <span className={`text-[13px] font-medium ${type === t ? 'text-text' : ''}`}>{label}</span>
                <span className="text-[11.5px] leading-snug text-text-muted">{desc}</span>
              </button>
            ))}
          </div>
        </Card>

        <Card as="section" aria-labelledby="ticket-content-heading">
          <h2
            id="ticket-content-heading"
            className="mb-3 text-[11px] font-semibold uppercase tracking-[0.08em] text-text-muted"
          >
            Détails
          </h2>
          <div className="flex flex-col gap-3">
            <div>
              <label htmlFor="ticket-title" className="mb-1.5 block text-[13px] font-medium text-text">
                Titre <span aria-hidden="true" className="text-red">*</span>
              </label>
              <input
                id="ticket-title"
                type="text"
                required
                maxLength={200}
                value={title}
                onChange={(e) => setTitle(e.target.value)}
                placeholder="Résumez votre demande en une phrase"
                className="w-full rounded-xl border border-border bg-white px-3 py-2.5 text-[13.5px] text-text placeholder:text-text-muted/50 transition-colors focus:border-blue-maat focus:outline-none focus:ring-2 focus:ring-blue-maat/20"
              />
            </div>
            <div>
              <label htmlFor="ticket-description" className="mb-1.5 block text-[13px] font-medium text-text">
                Description <span aria-hidden="true" className="text-red">*</span>
              </label>
              <textarea
                id="ticket-description"
                required
                rows={6}
                maxLength={5000}
                value={description}
                onChange={(e) => setDescription(e.target.value)}
                placeholder="Décrivez votre demande avec précision : étapes pour reproduire un bug, données incorrectes, fonctionnalité souhaitée…"
                className="w-full resize-y rounded-xl border border-border bg-white px-3 py-2.5 text-[13.5px] text-text placeholder:text-text-muted/50 transition-colors focus:border-blue-maat focus:outline-none focus:ring-2 focus:ring-blue-maat/20"
              />
              <p aria-live="polite" className="mt-1 text-right text-[11.5px] tabular-nums text-text-muted/70">
                {description.length}/5 000
              </p>
            </div>
          </div>
        </Card>

        <Card as="section" aria-labelledby="ticket-attachments-heading">
          <h2
            id="ticket-attachments-heading"
            className="mb-1 text-[11px] font-semibold uppercase tracking-[0.08em] text-text-muted"
          >
            Pièces jointes{' '}
            <span className="font-normal normal-case">(optionnel, max. {MAX_FILES} fichiers · 5 Mo chacun)</span>
          </h2>
          <p className="mb-3 text-[12px] text-text-muted">
            Images (PNG, JPG, GIF, WEBP) ou fichiers texte (TXT, LOG, JSON, CSV…)
          </p>

          <div
            role="button"
            tabIndex={files.length >= MAX_FILES ? -1 : 0}
            aria-label="Zone de dépôt de fichiers — cliquer ou glisser-déposer"
            aria-disabled={files.length >= MAX_FILES}
            onClick={() => files.length < MAX_FILES && fileInputRef.current?.click()}
            onKeyDown={(e) => {
              if ((e.key === 'Enter' || e.key === ' ') && files.length < MAX_FILES)
                fileInputRef.current?.click()
            }}
            onDrop={handleDrop}
            onDragOver={(e) => { e.preventDefault(); setDragOver(true) }}
            onDragLeave={() => setDragOver(false)}
            className={`flex cursor-pointer flex-col items-center justify-center gap-2 rounded-xl border-2 border-dashed px-4 py-6 text-center transition-colors focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-blue-maat/40 ${
              files.length >= MAX_FILES
                ? 'cursor-default opacity-50'
                : dragOver
                  ? 'border-blue-maat bg-blue-maat/5'
                  : 'border-border hover:border-border-strong hover:bg-bg/50'
            }`}
          >
            <Paperclip size={20} className="text-text-muted" aria-hidden="true" />
            <p className="text-[13px] text-text-muted">
              Glissez-déposez ou <span className="text-blue-maat">cliquez pour choisir</span>
            </p>
          </div>

          <input
            ref={fileInputRef}
            type="file"
            multiple
            accept=".txt,.log,.json,.csv,.xml,.yaml,.yml,.png,.jpg,.jpeg,.gif,.webp"
            onChange={handleFileChange}
            className="sr-only"
            aria-hidden="true"
            tabIndex={-1}
          />

          {files.length > 0 && (
            <ul className="mt-3 flex flex-col gap-1.5" aria-label="Fichiers sélectionnés">
              {files.map((file, i) => (
                <li key={`${file.name}-${i}`} className="flex items-center gap-2 rounded-lg bg-bg px-3 py-2">
                  <span className="flex-1 truncate text-[13px] text-text">{file.name}</span>
                  <span className="shrink-0 tabular-nums text-[11.5px] text-text-muted">
                    {formatBytes(file.size)}
                  </span>
                  <button
                    type="button"
                    onClick={() => removeFile(i)}
                    aria-label={`Retirer ${file.name}`}
                    className="shrink-0 rounded p-0.5 text-text-muted transition-colors hover:bg-border hover:text-text"
                  >
                    <X size={14} strokeWidth={1.75} aria-hidden="true" />
                  </button>
                </li>
              ))}
            </ul>
          )}
        </Card>

        {status === 'error' && error && (
          <p role="alert" className="text-[13px] text-red">{error}</p>
        )}

        <div className="flex justify-end">
          <button
            type="submit"
            disabled={!title.trim() || !description.trim() || status === 'submitting'}
            className="rounded-xl bg-blue-maat px-5 py-2.5 text-[13.5px] font-semibold text-white shadow-button-primary transition-opacity disabled:cursor-not-allowed disabled:opacity-50"
          >
            {status === 'submitting' ? 'Envoi en cours…' : 'Envoyer le ticket'}
          </button>
        </div>
      </div>
    </form>
  )
}

// ─── Page principale ──────────────────────────────────────────────────────────

type Tab = 'form' | 'list'

export function SupportPage() {
  const [activeTab, setActiveTab] = useState<Tab>('form')

  function handleSuccess() {
    setActiveTab('list')
  }

  return (
    <div className="flex flex-col gap-5">
      <PageHeader title="Support" />

      {/* Onglets */}
      <div className="flex gap-1 rounded-xl border border-border bg-white p-1 w-fit">
        <button
          type="button"
          onClick={() => setActiveTab('form')}
          className={`rounded-lg px-4 py-1.5 text-[13px] font-medium transition-colors ${
            activeTab === 'form'
              ? 'bg-blue-maat text-white shadow-sm'
              : 'text-text-muted hover:text-text'
          }`}
        >
          Nouveau ticket
        </button>
        <button
          type="button"
          onClick={() => setActiveTab('list')}
          className={`rounded-lg px-4 py-1.5 text-[13px] font-medium transition-colors ${
            activeTab === 'list'
              ? 'bg-blue-maat text-white shadow-sm'
              : 'text-text-muted hover:text-text'
          }`}
        >
          Mes tickets
        </button>
      </div>

      {activeTab === 'form' && <NewTicketForm onSuccess={handleSuccess} />}
      {activeTab === 'list' && <TicketListView onNewTicket={() => setActiveTab('form')} />}
    </div>
  )
}
