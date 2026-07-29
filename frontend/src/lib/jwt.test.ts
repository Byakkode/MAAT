import { describe, expect, it } from 'vitest'
import { decodeJwt } from './jwt'

function makeToken(payload: Record<string, unknown>): string {
  const header = btoa('{"alg":"HS256"}')
  const body = btoa(JSON.stringify(payload)).replace(/\+/g, '-').replace(/\//g, '_').replace(/=+$/, '')
  return `${header}.${body}.signature-non-verifiee`
}

describe('decodeJwt', () => {
  it('lit les claims sub, company_id et role du payload', () => {
    const token = makeToken({
      sub: 'user-1',
      company_id: 'company-1',
      role: 'Admin',
      exp: 1234567890,
      iat: 1234567000,
      jti: 'jti-1',
    })

    const claims = decodeJwt(token)

    expect(claims.sub).toBe('user-1')
    expect(claims.company_id).toBe('company-1')
    expect(claims.role).toBe('Admin')
  })

  it('décode correctement un payload encodé en base64url (avec - et _)', () => {
    // Choisi pour produire un base64 standard contenant '+' et '/', remplacés en base64url.
    const token = makeToken({ sub: '>>???>>>', company_id: 'c', role: 'Viewer', exp: 0, iat: 0, jti: 'j' })

    expect(() => decodeJwt(token)).not.toThrow()
    expect(decodeJwt(token).sub).toBe('>>???>>>')
  })
})
