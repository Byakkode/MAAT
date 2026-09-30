import type { ReactNode } from 'react'
import type { LucideIcon } from 'lucide-react'
import { Link } from 'react-router-dom'
import { motion } from 'framer-motion'
import '../../landing/landing.css'
import { LogoHorizontal } from '../ui/Logo'
import { EASE_OUT } from '../../landing/motion'

// Mêmes codes visuels que la page d'accueil (src/landing/) : fond ardoise de la section
// Sécurité, grand titre Poppins resserré, étapes numérotées séparées par des filets. Le
// visiteur qui arrive depuis « Commencer le diagnostic » ne change pas de site.
export interface AuthPanelItem {
  title: string
  text: string
  // Avec icône : liste de contenus (connexion). Sans icône : étapes numérotées (inscription).
  icon?: LucideIcon
}

// Par défaut, le parcours d'un nouvel inscrit.
const STEPS: AuthPanelItem[] = [
  { title: 'Créez votre espace', text: 'Un compte par entreprise, rattaché à votre secteur NAF.' },
  { title: 'Répondez à 45 questions', text: 'Environ trente minutes, enregistrement automatique.' },
  { title: 'Pilotez votre démarche', text: "Score pondéré, plan d’actions priorisé, rapport selon la norme volontaire (ex-VSME)." },
]

interface AuthPanelProps {
  // Titre et liste propres à chaque page : l'inscription présente le parcours, la connexion
  // rappelle à un utilisateur qui revient ce qui l'attend dans son espace.
  title?: ReactNode
  items?: AuthPanelItem[]
}

const DEFAULT_TITLE = (
  <>
    Trente minutes pour savoir <span className="text-white/45">où vous en êtes.</span>
  </>
)

export function AuthPanel({ title = DEFAULT_TITLE, items = STEPS }: AuthPanelProps) {
  return (
    <aside
      className="relative hidden flex-col justify-between overflow-hidden bg-sidebar px-12 py-10 text-white lg:flex lg:w-[42%] xl:px-16"
      aria-label="Présentation de MAAT"
    >
      <Link to="/" aria-label="MAAT, retour à l’accueil" className="self-start">
        <LogoHorizontal tone="dark" />
      </Link>

      <div className="py-8">
        <motion.h2
          className="display max-w-[16ch] text-[clamp(2rem,3vw,3rem)]"
          initial={{ opacity: 0, y: 16 }}
          animate={{ opacity: 1, y: 0 }}
          transition={{ duration: 0.9, ease: EASE_OUT }}
        >
          {title}
        </motion.h2>

        <ol className="mt-10 border-t border-white/10">
          {items.map((step, i) => (
            <motion.li
              key={step.title}
              className="grid grid-cols-[3rem_1fr] border-b border-white/10 py-4"
              initial={{ opacity: 0, y: 12 }}
              animate={{ opacity: 1, y: 0 }}
              transition={{ duration: 0.8, ease: EASE_OUT, delay: 0.15 + i * 0.08 }}
            >
              {step.icon ? (
                <step.icon size={18} strokeWidth={1.75} className="mt-0.5 text-blue-maat" aria-hidden="true" />
              ) : (
                <span className="numeric font-heading text-[14px] text-blue-maat" aria-hidden="true">
                  {String(i + 1).padStart(2, '0')}
                </span>
              )}
              <div>
                <p className="font-heading text-[16px] font-semibold tracking-tight">{step.title}</p>
                <p className="mt-1 text-[14px] leading-relaxed text-white/60">{step.text}</p>
              </div>
            </motion.li>
          ))}
        </ol>
      </div>

      <p className="text-[12.5px] text-white/50">Hébergé en France · Données souveraines · RGPD</p>
    </aside>
  )
}
