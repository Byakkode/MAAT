import { useEffect, useState } from 'react'
import { animate, motion, useMotionValue, useTransform } from 'framer-motion'
import { DEMO_DOMAIN_SCORES, DOMAINS, SECTORS, weightedScore } from '../content'
import { Container, Reveal, SectionLabel } from '../primitives'
import { EASE_OUT } from '../motion'

const formatPercent = (w: number) => `${Math.round(w * 100)} %`

// Section la plus « produit » de la page : elle montre le cœur du calcul
// (docs/specs/scoring.md). Mêmes scores de domaine, secteurs différents, score global
// différent — c'est exactement le cas de test 4 de la spec, rendu manipulable.
export function Sectors() {
  const [index, setIndex] = useState(0)
  const sector = SECTORS[index]
  const score = weightedScore(DEMO_DOMAIN_SCORES, sector.weights)

  return (
    <section id="secteurs" aria-labelledby="secteurs-title" className="bg-white py-24 sm:py-32">
      <Container>
        <Reveal className="grid gap-8 lg:grid-cols-12">
          <div className="lg:col-span-7">
            <SectionLabel index="03">Pondération sectorielle</SectionLabel>
            <h2 id="secteurs-title" className="display mt-6 text-[clamp(2.25rem,4.4vw,3.5rem)] text-text">
              Le même effort ne pèse pas pareil dans tous les secteurs.
            </h2>
          </div>
          <p className="max-w-[44ch] self-end text-[16px] leading-relaxed text-text-muted lg:col-span-4 lg:col-start-9">
            Un chantier est d’abord jugé sur la sécurité de ses équipes, un commerce sur ses
            fournisseurs. MAAT applique à chaque section NAF sa propre pondération des cinq domaines.
          </p>
        </Reveal>

        <div className="mt-16 grid gap-10 lg:grid-cols-12">
          <fieldset className="lg:col-span-4">
            <legend className="mb-4 text-[13px] font-medium text-text-muted">Choisissez un secteur</legend>
            <div className="flex flex-col border-t border-border">
              {SECTORS.map((s, i) => {
                const selected = i === index
                return (
                  <button
                    key={s.code}
                    type="button"
                    aria-pressed={selected}
                    onClick={() => setIndex(i)}
                    className={`group flex cursor-pointer items-center gap-4 border-b border-border py-3.5 text-left transition-colors ${selected ? 'text-text' : 'text-text-muted hover:text-text'}`}
                  >
                    <span
                      className={`flex h-7 w-7 shrink-0 items-center justify-center rounded-md font-heading text-[12px] font-semibold transition-colors ${selected ? 'bg-blue-maat text-white' : 'bg-bg text-text-muted group-hover:text-text'}`}
                      aria-hidden="true"
                    >
                      {s.code}
                    </span>
                    <span className={`text-[15px] ${selected ? 'font-medium' : ''}`}>{s.label}</span>
                  </button>
                )
              })}
            </div>
          </fieldset>

          <div className="lg:col-span-7 lg:col-start-6">
            <div className="rounded-card border border-border p-6 sm:p-8">
              <div className="flex flex-wrap items-baseline justify-between gap-4">
                <p className="text-[13px] text-text-muted">
                  Poids de chaque domaine · section {sector.code}
                </p>
              </div>

              <ul className="mt-6 flex flex-col gap-5">
                {DOMAINS.map((d) => (
                  <li key={d.key}>
                    <div className="flex items-baseline justify-between gap-4">
                      <p className="text-[15px] font-medium text-text">{d.label}</p>
                      <p className="numeric font-heading text-[15px] font-semibold text-text">{formatPercent(sector.weights[d.key])}</p>
                    </div>
                    <div className="mt-2 h-2 overflow-hidden rounded-full bg-bg">
                      <motion.div
                        className="h-full rounded-full"
                        style={{ background: d.color }}
                        initial={false}
                        animate={{ width: `${sector.weights[d.key] * 250}%` }}
                        transition={{ duration: 0.8, ease: EASE_OUT }}
                      />
                    </div>
                    <p className="mt-1.5 text-[12px] text-text-muted">{d.anchor}</p>
                  </li>
                ))}
              </ul>

              <div className="mt-8 flex flex-wrap items-end justify-between gap-6 border-t border-border pt-6">
                <p className="max-w-[34ch] text-[14px] leading-snug text-text-muted">
                  Score global d’une même entreprise témoin, avec des réponses identiques :
                </p>
                <p className="flex items-baseline gap-2" aria-live="polite">
                  <AnimatedNumber value={score} />
                  <span className="text-[14px] text-text-muted">/ 100</span>
                </p>
              </div>
            </div>
          </div>
        </div>
      </Container>
    </section>
  )
}

// Nombre qui défile jusqu'à sa nouvelle valeur. Le texte accessible reste la valeur finale
// (sr-only) : un lecteur d'écran n'a pas à entendre chaque valeur intermédiaire.
function AnimatedNumber({ value }: { value: number }) {
  const mv = useMotionValue(value)
  const rounded = useTransform(mv, (v) => Math.round(v).toString())
  useEffect(() => {
    const controls = animate(mv, value, { duration: 0.8, ease: EASE_OUT })
    return () => controls.stop()
  }, [mv, value])

  return (
    <span className="numeric display text-[clamp(3rem,6vw,4.5rem)] text-text">
      <motion.span aria-hidden="true">{rounded}</motion.span>
      <span className="sr-only">{value}</span>
    </span>
  )
}
