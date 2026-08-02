import { Navigate, Outlet } from 'react-router-dom'
import { useAuthStore } from '../store/authStore'

export function ProtectedRoute() {
  const status = useAuthStore((state) => state.status)

  if (status === 'restoring') {
    return (
      <p role="status" className="text-text-muted">
        Chargement de la session…
      </p>
    )
  }

  if (status === 'unauthenticated') {
    return <Navigate to="/login" replace />
  }

  return <Outlet />
}
