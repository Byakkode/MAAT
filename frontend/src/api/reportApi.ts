import { apiFetch } from './httpClient'
import { ApiError } from './authApi'

async function readErrorMessage(response: Response, fallback: string): Promise<string> {
  const body = await response.json().catch(() => null)
  return (body as { message?: string } | null)?.message ?? fallback
}

// docs/specs/rapport-pdf.md, section 2 : nom de fichier déterministe porté par
// Content-Disposition, exposé côté navigateur via CORS (Program.cs, WithExposedHeaders).
function extractFileName(contentDisposition: string | null): string {
  if (!contentDisposition) {
    return 'rapport.pdf'
  }
  const starMatch = /filename\*=UTF-8''([^;]+)/i.exec(contentDisposition)
  if (starMatch) {
    return decodeURIComponent(starMatch[1])
  }
  const plainMatch = /filename="?([^";]+)"?/i.exec(contentDisposition)
  return plainMatch ? plainMatch[1] : 'rapport.pdf'
}

// docs/specs/rapport-pdf.md, section 6 : requête authentifiée en blob, jamais un <a href>
// direct — l'access token vit en mémoire JS (auth-securite-rgpd.md, section 2) et
// n'accompagne pas une navigation classique. Le blob est ensuite sauvegardé via un lien
// temporaire, jamais affiché dans un nouvel onglet non authentifié.
export async function downloadReport(diagnosticId: string): Promise<void> {
  const response = await apiFetch(`/api/diagnostics/${diagnosticId}/report`)
  if (!response.ok) {
    throw new ApiError(await readErrorMessage(response, 'Impossible de générer le rapport.'), response.status)
  }

  const blob = await response.blob()
  const fileName = extractFileName(response.headers.get('Content-Disposition'))

  const url = URL.createObjectURL(blob)
  try {
    const link = document.createElement('a')
    link.href = url
    link.download = fileName
    document.body.appendChild(link)
    link.click()
    link.remove()
  } finally {
    URL.revokeObjectURL(url)
  }
}
