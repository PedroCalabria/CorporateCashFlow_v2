import { createBrowserRouter } from 'react-router-dom'
import { AppShell } from '@/features/app-shell/components/AppShell'
import { HomePlaceholder } from '@/features/app-shell/components/HomePlaceholder'

/**
 * Root route renders the persistent <AppShell>; every screen is a child route
 * rendered into its <Outlet />. Future capabilities add child routes here —
 * never a sibling of the shell — so the shell is never duplicated. See
 * design.md §D2.
 */
export const router = createBrowserRouter([
  {
    path: '/',
    element: <AppShell />,
    children: [{ index: true, element: <HomePlaceholder /> }],
  },
])
