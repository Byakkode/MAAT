export interface JwtClaims {
  sub: string
  company_id: string
  role: string
  exp: number
  iat: number
  jti: string
}

// docs/specs/auth-securite-rgpd.md, section 2 : « un JWT est signé, pas chiffré, et son
// contenu est lisible par quiconque l'intercepte ». Le décoder ici pour lire role/
// company_id à des fins d'affichage ne franchit donc aucune frontière de sécurité :
// l'autorisation réelle reste vérifiée par le serveur à chaque requête, jamais ici.
export function decodeJwt(token: string): JwtClaims {
  const payload = token.split('.')[1]
  const base64 = payload.replace(/-/g, '+').replace(/_/g, '/')
  const json = decodeURIComponent(
    atob(base64)
      .split('')
      .map((c) => '%' + c.charCodeAt(0).toString(16).padStart(2, '0'))
      .join(''),
  )
  return JSON.parse(json) as JwtClaims
}
