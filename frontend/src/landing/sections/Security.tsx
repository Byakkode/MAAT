import { FileX2, KeyRound, MapPin, UserCheck } from 'lucide-react'
import { Container, Reveal, SectionLabel } from '../primitives'

// Chaque affirmation correspond à un choix vérifiable du code ou de la configuration
// (CLAUDE.md, sections Hébergement et Sécurité ; docs/specs/auth-securite-rgpd.md).
const POINTS = [
  {
    icon: MapPin,
    title: 'Hébergé en France',
    body: "Serveurs OVHcloud situés en France. Aucun hébergeur ni service de stockage hors de l’Union européenne.",
  },
  {
    icon: FileX2,
    title: 'Aucun rapport conservé',
    body: 'Le PDF est régénéré depuis votre diagnostic à chaque téléchargement. Seules sa date et son format sont enregistrés.',
  },
  {
    icon: KeyRound,
    title: 'Sessions protégées',
    body: "Jeton d’accès de 15 minutes gardé en mémoire, jamais dans le stockage du navigateur. Mots de passe hachés avec bcrypt.",
  },
  {
    icon: UserCheck,
    title: 'Vos droits RGPD, sans formulaire',
    body: "Export de l’ensemble de vos données et suppression du compte, directement depuis l’espace « Mon compte ».",
  },
]

export function Security() {
  return (
    <section id="securite" aria-labelledby="securite-title" className="bg-sidebar py-24 text-white sm:py-32">
      <Container>
        <Reveal>
          <SectionLabel index="04" tone="dark">Sécurité et souveraineté</SectionLabel>
          <h2 id="securite-title" className="display mt-6 max-w-[22ch] text-[clamp(2.25rem,5vw,4rem)]">
            Vos données restent en France. <span className="text-white/45">Vos rapports ne restent nulle part.</span>
          </h2>
        </Reveal>

        <ul className="mt-16 grid gap-px overflow-hidden rounded-card border border-white/10 bg-white/10 sm:grid-cols-2 lg:grid-cols-4">
          {POINTS.map(({ icon: Icon, title, body }, i) => (
            <li key={title} className="bg-sidebar p-7">
              <Reveal delay={i * 0.06}>
                <Icon size={22} strokeWidth={1.5} className="text-blue-maat" aria-hidden="true" />
                <h3 className="mt-8 font-heading text-[18px] font-semibold tracking-tight">{title}</h3>
                <p className="mt-3 text-[14.5px] leading-relaxed text-white/60">{body}</p>
              </Reveal>
            </li>
          ))}
        </ul>
      </Container>
    </section>
  )
}
