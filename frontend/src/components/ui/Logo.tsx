import logoFull from '../../assets/maat-logo.png'
import logoFullWhite from '../../assets/maat-logo-blanc.png'
import logoName from '../../assets/maat-nom.png'
import logoNameWhite from '../../assets/maat-nom-blanc.png'
import logoSymbol from '../../assets/maat-symbole.png'
import logoSymbolWhite from '../../assets/maat-symbole-blanc.png'

// Fichiers découpés dans le logo officiel (vertical, fond transparent, 512 px), en version
// noire et blanche : le logo complet, le symbole seul et le nom seul.
// tone="light" : fond clair, logo noir. tone="dark" : fond sombre, logo blanc.

type Tone = 'light' | 'dark'

const FILES: Record<Tone, { full: string; name: string; symbol: string }> = {
  light: { full: logoFull, name: logoName, symbol: logoSymbol },
  dark: { full: logoFullWhite, name: logoNameWhite, symbol: logoSymbolWhite },
}

interface LogoProps {
  tone?: Tone
  className?: string
}

// Version horizontale (symbole à gauche du nom) pour les barres de navigation : le logo
// vertical, réduit à leur hauteur, rendrait le nom illisible.
export function LogoHorizontal({ tone = 'light', className = '' }: LogoProps) {
  const files = FILES[tone]
  return (
    <span className={`flex items-center gap-2.5 ${className}`}>
      <img src={files.symbol} alt="" className="h-8 w-auto" />
      <img src={files.name} alt="MAAT" className="h-[15px] w-auto" />
    </span>
  )
}

export function LogoVertical({ tone = 'light', className = '' }: LogoProps) {
  return <img src={FILES[tone].full} alt="MAAT" className={`w-auto ${className}`} />
}
