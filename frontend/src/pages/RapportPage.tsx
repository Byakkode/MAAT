import { CheckCircle, FileText } from 'lucide-react'
import { useEffect } from 'react'
import { Link } from 'react-router-dom'
import { useEntitlements } from '../billing/entitlements'
import { UpgradeNotice } from '../components/billing/UpgradeNotice'
import { ReportDownloadButton } from '../components/report/ReportDownloadButton'
import { ReportLogoCard } from '../components/report/ReportLogoCard'
import { Card } from '../components/ui/Card'
import { PageHeader } from '../components/ui/PageHeader'
import { buttonLinkClass } from '../components/ui/buttonStyles'
import { roundScoreForDisplay } from '../constants/scoreLabels'
import { useDashboardStore } from '../store/dashboardStore'

// Sections réelles du document (docs/specs/rapport-pdf.md, section 4).
const REPORT_SECTIONS = [
  'Synthèse : score global, profil des cinq domaines, points forts et axes de progrès',
  'Évolution de votre score depuis vos diagnostics précédents',
  "Plan d'actions priorisé et son avancement",
  'Informations de durabilité B1 à B11 de la norme volontaire (ex-VSME), avec la déclaration de conformité une fois complètes',
  'Méthode de calcul détaillée, pour que votre lecteur puisse refaire le calcul',
]

// docs/specs/abonnement.md, section 8 : rapport de l'offre Starter.
const STARTER_REPORT_SECTIONS = [
  'Identité de votre entreprise et date du diagnostic',
  'Score RSE global et son niveau',
  "Mentions : auto-évaluation déclarative, ni certification ni audit",
]

function formatDate(iso: string): string {
  return new Date(iso).toLocaleDateString('fr-FR', { day: 'numeric', month: 'long', year: 'numeric' })
}

// docs/specs/rapport-pdf.md, section 6 : bouton de téléchargement sur la page de résultat
// d'un diagnostic. Réutilise useDashboardStore (déjà chargé dans le parcours normal depuis
// DashboardPage) plutôt qu'un appel réseau dédié : latestDiagnostic porte déjà l'identifiant
// du diagnostic dont ce rapport est le compte-rendu.
export function RapportPage() {
  const load = useDashboardStore((s) => s.load)
  const loadStatus = useDashboardStore((s) => s.loadStatus)
  const loadError = useDashboardStore((s) => s.loadError)
  const latestDiagnostic = useDashboardStore((s) => s.latestDiagnostic)
  const { fullReport } = useEntitlements()

  useEffect(() => {
    void load()
  }, [load])

  if (loadStatus === 'idle' || loadStatus === 'loading') {
    return (
      <p role="status" className="text-text-muted">
        Chargement…
      </p>
    )
  }

  if (loadStatus === 'error') {
    return (
      <p role="alert" className="text-red">
        {loadError}
      </p>
    )
  }

  if (!latestDiagnostic) {
    return (
      <div className="flex flex-col gap-4">
        <PageHeader title="Rapports" />
        <Card as="section" className="flex flex-col items-center py-12 text-center">
          <FileText className="mb-4 h-12 w-12 text-border" aria-hidden />
          <h2 className="mb-2 text-lg font-semibold text-text">Aucun rapport disponible</h2>
          <p className="mb-6 max-w-sm text-sm text-text-muted">
            Terminez votre premier questionnaire RSE pour générer votre rapport PDF selon la norme volontaire (ex-VSME).
          </p>
          <Link to="/questionnaire" className={buttonLinkClass()}>
            Commencer le questionnaire
          </Link>
        </Card>
        <ReportLogoCard />
      </div>
    )
  }

  const rounded = roundScoreForDisplay(latestDiagnostic.globalScore)

  return (
    <div className="flex flex-col gap-4">
      <PageHeader title="Rapports" />

      <Card as="section" className="flex flex-col gap-6">
        {/* Métadonnées du diagnostic */}
        <div className="grid grid-cols-1 gap-3 sm:grid-cols-3">
          <div className="rounded-lg bg-bg px-4 py-3">
            <p className="mb-0.5 text-xs text-text-muted">Date du diagnostic</p>
            <p className="text-sm font-semibold text-text">{formatDate(latestDiagnostic.completedAt)}</p>
          </div>
          <div className="rounded-lg bg-bg px-4 py-3">
            <p className="mb-0.5 text-xs text-text-muted">Secteur NAF</p>
            <p className="text-sm font-semibold text-text">{latestDiagnostic.sectorCode}</p>
          </div>
          <div className="rounded-lg bg-kpi-blue px-4 py-3">
            <p className="mb-0.5 text-xs text-text-muted">Score RSE global</p>
            <p className="text-sm font-semibold text-blue-maat">{rounded} / 100</p>
          </div>
        </div>

        {/* Contenu du rapport */}
        <div>
          <h2 className="mb-3 text-base font-semibold text-text">Ce rapport contient</h2>
          <ul className="flex flex-col gap-2">
            {(fullReport ? REPORT_SECTIONS : STARTER_REPORT_SECTIONS).map((section) => (
              <li key={section} className="flex items-start gap-2.5 text-sm text-text-muted">
                <CheckCircle size={15} className="mt-0.5 shrink-0 text-green-maat" aria-hidden />
                {section}
              </li>
            ))}
          </ul>
          {!fullReport && (
            <div className="mt-4">
              <UpgradeNotice compact requiredPlan="Essential" title="Le rapport complet">
                Scores par domaine, évolution, plan d&apos;actions et méthode de calcul : le document
                à transmettre à vos clients et partenaires.
              </UpgradeNotice>
            </div>
          )}
        </div>

        {/* Téléchargement */}
        <div className="border-t border-border pt-4">
          <p className="mb-3 text-sm text-text-muted">
            Le rapport est généré à la demande en format PDF. Aucun stockage permanent, conformément à notre politique de données souveraines.
          </p>
          <ReportDownloadButton diagnosticId={latestDiagnostic.id} />
        </div>
      </Card>

      <ReportLogoCard />
    </div>
  )
}
