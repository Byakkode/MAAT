import { Container, Reveal, SectionLabel } from '../primitives'

const STEPS = [
  {
    title: 'Répondre',
    body: "45 questions réparties en cinq domaines. Chacune précise ce qui est attendu et à quoi ressemble une situation absente, partielle ou maîtrisée. Les réponses s’enregistrent au fil de l’eau : on peut s’interrompre et reprendre plus tard.",
    meta: 'Environ 30 minutes',
  },
  {
    title: 'Mesurer',
    body: "Un score sur 100 par domaine, puis un score global pondéré selon votre section NAF. Cinq niveaux de maturité, de « Démarche à initier » à « Démarche exemplaire », pour situer l’entreprise sans jargon.",
    meta: 'Score pondéré par secteur',
  },
  {
    title: 'Prioriser',
    body: "Chaque réponse faible déclenche une recommandation concrète, rédigée pour une PME. Elle est chiffrée en points de score à gagner et qualifiée par un niveau d’effort : on sait par où commencer.",
    meta: 'Gain estimé · effort',
  },
  {
    title: 'Piloter et rendre compte',
    body: "Les recommandations retenues deviennent un plan d’actions : responsable, échéance, statut, notes. Les diagnostics suivants mesurent le chemin parcouru, et le rapport PDF se génère en un clic.",
    meta: 'Rapport PDF · standard VSME',
  },
]

export function Method() {
  return (
    <section id="methode" aria-labelledby="methode-title" className="bg-white py-24 sm:py-32">
      <Container className="grid gap-14 lg:grid-cols-12">
        <div className="lg:col-span-5">
          <div className="lg:sticky lg:top-32">
            <SectionLabel index="01">Méthode</SectionLabel>
            <h2 id="methode-title" className="display mt-6 max-w-[13ch] text-[clamp(2.25rem,4vw,3.25rem)] text-text">
              De la première question au rapport.
            </h2>
            <p className="mt-6 max-w-[38ch] text-[16px] leading-relaxed text-text-muted">
              Un parcours unique, pensé pour un dirigeant qui n’a ni cabinet de conseil ni
              responsable RSE à temps plein.
            </p>
          </div>
        </div>

        <ol className="lg:col-span-6 lg:col-start-7">
          {STEPS.map((step, i) => (
            <li key={step.title} className="group border-t border-border py-10 first:border-t-0 first:pt-0 sm:py-12 lg:first:pt-2">
              <Reveal className="grid gap-4 sm:grid-cols-[6rem_1fr]">
                <span className="numeric display text-[2.5rem] text-border-strong transition-colors duration-500 group-hover:text-blue-maat" aria-hidden="true">
                  {String(i + 1).padStart(2, '0')}
                </span>
                <div>
                  <h3 className="font-heading text-[22px] font-semibold tracking-tight text-text sm:text-[26px]">{step.title}</h3>
                  <p className="mt-3 text-[16px] leading-relaxed text-text-muted">{step.body}</p>
                  <p className="mt-5 text-[13px] font-medium text-blue-maat-text">{step.meta}</p>
                </div>
              </Reveal>
            </li>
          ))}
        </ol>
      </Container>
    </section>
  )
}
