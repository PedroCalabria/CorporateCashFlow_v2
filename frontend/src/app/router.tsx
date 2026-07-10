import { createBrowserRouter } from 'react-router-dom'
import { AppShell } from '@/features/app-shell/components/AppShell'
import { HomePlaceholder } from '@/features/app-shell/components/HomePlaceholder'
import { LoginPage } from '@/features/auth/components/LoginPage'
import { RequireAuth } from '@/features/auth/components/RequireAuth'

/**
 * `/login` is a public route rendered OUTSIDE the shell. Everything under `/` is gated by
 * <RequireAuth>, which redirects unauthenticated users to `/login`; the persistent
 * <AppShell> renders there and every screen is a child route in its <Outlet />. Future
 * capabilities add child routes under the shell — never a sibling. See design.md §D5.
 */
export const router = createBrowserRouter([
  {
    path: '/login',
    element: <LoginPage />,
  },
  {
    element: <RequireAuth />,
    children: [
      {
        path: '/',
        element: <AppShell />,
        children: [{ index: true, element: <HomePlaceholder /> }],
      },
    ],
  },
])
