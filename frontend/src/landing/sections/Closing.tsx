import { ArrowRight } from 'lucide-react'
import { buttonLinkClass } from '../../components/ui/buttonStyles'
import { Link } from 'react-router-dom'
import { LogoVertical } from '../../components/ui/Logo'
import { Container, Reveal } from '../primitives'

export function Closing() {
  return (
    <section aria-labelledby="closing-title" className="border-t border-border bg-bg py-24 sm:py-36">
      <Container>
        <Reveal className="grid gap-10 lg:grid-cols-12 lg:items-end">
          <h2 id="closing-title" className="display text-[clamp(2.5rem,6.4vw,5.5rem)] text-text lg:col-span-9">
            Trente minutes pour savoir <span className="text-blue-maat">où vous en êtes.</span>
          </h2>
          <div className="flex flex-col items-start gap-4 lg:col-span-3 lg:items-end">
            <Link to="/register" className={`${buttonLinkClass('primary', 'lg')} group px-6 py-3 font-heading text-[15px]`}>
              Créer mon compte
              <ArrowRight size={16} className="transition-transform duration-300 group-hover:translate-x-0.5" aria-hidden="true" />
            </Link>
            <Link to="/login" className="text-[14px] text-text-muted underline decoration-border-strong underline-offset-4 hover:text-text">
              J’ai déjà un compte
            </Link>
          </div>
        </Reveal>
      </Container>
    </section>
  )
}

const FOOTER_LINKS = [
  { href: '#methode', label: 'Méthode' },
  { href: '#produit', label: 'Produit' },
  { href: '#secteurs', label: 'Pondération' },
  { href: '#securite', label: 'Sécurité' },
  { href: '#tarifs', label: 'Tarifs' },
  { href: '#faq', label: 'Questions' },
]

export function Footer() {
  return (
    <footer className="bg-sidebar text-white">
      <Container className="grid gap-12 py-16 md:grid-cols-12">
        <div className="md:col-span-5">
          <LogoVertical tone="dark" className="h-20" />
          <p className="mt-5 max-w-[36ch] text-[14px] leading-relaxed text-white/55">
            Le diagnostic RSE des PME : mesurer, prioriser, piloter, rendre compte.
          </p>
        </div>
        <nav aria-label="Plan de la page" className="md:col-span-3 md:col-start-7">
          <p className="text-[12px] font-medium uppercase tracking-[0.12em] text-white/50">La page</p>
          <ul className="mt-4 flex flex-col gap-2.5">
            {FOOTER_LINKS.map((l) => (
              <li key={l.href}>
                <a href={l.href} className="text-[14px] text-white/70 transition-colors hover:text-white">{l.label}</a>
              </li>
            ))}
          </ul>
        </nav>
        <div className="md:col-span-3">
          <p className="text-[12px] font-medium uppercase tracking-[0.12em] text-white/50">Application</p>
          <ul className="mt-4 flex flex-col gap-2.5">
            <li><Link to="/register" className="text-[14px] text-white/70 transition-colors hover:text-white">Créer un compte</Link></li>
            <li><Link to="/login" className="text-[14px] text-white/70 transition-colors hover:text-white">Se connecter</Link></li>
          </ul>
        </div>
      </Container>
      <Container>
        <div className="flex flex-col gap-2 border-t border-white/10 py-6 text-[12.5px] text-white/50 sm:flex-row sm:justify-between">
          <p>© 2026 MAAT</p>
          <p>Hébergé en France · Données souveraines</p>
        </div>
      </Container>
    </footer>
  )
}
