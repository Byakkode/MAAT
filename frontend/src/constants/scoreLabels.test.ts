import { describe, expect, it } from 'vitest'
import { getScoreLabel, roundScoreForDisplay } from './scoreLabels'

// docs/specs/dashboard.md, section 2 : ces libellés doivent apparaître à l'identique dans le
// rapport PDF — les bornes de chaque tranche sont donc vérifiées une par une, pas seulement
// un échantillon.
describe('getScoreLabel', () => {
  it.each([
    [0, 'Démarche à initier'],
    [24, 'Démarche à initier'],
    [25, 'Premiers pas engagés'],
    [49, 'Premiers pas engagés'],
    [50, 'Démarche structurée'],
    [69, 'Démarche structurée'],
    [70, 'Démarche avancée'],
    [84, 'Démarche avancée'],
    [85, 'Démarche exemplaire'],
    [100, 'Démarche exemplaire'],
  ])('score %i -> %s', (score, label) => {
    expect(getScoreLabel(score)).toBe(label)
  })
})

describe('roundScoreForDisplay', () => {
  it('arrondit au plus proche, .5 vers le haut', () => {
    expect(roundScoreForDisplay(62.3)).toBe(62)
    expect(roundScoreForDisplay(62.5)).toBe(63)
    expect(roundScoreForDisplay(62.49)).toBe(62)
  })
})
