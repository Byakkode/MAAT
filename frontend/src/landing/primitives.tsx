import type { ReactNode } from 'react'
import { motion } from 'framer-motion'
import { EASE_OUT } from './motion'


interface RevealProps {
  children: ReactNode
  delay?: number
  className?: string
}

// Apparition au défilement, une seule fois. MotionConfig reducedMotion="user" (LandingPage)
// supprime le déplacement pour les visiteurs qui ont demandé moins d'animations.
export function Reveal({ children, delay = 0, className }: RevealProps) {
  return (
    <motion.div
      className={className}
      initial={{ opacity: 0, y: 24 }}
      whileInView={{ opacity: 1, y: 0 }}
      viewport={{ once: true, margin: '0px 0px -12% 0px' }}
      transition={{ duration: 0.9, ease: EASE_OUT, delay }}
    >
      {children}
    </motion.div>
  )
}

export function Container({ children, className = '' }: { children: ReactNode; className?: string }) {
  return <div className={`mx-auto w-full max-w-[1280px] px-4 sm:px-8 ${className}`}>{children}</div>
}

interface SectionLabelProps {
  index: string
  children: ReactNode
  tone?: 'light' | 'dark'
}

// Repère de section « 01 — Méthode » : numérotation et filet plutôt qu'un badge coloré.
export function SectionLabel({ index, children, tone = 'light' }: SectionLabelProps) {
  const muted = tone === 'dark' ? 'text-white/50' : 'text-text-muted'
  const rule = tone === 'dark' ? 'bg-white/20' : 'bg-border-strong'
  return (
    <p className={`flex items-center gap-3 text-[13px] font-medium ${muted}`}>
      <span className="numeric font-heading">{index}</span>
      <span className={`h-px w-8 ${rule}`} aria-hidden="true" />
      <span>{children}</span>
    </p>
  )
}
