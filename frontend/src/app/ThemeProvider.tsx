import { useCallback, useEffect, useMemo, useState } from 'react'
import type { ReactNode } from 'react'
import { resolveInitialTheme, THEME_STORAGE_KEY } from '@/lib/theme'
import type { Theme } from '@/lib/theme'
import { ThemeContext } from './theme-context'

/** Apply/remove the `dark` class on the document root so Tailwind's `dark:` variant keys off it. */
function applyThemeClass(theme: Theme) {
  const root = document.documentElement
  root.classList.toggle('dark', theme === 'dark')
}

function getInitialTheme(): Theme {
  const saved = localStorage.getItem(THEME_STORAGE_KEY)
  const prefersDark = window.matchMedia('(prefers-color-scheme: dark)').matches
  return resolveInitialTheme(saved, prefersDark)
}

/**
 * Holds the active theme in React state, mirrors it onto the document root
 * class (so the whole app restyles at once), and persists changes to
 * localStorage. The initial theme matches the inline applier in index.html to
 * avoid a flash. See design.md §D5.
 */
export function ThemeProvider({ children }: { children: ReactNode }) {
  const [theme, setThemeState] = useState<Theme>(getInitialTheme)

  useEffect(() => {
    applyThemeClass(theme)
    localStorage.setItem(THEME_STORAGE_KEY, theme)
  }, [theme])

  const setTheme = useCallback((next: Theme) => setThemeState(next), [])
  const toggleTheme = useCallback(
    () => setThemeState((prev) => (prev === 'dark' ? 'light' : 'dark')),
    [],
  )

  const value = useMemo(
    () => ({ theme, setTheme, toggleTheme }),
    [theme, setTheme, toggleTheme],
  )

  return <ThemeContext.Provider value={value}>{children}</ThemeContext.Provider>
}
