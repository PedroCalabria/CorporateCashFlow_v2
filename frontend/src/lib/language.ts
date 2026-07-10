export type Language = 'en' | 'pt-BR'

export const SUPPORTED_LANGUAGES: Language[] = ['en', 'pt-BR']

/** localStorage key holding the client-side language preference. */
export const LANGUAGE_STORAGE_KEY = 'ct.lang'

/**
 * Resolve the initial UI language. A saved preference always wins; otherwise
 * the first browser/OS locale that maps to a supported language decides. Any
 * `pt*` locale maps to `pt-BR`; anything else falls back to English. Pure so it
 * can be unit-tested independently of i18next / navigator wiring.
 */
export function resolveInitialLanguage(
  saved: string | null | undefined,
  navigatorLangs: readonly string[] | null | undefined,
): Language {
  if (saved === 'en' || saved === 'pt-BR') return saved

  for (const locale of navigatorLangs ?? []) {
    if (locale.toLowerCase().startsWith('pt')) return 'pt-BR'
  }
  return 'en'
}
