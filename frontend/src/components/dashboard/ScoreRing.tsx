import { motion } from 'framer-motion'

interface ScoreRingProps {
  score: number
  size?: number
  delay?: number
}

const CX = 50
const CY = 50
const RADIUS = 37
const STROKE_WIDTH = 7

// pathLength="1" normalise l'arc à [0,1] — Framer Motion anime directement
// stroke-dashoffset sans calcul de circonférence manuel.
export function ScoreRing({ score, size = 148, delay = 0 }: ScoreRingProps) {
  const clamped = Math.max(0, Math.min(100, score))

  return (
    <div
      style={{ width: size, height: size }}
      className="relative shrink-0"
      aria-hidden="true"
    >
      <svg
        viewBox="0 0 100 100"
        width={size}
        height={size}
        style={{ transform: 'rotate(-90deg)' }}
      >
        {/* Piste */}
        <circle
          cx={CX}
          cy={CY}
          r={RADIUS}
          fill="none"
          stroke="var(--color-border)"
          strokeWidth={STROKE_WIDTH}
        />
        {/* Arc de progression */}
        <motion.circle
          cx={CX}
          cy={CY}
          r={RADIUS}
          fill="none"
          stroke="var(--color-blue-maat)"
          strokeWidth={STROKE_WIDTH}
          strokeLinecap="round"
          pathLength="1"
          initial={{ pathLength: 0, opacity: 0 }}
          animate={{ pathLength: clamped / 100, opacity: 1 }}
          transition={{
            pathLength: { duration: 1.4, ease: [0.16, 1, 0.3, 1], delay },
            opacity: { duration: 0.2, delay },
          }}
        />
      </svg>

      {/* Valeur centrée */}
      <div className="absolute inset-0 flex flex-col items-center justify-center">
        <motion.span
          className="text-[2.2rem] font-bold leading-none tabular-nums lining-nums text-text"
          initial={{ opacity: 0 }}
          animate={{ opacity: 1 }}
          transition={{ duration: 0.4, delay: delay + 0.3 }}
        >
          {Math.round(score)}
        </motion.span>
        <span className="mt-0.5 text-[11px] font-medium text-text-muted">/ 100</span>
      </div>
    </div>
  )
}
