import { Navigate, Outlet } from 'react-router-dom'
import { useAuth } from '../use-auth'

/**
 * Route guard for the App Shell branch. While the boot-time silent refresh is in flight
 * (`loading`) it renders nothing to avoid a login flash; an unauthenticated user is
 * redirected to `/login`; otherwise the shell's child routes render via <Outlet /> (spec:
 * "Unauthenticated user is redirected to login").
 */
export function RequireAuth() {
  const { status } = useAuth()

  if (status === 'loading') {
    return null
  }

  if (status === 'unauthenticated') {
    return <Navigate to="/login" replace />
  }

  return <Outlet />
}
