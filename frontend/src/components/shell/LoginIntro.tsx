import { useEffect, useRef } from 'react'
import { motion } from 'framer-motion'
import introVideo from '../../assets/chargement-connexion.mp4'

// Durée de la vidéo (10 s) plus une marge : si elle se bloque en cours de lecture (réseau
// lent, décodage interrompu), l'écran se ferme quand même au lieu de retenir l'utilisateur.
const SAFETY_TIMEOUT_MS = 12_000

interface LoginIntroProps {
  onDone: () => void
}

// Animation du logo jouée juste après la connexion, par-dessus la coquille (AppShell) : le
// tableau de bord se charge derrière pendant la lecture, l'attente n'est donc pas ajoutée au
// temps de chargement réel. Sans le son (muted), condition pour que les navigateurs
// autorisent la lecture automatique. Toujours interruptible : bouton « Passer » et Échap.
export function LoginIntro({ onDone }: LoginIntroProps) {
  const videoRef = useRef<HTMLVideoElement>(null)
  const skipRef = useRef<HTMLButtonElement>(null)

  useEffect(() => {
    // L'attribut muted de React n'est pas toujours reporté sur l'élément DOM : on le pose
    // explicitement, sans quoi la lecture automatique peut être refusée.
    if (videoRef.current) videoRef.current.muted = true
    // Focus sur « Passer » : un utilisateur au clavier peut sortir d'une seule touche.
    skipRef.current?.focus()

    const timer = window.setTimeout(onDone, SAFETY_TIMEOUT_MS)
    const onKeyDown = (e: KeyboardEvent) => {
      if (e.key === 'Escape') onDone()
    }
    window.addEventListener('keydown', onKeyDown)
    return () => {
      window.clearTimeout(timer)
      window.removeEventListener('keydown', onKeyDown)
    }
  }, [onDone])

  return (
    <motion.div
      role="status"
      aria-label="Connexion réussie, chargement de votre espace"
      className="fixed inset-0 z-[100] flex items-center justify-center overflow-hidden bg-black"
      initial={{ opacity: 1 }}
      exit={{ opacity: 0 }}
      transition={{ duration: 0.6, ease: 'easeInOut' }}
    >
      {/* L'élément a exactement le format de la vidéo (16:9) : sur écran large, il tient
          entier dans la fenêtre ; sur téléphone en portrait, il déborde en largeur (200vw)
          pour que le logo, centré et occupant 40 % de l'image, reste lisible — seuls les
          côtés vides sortent de l'écran. Le masque radial fond les bords de l'image dans le
          fond noir : aucune bande visible, quel que soit le format. */}
      <video
        ref={videoRef}
        src={introVideo}
        autoPlay
        muted
        playsInline
        preload="auto"
        aria-hidden="true"
        onEnded={onDone}
        onError={onDone}
        className="aspect-video w-[min(100vw,177.78vh)] max-w-none shrink-0 object-cover portrait:w-[200vw] [mask-image:radial-gradient(closest-side,black_70%,transparent_100%)]"
      />
      <button
        ref={skipRef}
        type="button"
        onClick={onDone}
        className="absolute right-6 bottom-6 cursor-pointer rounded-button border border-white/20 px-4 py-2 font-heading text-[14px] font-medium text-white/80 transition-colors hover:border-white/40 hover:text-white"
      >
        Passer
      </button>
    </motion.div>
  )
}
