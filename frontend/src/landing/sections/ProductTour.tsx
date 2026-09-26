import { useRef, useState, type KeyboardEvent } from 'react'
import { AnimatePresence, motion } from 'framer-motion'
import { ActionPlanMock, QuestionnaireMock, RecommendationsMock, ReportMock } from '../mocks/ProductMocks'
import { Container, Reveal, SectionLabel } from '../primitives'
import { EASE_OUT } from '../motion'

const TABS = [
  {
    id: 'diagnostic',
    label: 'Diagnostic',
    title: 'Une question à la fois, jamais de jargon.',
    body: "Une échelle en six paliers décrits en toutes lettres, plutôt qu’une note abstraite qui invite aux réponses de complaisance. Le temps restant est estimé en continu.",
    Mock: QuestionnaireMock,
  },
  {
    id: 'recommandations',
    label: 'Recommandations',
    title: 'Ce qui fait gagner le plus de points, en premier.',
    body: "Chaque recommandation indique son effet estimé sur votre score et l’effort qu’elle demande. Les actions rapides et rentables remontent d’elles-mêmes.",
    Mock: RecommendationsMock,
  },
  {
    id: 'plan',
    label: "Plan d’actions",
    title: 'Un responsable, une échéance, un statut.',
    body: "Planifié, en cours, bloqué, terminé : l’avancement se lit d’un coup d’œil, et les notes s’enregistrent automatiquement. Le tableau de bord suit la progression d’un diagnostic à l’autre.",
    Mock: ActionPlanMock,
  },
  {
    id: 'rapport',
    label: 'Rapport VSME',
    title: 'Un rapport que vos clients savent lire.',
    body: 'Page de garde, synthèse, scores par domaine, plan d’actions et évolution, structurés selon le standard volontaire européen pour les PME. Régénéré à chaque téléchargement, jamais stocké.',
    Mock: ReportMock,
  },
] as const

export function ProductTour() {
  const [active, setActive] = useState(0)
  const tabRefs = useRef<(HTMLButtonElement | null)[]>([])
  const tab = TABS[active]

  // Motif « onglets » WAI-ARIA : flèches gauche/droite, Début et Fin déplacent le focus et
  // activent l'onglet ; un seul onglet est atteignable par Tab (tabIndex itinérant).
  function onKeyDown(e: KeyboardEvent<HTMLDivElement>) {
    const last = TABS.length - 1
    const next = { ArrowRight: active === last ? 0 : active + 1, ArrowLeft: active === 0 ? last : active - 1, Home: 0, End: last }[e.key]
    if (next === undefined) return
    e.preventDefault()
    setActive(next)
    tabRefs.current[next]?.focus()
  }

  return (
    <section id="produit" aria-labelledby="produit-title" className="border-y border-border bg-bg py-24 sm:py-32">
      <Container>
        <Reveal className="grid gap-6 lg:grid-cols-12 lg:items-end">
          <div className="lg:col-span-7">
            <SectionLabel index="02">Produit</SectionLabel>
            <h2 id="produit-title" className="display mt-6 text-[clamp(2.25rem,4.4vw,3.5rem)] text-text">
              Un outil de pilotage, pas un questionnaire de plus.
            </h2>
          </div>
        </Reveal>

        <div role="tablist" aria-label="Écrans de l’application" onKeyDown={onKeyDown} className="mt-14 flex gap-1 overflow-x-auto border-b border-border-strong/70">
          {TABS.map((t, i) => {
            const selected = i === active
            return (
              <button
                key={t.id}
                ref={(el) => { tabRefs.current[i] = el }}
                id={`tab-${t.id}`}
                role="tab"
                type="button"
                aria-selected={selected}
                aria-controls={`panel-${t.id}`}
                tabIndex={selected ? 0 : -1}
                onClick={() => setActive(i)}
                className={`relative shrink-0 cursor-pointer px-4 py-3.5 font-heading text-[15px] font-medium transition-colors ${selected ? 'text-text' : 'text-text-muted hover:text-text'}`}
              >
                <span className="numeric mr-2 text-[12px] text-text-muted">{String(i + 1).padStart(2, '0')}</span>
                {t.label}
                {selected && (
                  <motion.span layoutId="tab-underline" className="absolute inset-x-0 -bottom-px h-0.5 bg-blue-maat" transition={{ duration: 0.5, ease: EASE_OUT }} />
                )}
              </button>
            )
          })}
        </div>

        <div id={`panel-${tab.id}`} role="tabpanel" aria-labelledby={`tab-${tab.id}`} className="mt-12 grid gap-10 lg:min-h-[35rem] lg:grid-cols-12 lg:items-center">
          <AnimatePresence mode="wait" initial={false}>
            <motion.div
              key={tab.id}
              className="contents"
              initial={{ opacity: 0 }}
              animate={{ opacity: 1 }}
              exit={{ opacity: 0 }}
              transition={{ duration: 0.25 }}
            >
              <motion.div
                className="lg:col-span-4"
                initial={{ opacity: 0, y: 12 }}
                animate={{ opacity: 1, y: 0 }}
                transition={{ duration: 0.6, ease: EASE_OUT }}
              >
                <h3 className="font-heading text-[26px] font-semibold leading-tight tracking-tight text-text">{tab.title}</h3>
                <p className="mt-4 text-[16px] leading-relaxed text-text-muted">{tab.body}</p>
              </motion.div>
              <motion.div
                className="lg:col-span-7 lg:col-start-6"
                initial={{ opacity: 0, y: 20 }}
                animate={{ opacity: 1, y: 0 }}
                transition={{ duration: 0.7, ease: EASE_OUT, delay: 0.05 }}
              >
                <tab.Mock />
              </motion.div>
            </motion.div>
          </AnimatePresence>
        </div>
      </Container>
    </section>
  )
}
