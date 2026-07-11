import { Navigate, Outlet } from 'react-router-dom'
import { isManager } from '@/features/auth/roles'
import { useAuth } from '@/features/auth/use-auth'

/**
 * Route guard for the Manager-only user-management branch. A non-Manager (Editor/Auditor) is
 * redirected home, so direct navigation to `/users` is blocked (spec: "Editor or Auditor never
 * sees the users screen"). Sits inside <RequireAuth>, so the session is already resolved. The
 * backend still enforces the same rule with `403`.
 */
export function RequireManager() {
  const { user } = useAuth()

  if (!isManager(user)) {
    return <Navigate to="/" replace />
  }

  return <Outlet />
}
