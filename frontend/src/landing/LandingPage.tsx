import { MotionConfig } from 'framer-motion'
import './landing.css'
import { Closing, Footer } from './sections/Closing'
import { Faq } from './sections/Faq'
import { Header } from './sections/Header'
import { Hero } from './sections/Hero'
import { Method } from './sections/Method'
import { ProductTour } from './sections/ProductTour'
import { Sectors } from './sections/Sectors'
import { Security } from './sections/Security'

// Page d'accueil publique, route « / » (App.tsx). Déroulé : promesse et produit réel
// (héros), méthode, visite des écrans, pondération sectorielle manipulable, sécurité, questions, appel à l'action.
export function LandingPage() {
  return (
    // reducedMotion="user" : les visiteurs qui ont activé « réduire les animations » dans
    // leur système ne voient ni déplacement ni défilement de chiffres (WCAG 2.3.3).
    <MotionConfig reducedMotion="user">
      <div className="landing bg-white font-body text-text antialiased">
        <a
          href="#contenu"
          className="sr-only focus:not-sr-only focus:fixed focus:top-3 focus:left-3 focus:z-50 focus:rounded-button focus:bg-blue-maat focus:px-4 focus:py-2 focus:text-white"
        >
          Aller au contenu
        </a>
        <Header />
        <main id="contenu">
          <Hero />
          <Method />
          <ProductTour />
          <Sectors />
          <Security />
          <Faq />
          <Closing />
        </main>
        <Footer />
      </div>
    </MotionConfig>
  )
}
