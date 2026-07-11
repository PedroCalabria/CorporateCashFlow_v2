import { Navigate, Outlet } from 'react-router-dom'
import { isGlobalManager } from '@/features/auth/roles'
import { useAuth } from '@/features/auth/use-auth'

/**
 * Route guard for the Global-Manager-only subsidiaries branch. A non-Global-Manager (subsidiary
 * -scoped user of any role) is redirected home, so direct navigation to `/subsidiaries` is blocked
 * (spec: "Subsidiary-scoped user never sees the subsidiaries screen"). Sits inside <RequireAuth>,
 * so the session is already resolved here. The backend still enforces the same rule with `403`.
 */
export function RequireGlobalManager() {
  const { user } = useAuth()

  if (!isGlobalManager(user)) {
    return <Navigate to="/" replace />
  }

  return <Outlet />
}
