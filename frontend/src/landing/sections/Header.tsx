import { useEffect, useState } from 'react'
import { buttonLinkClass } from '../../components/ui/buttonStyles'
import { Link } from 'react-router-dom'
import { LogoHorizontal } from '../../components/ui/Logo'
import { Container } from '../primitives'

const LINKS = [
  { href: '#methode', label: 'Méthode' },
  { href: '#produit', label: 'Produit' },
  { href: '#secteurs', label: 'Pondération' },
  { href: '#securite', label: 'Sécurité' },
  { href: '#tarifs', label: 'Tarifs' },
  { href: '#faq', label: 'Questions' },
]

export function Header() {
  // En-tête transparent au-dessus du héros, puis fond blanc et filet dès que le contenu
  // passe dessous : le filet n'apparaît que lorsqu'il sépare réellement quelque chose.
  const [scrolled, setScrolled] = useState(false)
  useEffect(() => {
    const onScroll = () => setScrolled(window.scrollY > 8)
    onScroll()
    window.addEventListener('scroll', onScroll, { passive: true })
    return () => window.removeEventListener('scroll', onScroll)
  }, [])

  return (
    <header
      className={`fixed inset-x-0 top-0 z-40 border-b transition-colors duration-300 ${scrolled ? 'border-border bg-white' : 'border-transparent bg-transparent'}`}
    >
      <Container className="flex h-16 items-center justify-between gap-6">
        <a href="#top" aria-label="MAAT, retour en haut de page">
          <LogoHorizontal />
        </a>

        <nav aria-label="Sections de la page" className="hidden lg:block">
          <ul className="flex items-center gap-8">
            {LINKS.map((l) => (
              <li key={l.href}>
                <a href={l.href} className="text-[14px] text-text-muted transition-colors hover:text-text">
                  {l.label}
                </a>
              </li>
            ))}
          </ul>
        </nav>

        <div className="flex items-center gap-2 sm:gap-4">
          <Link to="/login" className="whitespace-nowrap px-2 py-2 font-heading text-[14px] font-medium text-text transition-colors hover:text-blue-maat-text">
            Se connecter
          </Link>
          <Link to="/register" className={`${buttonLinkClass('primary', 'md')} font-heading max-sm:hidden`}>
            Commencer le diagnostic
          </Link>
        </div>
      </Container>
    </header>
  )
}
