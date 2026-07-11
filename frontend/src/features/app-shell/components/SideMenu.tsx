import { useTranslation } from 'react-i18next'
import { NavLink } from 'react-router-dom'
import { LogOut } from 'lucide-react'
import { Button } from '@/components/ui/button'
import { isGlobalManager } from '@/features/auth/roles'
import { useAuth } from '@/features/auth/use-auth'
import { cn } from '@/lib/utils'
import { LanguageSwitcher } from './LanguageSwitcher'
import { ThemeToggle } from './ThemeToggle'

/**
 * Placeholder navigation entries. These capabilities have no screen yet, so they are static
 * labels (translated) with no route target — each real capability replaces its placeholder with
 * a link. See spec: "Navigation area shows placeholders only".
 */
const NAV_ITEM_KEYS = ['dashboard', 'ledgerEntries', 'reports', 'settings'] as const

/** First letters of the first and last name parts, e.g. "Ada Lovelace" → "AL". */
function initialsFor(name: string): string {
  const parts = name.trim().split(/\s+/).filter(Boolean)
  if (parts.length === 0) return '?'
  const first = parts[0]!.charAt(0)
  const last = parts.length > 1 ? parts[parts.length - 1]!.charAt(0) : ''
  return (first + last).toUpperCase()
}

export function SideMenu() {
  const { t } = useTranslation('app-shell')
  const { t: tAuth } = useTranslation('auth')
  const { user, logout } = useAuth()

  return (
    <aside className="flex h-full w-64 flex-col border-r border-border bg-card">
      {/* Real authenticated-user header (the mocked user has been removed). */}
      <div className="flex items-center gap-3 border-b border-border p-4">
        <div className="flex size-9 items-center justify-center rounded-full bg-primary text-sm font-medium text-primary-foreground">
          {user ? initialsFor(user.name) : '?'}
        </div>
        <div className="min-w-0">
          <p className="truncate text-sm font-medium">{user?.name}</p>
          <p className="truncate text-xs text-muted-foreground">{user?.role}</p>
        </div>
      </div>

      {/* Navigation area: real capability links (RBAC-gated) plus remaining placeholders. */}
      <nav className="flex-1 overflow-y-auto p-3" aria-label={t('nav.heading')}>
        <p className="px-2 pb-2 text-xs font-semibold uppercase tracking-wide text-muted-foreground">
          {t('nav.heading')}
        </p>
        <ul className="space-y-1">
          {/* Subsidiaries — a real link, shown only to the Global Manager. */}
          {isGlobalManager(user) && (
            <li>
              <NavLink
                to="/subsidiaries"
                className={({ isActive }) =>
                  cn(
                    'block rounded-md px-3 py-2 text-sm hover:bg-accent hover:text-accent-foreground',
                    isActive
                      ? 'bg-accent font-medium text-accent-foreground'
                      : 'text-foreground/70',
                  )
                }
              >
                {t('nav.subsidiaries')}
              </NavLink>
            </li>
          )}
          {NAV_ITEM_KEYS.map((key) => (
            <li key={key}>
              <span className="block cursor-default rounded-md px-3 py-2 text-sm text-foreground/70 hover:bg-accent hover:text-accent-foreground">
                {t(`nav.${key}`)}
              </span>
            </li>
          ))}
        </ul>
      </nav>

      {/* Fixed footer: language switcher, theme toggle, and sign out — reachable anywhere. */}
      <div className="flex flex-col gap-2 border-t border-border p-3">
        <ThemeToggle />
        <LanguageSwitcher />
        <Button variant="ghost" className="justify-start" onClick={() => void logout()}>
          <LogOut />
          {tAuth('logout')}
        </Button>
      </div>
    </aside>
  )
}
