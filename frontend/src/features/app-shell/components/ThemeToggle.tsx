import { Moon, Sun } from 'lucide-react'
import { useTranslation } from 'react-i18next'
import { Button } from '@/components/ui/button'
import { useTheme } from '@/app/use-theme'

/**
 * Footer control that toggles light/dark. The change is applied immediately via
 * the ThemeProvider (which flips the root `dark` class), so no reload is needed.
 */
export function ThemeToggle() {
  const { t } = useTranslation()
  const { theme, toggleTheme } = useTheme()
  const isDark = theme === 'dark'

  return (
    <Button
      variant="ghost"
      size="sm"
      onClick={toggleTheme}
      aria-label={t('theme.toggle')}
      title={t('theme.toggle')}
      className="justify-start gap-2"
    >
      {isDark ? <Moon /> : <Sun />}
      <span>{isDark ? t('theme.dark') : t('theme.light')}</span>
    </Button>
  )
}
