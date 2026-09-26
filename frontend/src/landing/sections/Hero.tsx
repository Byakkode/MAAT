import { motion } from 'framer-motion'
import { ArrowRight } from 'lucide-react'
import { buttonLinkClass } from '../../components/ui/buttonStyles'
import { Link } from 'react-router-dom'
import { DashboardMock } from '../mocks/DashboardMock'
import { Container, Reveal } from '../primitives'
import { EASE_OUT } from '../motion'

// Découpage explicite en lignes : chaque ligne monte depuis derrière un masque
// (overflow-hidden), l'effet typographique le plus sobre qui soit.
const HEADLINE = ["La RSE d’une PME", 'se mesure en', 'trente minutes.']

const FIGURES = [
  { value: '45', text: 'questions écrites pour des dirigeants, pas pour des auditeurs' },
  { value: '5', text: 'domaines alignés sur le VSME, les ESRS et l’ISO 26000' },
  { value: '19', text: 'sections NAF, chacune avec sa propre pondération' },
  { value: '0', text: 'rapport conservé : chaque PDF est régénéré à la demande' },
]

export function Hero() {
  return (
    <section id="top" aria-labelledby="hero-title" className="relative overflow-hidden bg-white">
      <Container className="relative">
        <div className="column-guides pointer-events-none absolute inset-y-0 inset-x-4 opacity-60 sm:inset-x-8" aria-hidden="true" />

        <div className="relative pt-32 pb-14 sm:pt-40 lg:pt-44">
          <motion.p
            className="flex items-center gap-2.5 text-[13px] font-medium text-text-muted"
            initial={{ opacity: 0 }}
            animate={{ opacity: 1 }}
            transition={{ duration: 0.8, ease: EASE_OUT }}
          >
            <span className="h-2 w-2 rounded-[2px] bg-green-maat" aria-hidden="true" />
            Diagnostic RSE pour PME · Rapport conforme au standard VSME
          </motion.p>

          <h1 id="hero-title" className="display mt-7 text-[clamp(2.35rem,8vw,7rem)] text-text">
            {HEADLINE.map((line, i) => (
              <span key={line} className="block overflow-hidden pb-[0.06em]">
                <motion.span
                  className={`block ${i === 2 ? 'text-blue-maat' : ''}`}
                  initial={{ y: '105%' }}
                  animate={{ y: 0 }}
                  transition={{ duration: 1.1, ease: EASE_OUT, delay: 0.1 + i * 0.09 }}
                >
                  {line}
                </motion.span>
              </span>
            ))}
          </h1>

          <motion.div
            className="mt-12 grid gap-8 md:grid-cols-12 md:items-end"
            initial={{ opacity: 0, y: 16 }}
            animate={{ opacity: 1, y: 0 }}
            transition={{ duration: 1, ease: EASE_OUT, delay: 0.45 }}
          >
            <p className="max-w-[46ch] text-[17px] leading-relaxed text-text-muted md:col-span-6 lg:col-span-5">
              MAAT pose 45 questions, calcule un score pondéré selon votre secteur d’activité et en
              tire un plan d’actions chiffré. Vous repartez avec une vision claire de vos priorités
              et un rapport prêt à transmettre à vos clients, banques et donneurs d’ordre.
            </p>
            <div className="flex flex-wrap items-center gap-x-6 gap-y-4 md:col-span-6 md:justify-end lg:col-span-5 lg:col-start-8">
              <Link to="/register" className={`${buttonLinkClass('primary', 'lg')} group px-6 py-3 font-heading text-[15px]`}>
                Commencer le diagnostic
                <ArrowRight size={16} className="transition-transform duration-300 group-hover:translate-x-0.5" aria-hidden="true" />
              </Link>
              <a href="#methode" className="font-heading text-[15px] font-medium text-text underline decoration-border-strong underline-offset-[6px] transition-colors hover:decoration-blue-maat">
                Voir la méthode
              </a>
            </div>
          </motion.div>
        </div>

        <motion.figure
          className="relative"
          initial={{ opacity: 0, y: 48 }}
          animate={{ opacity: 1, y: 0 }}
          transition={{ duration: 1.3, ease: EASE_OUT, delay: 0.35 }}
        >
          <DashboardMock />
          <figcaption className="sr-only">
            Aperçu du tableau de bord MAAT : score RSE global sur 100, score de chacun des cinq
            domaines, actions en cours, point fort et domaine à surveiller.
          </figcaption>
        </motion.figure>

        <ul className="relative grid grid-cols-2 border-border lg:grid-cols-4">
          {FIGURES.map((f, i) => (
            <li key={f.value + f.text} className="border-t border-border py-8 pr-6 sm:py-10">
              <Reveal delay={i * 0.06}>
                <p className="numeric display text-[clamp(2.5rem,5vw,4rem)] text-text">{f.value}</p>
                <p className="mt-3 max-w-[26ch] text-[14px] leading-snug text-text-muted">{f.text}</p>
              </Reveal>
            </li>
          ))}
        </ul>
      </Container>
    </section>
  )
}
