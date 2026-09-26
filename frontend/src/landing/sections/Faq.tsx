import { Plus } from 'lucide-react'
import { Container, Reveal, SectionLabel } from '../primitives'

const QUESTIONS = [
  {
    q: 'Faut-il connaître la RSE pour répondre ?',
    a: "Non. Les questions portent sur des pratiques concrètes (relevés d’énergie, accueil sécurité, choix des fournisseurs) et chacune explique ce qui est attendu. Un dirigeant ou un responsable administratif peut répondre seul.",
  },
  {
    q: "Qu’est-ce que le standard VSME ?",
    a: "Le Voluntary SME Standard est le cadre européen de reporting de durabilité destiné aux PME non soumises à la CSRD. Il permet de répondre avec un seul document aux demandes de vos clients, banques et donneurs d’ordre.",
  },
  {
    q: 'Comment le score est-il calculé ?',
    a: "Chaque domaine reçoit un score sur 100 à partir de vos réponses, pondérées par l’importance de chaque question. Le score global combine les cinq domaines selon la pondération propre à votre section NAF. Le calcul est déterministe : mêmes réponses, même score.",
  },
  {
    q: 'Peut-on refaire le diagnostic ?',
    a: "Oui, et c’est recommandé une fois par an. Le tableau de bord compare alors vos diagnostics successifs et montre l’évolution de chaque domaine.",
  },
  {
    q: 'Qui a accès à nos réponses ?',
    a: 'Uniquement les comptes de votre entreprise. Les données sont hébergées en France, et vous pouvez les exporter ou supprimer votre compte à tout moment.',
  },
]

// <details>/<summary> natifs : ouverts au clavier et annoncés par les lecteurs d'écran sans
// une ligne de JavaScript.
export function Faq() {
  return (
    <section id="faq" aria-labelledby="faq-title" className="bg-white py-24 sm:py-32">
      <Container className="grid gap-12 lg:grid-cols-12">
        <Reveal className="lg:col-span-4">
          <SectionLabel index="06">Questions</SectionLabel>
          <h2 id="faq-title" className="display mt-6 text-[clamp(2.25rem,4.4vw,3.5rem)] text-text">
            Avant de commencer.
          </h2>
        </Reveal>

        <div className="border-t border-border lg:col-span-7 lg:col-start-6">
          {QUESTIONS.map(({ q, a }) => (
            <details key={q} className="group border-b border-border">
              <summary className="flex cursor-pointer items-center justify-between gap-6 py-6 font-heading text-[18px] font-medium text-text transition-colors hover:text-blue-maat-text sm:text-[20px]">
                {q}
                <Plus size={20} strokeWidth={1.5} className="shrink-0 text-text-muted transition-transform duration-300 group-open:rotate-45" aria-hidden="true" />
              </summary>
              <p className="max-w-[62ch] pb-7 text-[16px] leading-relaxed text-text-muted">{a}</p>
            </details>
          ))}
        </div>
      </Container>
    </section>
  )
}
