import { create } from 'zustand'
import * as recommendationsApi from '../api/recommendationsApi'
import type { RecommendationDetail } from '../types/recommendations'

export type LoadStatus = 'idle' | 'loading' | 'loaded' | 'error'

interface PlanActionsState {
  loadStatus: LoadStatus
  loadError: string | null
  diagnosticId: string | null
  items: RecommendationDetail[]
  // Code de la recommandation dont la case est en cours de bascule, même patron que
  // dashboardStore.togglingCode.
  togglingCode: string | null
  toggleError: string | null

  load: (diagnosticId: string) => Promise<void>
  toggle: (code: string, isCompleted: boolean) => Promise<void>
}

// docs/specs/recommandations.md, section 4 : la liste complète d'un diagnostic — pas
// seulement les cinq premières lignes que porte dashboardStore.actionPlan.
export const usePlanActionsStore = create<PlanActionsState>((set, get) => ({
  loadStatus: 'idle',
  loadError: null,
  diagnosticId: null,
  items: [],
  togglingCode: null,
  toggleError: null,

  async load(diagnosticId) {
    set({ loadStatus: 'loading', loadError: null, diagnosticId })
    try {
      const items = await recommendationsApi.getRecommendations(diagnosticId)
      set({ items, loadStatus: 'loaded' })
    } catch (err) {
      const message = err instanceof Error ? err.message : 'Impossible de récupérer le plan d’actions.'
      set({ loadStatus: 'error', loadError: message })
    }
  },

  // docs/specs/recommandations.md, section 5 : "Cocher une action ne modifie jamais le
  // score." — un rechargement complet après bascule est donc sûr, même principe que
  // dashboardStore.toggleRecommendation.
  async toggle(code, isCompleted) {
    const { diagnosticId } = get()
    if (!diagnosticId) {
      return
    }

    set({ togglingCode: code, toggleError: null })
    try {
      await recommendationsApi.updateRecommendationProgress(diagnosticId, code, isCompleted)
      const items = await recommendationsApi.getRecommendations(diagnosticId)
      set({ items, togglingCode: null })
    } catch (err) {
      const message = err instanceof Error ? err.message : "Échec de la mise à jour de l'action."
      set({ togglingCode: null, toggleError: message })
    }
  },
}))
