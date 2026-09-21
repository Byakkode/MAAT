/// <reference types="vitest/config" />
import { defineConfig } from 'vite'
import react from '@vitejs/plugin-react'
import tailwindcss from '@tailwindcss/vite'

// https://vite.dev/config/
export default defineConfig({
  plugins: [react(), tailwindcss()],
  optimizeDeps: {
    include: ['framer-motion', '@radix-ui/react-tooltip'],
  },
  test: {
    environment: 'jsdom',
    setupFiles: ['./src/test/setup.ts'],
    // Le dossier e2e/ appartient à Playwright (playwright.config.ts), jamais à Vitest :
    // sans cette exclusion, Vitest tenterait d'exécuter les specs Playwright comme des
    // tests unitaires (API test()/describe() différente, incompatible).
    exclude: ['**/node_modules/**', '**/dist/**', 'e2e/**'],
  },
})
