import { defineConfig, devices } from '@playwright/test'

// e2e/ est le domaine exclusif de Playwright : vite.config.ts exclut ce dossier de
// Vitest pour la raison symétrique (deux frameworks de test, deux API test()/describe()
// incompatibles entre elles).
export default defineConfig({
  testDir: './e2e',
  fullyParallel: true,
  reporter: 'list',
  use: {
    baseURL: 'http://localhost:5173',
    trace: 'on-first-retry',
  },
  // Un seul projet pour l'instant : suffisant pour prouver que la chaîne fonctionne,
  // sans installer les moteurs Firefox/WebKit tant qu'aucun test réel n'en a besoin.
  projects: [{ name: 'chromium', use: { ...devices['Desktop Chrome'] } }],
  webServer: {
    command: 'npm run dev',
    url: 'http://localhost:5173',
    reuseExistingServer: !process.env.CI,
  },
})
