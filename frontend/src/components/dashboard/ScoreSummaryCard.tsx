import { motion } from 'framer-motion'
import { TrendingDown, TrendingUp } from 'lucide-react'
import { ScoreRing } from './ScoreRing'

interface ScoreSummaryCardProps {
  score: number
  scoreLabel: string
  sectorCode: string
  completedAt: string
  delta?: number | null
}

function DeltaPill({ delta }: { delta: number }) {
  if (delta === 0) return null
  const isUp = delta > 0
  const abs = Math.abs(Math.round(delta))
  const Icon = isUp ? TrendingUp : TrendingDown
  return (
    <span
      className={`inline-flex items-center gap-1 rounded-full px-2 py-1 text-[12px] font-semibold tabular-nums lining-nums ${
        isUp ? 'bg-green-maat/10 text-green-maat-text' : 'bg-red/10 text-red'
      }`}
    >
      <Icon size={11} strokeWidth={2.5} aria-hidden />
      {isUp ? '+' : '-'}{abs} pts
    </span>
  )
}

function formatDate(iso: string): string {
  return new Date(iso).toLocaleDateString('fr-FR', {
    day: 'numeric',
    month: 'long',
    year: 'numeric',
  })
}

export function ScoreSummaryCard({
  score,
  scoreLabel,
  sectorCode,
  completedAt,
  delta,
}: ScoreSummaryCardProps) {
  return (
    <motion.section
      aria-labelledby="score-hero-heading"
      className="rounded-xl border border-border bg-white p-6 shadow-card"
      initial={{ opacity: 0, y: 8 }}
      animate={{ opacity: 1, y: 0 }}
      transition={{ duration: 0.28, ease: 'easeOut' }}
    >
      <div className="flex items-center gap-8">
        {/* Anneau SVG animé */}
        <ScoreRing score={score} size={148} delay={0.1} />

        {/* Contexte */}
        <div className="min-w-0 flex-1">
          <div className="flex items-start justify-between gap-4">
            <div>
              <p className="text-[11.5px] font-semibold uppercase tracking-[0.08em] text-text-muted">
                Score RSE global
              </p>
              <h2
                id="score-hero-heading"
                className="mt-2 text-[2.5rem] font-bold leading-none tabular-nums lining-nums text-text"
              >
                {Math.round(score)}
                <span className="ml-1.5 text-xl font-medium text-text-muted">/ 100</span>
              </h2>
              <p className="mt-2 text-[15px] font-medium text-text">{scoreLabel}</p>
            </div>

            {delta !== null && delta !== undefined && delta !== 0 && (
              <DeltaPill delta={delta} />
            )}
          </div>

          <div className="mt-5 flex flex-wrap gap-x-5 gap-y-1 border-t border-border pt-4 text-[12.5px] text-text-muted">
            <span>
              Secteur{' '}
              <span className="font-medium text-text">{sectorCode}</span>
            </span>
            <span aria-hidden>·</span>
            <span>
              Diagnostic du{' '}
              <span className="font-medium text-text">{formatDate(completedAt)}</span>
            </span>
          </div>
        </div>
      </div>
    </motion.section>
  )
}
