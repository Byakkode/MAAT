import { describe, expect, it } from 'vitest'
import { estimateRemainingMinutes } from './estimateRemainingTime'

describe('estimateRemainingMinutes', () => {
  it('retourne null tant qu’aucune question n’a été répondue (pas de moyenne observable)', () => {
    expect(estimateRemainingMinutes(0, 60_000, 0, 45)).toBeNull()
  })

  it('extrapole le temps restant sur la durée moyenne observée par question', () => {
    // 5 questions en 100 000 ms -> 20 000 ms/question ; il en reste 40 -> 800 000 ms = 13,3 min.
    const result = estimateRemainingMinutes(0, 100_000, 5, 45)

    expect(result).toBe(13)
  })

  it('retourne 0 quand toutes les questions ont une réponse', () => {
    expect(estimateRemainingMinutes(0, 100_000, 45, 45)).toBe(0)
  })
})
