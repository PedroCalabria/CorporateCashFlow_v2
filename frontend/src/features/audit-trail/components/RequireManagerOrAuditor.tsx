import { Navigate, Outlet } from 'react-router-dom'
import { isManagerOrAuditor } from '@/features/auth/roles'
import { useAuth } from '@/features/auth/use-auth'

/**
 * Route guard for the Manager/Auditor-only audit-trail branch. An Editor is redirected home, so
 * direct navigation to `/audit-trail` is blocked (spec: "Editor does not see the audit trail
 * link"). Sits inside <RequireAuth>, so the session is already resolved. The backend still
 * enforces the same rule with `403` on both `/api/audit-log` and `/api/access-log`.
 */
export function RequireManagerOrAuditor() {
  const { user } = useAuth()

  if (!isManagerOrAuditor(user)) {
    return <Navigate to="/" replace />
  }

  return <Outlet />
}
