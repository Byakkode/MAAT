import { create } from 'zustand'
import * as dashboardApi from '../api/dashboardApi'
import * as recommendationsApi from '../api/recommendationsApi'
import type { DashboardView } from '../types/dashboard'

export type LoadStatus = 'idle' | 'loading' | 'loaded' | 'error'

interface DashboardState extends DashboardView {
  loadStatus: LoadStatus
  loadError: string | null
  // Code de la recommandation dont la case est en cours de bascule : sert à désactiver
  // uniquement CETTE case pendant la requête, pas tout le plan d'actions.
  togglingCode: string | null
  toggleError: string | null

  load: () => Promise<void>
  toggleRecommendation: (diagnosticId: string, code: string, isCompleted: boolean) => Promise<void>
}

const emptyDashboard: DashboardView = {
  hasCompletedDiagnostic: false,
  latestDiagnostic: null,
  domainScores: [],
  history: [],
  benchmark: null,
  actionPlan: { items: [], totalCount: 0, completedCount: 0 },
  inProgressDiagnostic: null,
}

const initialState: Omit<DashboardState, 'load' | 'toggleRecommendation'> = {
  ...emptyDashboard,
  loadStatus: 'idle',
  loadError: null,
  togglingCode: null,
  toggleError: null,
}

export const useDashboardStore = create<DashboardState>((set) => ({
  ...initialState,

  async load() {
    set({ loadStatus: 'loading', loadError: null })
    try {
      const data = await dashboardApi.getDashboard()
      set({ ...data, loadStatus: 'loaded' })
    } catch (err) {
      const message = err instanceof Error ? err.message : 'Impossible de récupérer le tableau de bord.'
      set({ loadStatus: 'error', loadError: message })
    }
  },

  // docs/specs/dashboard.md, section 6 : "Cocher une action ne modifie pas le score." — un
  // rechargement complet du tableau de bord après bascule est donc sûr, aucun autre champ que
  // le plan d'actions ne peut avoir changé côté serveur entre les deux appels.
  async toggleRecommendation(diagnosticId, code, isCompleted) {
    set({ togglingCode: code, toggleError: null })
    try {
      await recommendationsApi.updateRecommendationProgress(diagnosticId, code, isCompleted)
      const data = await dashboardApi.getDashboard()
      set({ ...data, togglingCode: null })
    } catch (err) {
      const message = err instanceof Error ? err.message : "Échec de la mise à jour de l'action."
      set({ togglingCode: null, toggleError: message })
    }
  },
}))
