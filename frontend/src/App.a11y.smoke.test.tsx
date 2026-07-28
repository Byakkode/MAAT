import { describe, expect, it } from 'vitest'
import { render } from '@testing-library/react'
import { axe } from 'vitest-axe'
import App from './App'

// Preuve que vitest-axe fonctionne : un vrai passage d'axe-core sur le DOM rendu, via le
// matcher toHaveNoViolations enregistré dans src/test/setup.ts. La charte impose WCAG AA
// (docs/specs "charte-maat") — ce test est un garde-fou automatique, pas une preuve de
// conformité complète (axe-core ne détecte qu'une partie des critères WCAG).
describe('App (fumée vitest-axe)', () => {
  it("ne signale aucune violation d'accessibilité détectable statiquement", async () => {
    const { container } = render(<App />)

    const results = await axe(container)

    expect(results).toHaveNoViolations()
  })
})
