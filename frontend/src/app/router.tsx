import { createBrowserRouter } from 'react-router-dom'
import { AppShell } from '@/features/app-shell/components/AppShell'
import { HomePlaceholder } from '@/features/app-shell/components/HomePlaceholder'
import { LoginPage } from '@/features/auth/components/LoginPage'
import { RequireAuth } from '@/features/auth/components/RequireAuth'
import { RequireGlobalManager } from '@/features/subsidiaries/components/RequireGlobalManager'
import { SubsidiariesListPage } from '@/features/subsidiaries/components/SubsidiariesListPage'
import { RequireManager } from '@/features/users/components/RequireManager'
import { UsersListPage } from '@/features/users/components/UsersListPage'
import { LedgerEntriesListPage } from '@/features/ledger-entries/components/LedgerEntriesListPage'
import { BankStatementImportsListPage } from '@/features/bank-statement-import/components/BankStatementImportsListPage'
import { ReconciliationBoardPage } from '@/features/reconciliation/components/ReconciliationBoardPage'
import { AuditTrailPage } from '@/features/audit-trail/components/AuditTrailPage'
import { RequireManagerOrAuditor } from '@/features/audit-trail/components/RequireManagerOrAuditor'

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
          {
            element: <RequireManager />,
            children: [{ path: 'users', element: <UsersListPage /> }],
          },
          // Ledger entries: any authenticated role (the backend scopes reads and gates writes).
          { path: 'ledger-entries', element: <LedgerEntriesListPage /> },
          // Bank statements: any authenticated role (the backend scopes reads and gates writes).
          { path: 'bank-statement-imports', element: <BankStatementImportsListPage /> },
          // Reconciliation: any authenticated role (the backend scopes reads and gates writes).
          { path: 'reconciliation', element: <ReconciliationBoardPage /> },
          {
            element: <RequireManagerOrAuditor />,
            children: [{ path: 'audit-trail', element: <AuditTrailPage /> }],
          },
        ],
      },
    ],
  },
])
