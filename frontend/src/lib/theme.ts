export type Theme = 'light' | 'dark'

/** localStorage key holding the client-side theme preference. */
export const THEME_STORAGE_KEY = 'ct.theme'

/**
 * Resolve the initial theme. A saved preference always wins; otherwise the OS
 * `prefers-color-scheme` decides. Pure so it can be unit-tested independently
 * of the DOM / matchMedia wiring.
 */
export function resolveInitialTheme(
  saved: string | null | undefined,
  prefersDark: boolean,
): Theme {
  if (saved === 'light' || saved === 'dark') return saved
  return prefersDark ? 'dark' : 'light'
}
