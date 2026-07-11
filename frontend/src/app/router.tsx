import { createBrowserRouter } from 'react-router-dom'
import { AppShell } from '@/features/app-shell/components/AppShell'
import { HomePlaceholder } from '@/features/app-shell/components/HomePlaceholder'
import { LoginPage } from '@/features/auth/components/LoginPage'
import { RequireAuth } from '@/features/auth/components/RequireAuth'
import { RequireGlobalManager } from '@/features/subsidiaries/components/RequireGlobalManager'
import { SubsidiariesListPage } from '@/features/subsidiaries/components/SubsidiariesListPage'

/**
 * `/login` is a public route rendered OUTSIDE the shell. Everything under `/` is gated by
 * <RequireAuth>, which redirects unauthenticated users to `/login`; the persistent
 * <AppShell> renders there and every screen is a child route in its <Outlet />. Capability
 * screens with extra RBAC (e.g. `/subsidiaries`, Global-Manager-only via <RequireGlobalManager>)
 * nest as child routes under the shell — never a sibling. See design.md §D5/§D7.
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
        children: [
          { index: true, element: <HomePlaceholder /> },
          {
            element: <RequireGlobalManager />,
            children: [{ path: 'subsidiaries', element: <SubsidiariesListPage /> }],
          },
        ],
      },
    ],
  },
])
