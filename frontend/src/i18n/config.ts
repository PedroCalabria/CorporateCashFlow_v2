import i18n from 'i18next'
import { initReactI18next } from 'react-i18next'
import LanguageDetector from 'i18next-browser-languagedetector'
import { LANGUAGE_STORAGE_KEY, SUPPORTED_LANGUAGES } from '@/lib/language'

import commonEn from './en.json'
import commonPtBR from './pt-BR.json'
import appShellEn from '@/features/app-shell/i18n/en.json'
import appShellPtBR from '@/features/app-shell/i18n/pt-BR.json'

/**
 * i18next setup (design.md §D4). One namespace per feature: `common` holds
 * shared strings, `app-shell` holds the shell's nav/footer/placeholder strings.
 * Detection order is localStorage (saved preference) → navigator (browser/OS),
 * with the detected value cached back to localStorage under `ct.lang` so the
 * choice survives a reload without any account/server involvement.
 */
export const defaultNS = 'common'

export const resources = {
  en: {
    common: commonEn,
    'app-shell': appShellEn,
  },
  'pt-BR': {
    common: commonPtBR,
    'app-shell': appShellPtBR,
  },
} as const

void i18n
  .use(LanguageDetector)
  .use(initReactI18next)
  .init({
    resources,
    supportedLngs: SUPPORTED_LANGUAGES,
    fallbackLng: 'en',
    load: 'currentOnly',
    defaultNS,
    ns: ['common', 'app-shell'],
    interpolation: { escapeValue: false },
    detection: {
      order: ['localStorage', 'navigator'],
      lookupLocalStorage: LANGUAGE_STORAGE_KEY,
      caches: ['localStorage'],
    },
  })

export default i18n
