import { useTranslation } from 'react-i18next'
import { LanguageSwitcher } from './LanguageSwitcher'
import { ThemeToggle } from './ThemeToggle'
import { mockCurrentUser } from '../mock-user'

/**
 * Placeholder navigation entries. No business screens exist yet, so these are
 * static labels (translated) with no real route targets — real capabilities
 * add their own entries and targets. See spec: "Navigation area shows
 * placeholders only".
 */
const NAV_ITEM_KEYS = ['dashboard', 'ledgerEntries', 'reports', 'settings'] as const

export function SideMenu() {
  const { t } = useTranslation('app-shell')

  return (
    <aside className="flex h-full w-64 flex-col border-r border-border bg-card">
      {/* Mocked current-user header (no real auth yet). */}
      <div className="flex items-center gap-3 border-b border-border p-4">
        <div className="flex size-9 items-center justify-center rounded-full bg-primary text-sm font-medium text-primary-foreground">
          {mockCurrentUser.initials}
        </div>
        <div className="min-w-0">
          <p className="truncate text-sm font-medium">{mockCurrentUser.name}</p>
          <p className="truncate text-xs text-muted-foreground">{t('user.role')}</p>
        </div>
      </div>

      {/* Navigation area (placeholders only). */}
      <nav className="flex-1 overflow-y-auto p-3" aria-label={t('nav.heading')}>
        <p className="px-2 pb-2 text-xs font-semibold uppercase tracking-wide text-muted-foreground">
          {t('nav.heading')}
        </p>
        <ul className="space-y-1">
          {NAV_ITEM_KEYS.map((key) => (
            <li key={key}>
              <span className="block cursor-default rounded-md px-3 py-2 text-sm text-foreground/70 hover:bg-accent hover:text-accent-foreground">
                {t(`nav.${key}`)}
              </span>
            </li>
          ))}
        </ul>
      </nav>

      {/* Fixed footer: language switcher + theme toggle, reachable from any screen. */}
      <div className="flex flex-col gap-2 border-t border-border p-3">
        <ThemeToggle />
        <LanguageSwitcher />
      </div>
    </aside>
  )
}
