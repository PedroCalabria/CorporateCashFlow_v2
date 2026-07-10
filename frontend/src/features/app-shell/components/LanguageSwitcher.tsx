import { Languages } from 'lucide-react'
import { useTranslation } from 'react-i18next'
import { Button } from '@/components/ui/button'
import { SUPPORTED_LANGUAGES } from '@/lib/language'
import type { Language } from '@/lib/language'

/**
 * Footer control that switches the active language. `i18n.changeLanguage`
 * re-renders every consumer immediately and the detector caches the choice to
 * localStorage — applied across the whole UI with no page reload.
 */
export function LanguageSwitcher() {
  const { t, i18n } = useTranslation()
  const current = (
    SUPPORTED_LANGUAGES as readonly string[]
  ).includes(i18n.resolvedLanguage ?? '')
    ? (i18n.resolvedLanguage as Language)
    : 'en'

  return (
    <div className="flex items-center gap-1" role="group" aria-label={t('language.label')}>
      <Languages className="size-4 text-muted-foreground" aria-hidden="true" />
      {SUPPORTED_LANGUAGES.map((lng) => (
        <Button
          key={lng}
          variant={lng === current ? 'secondary' : 'ghost'}
          size="sm"
          aria-pressed={lng === current}
          onClick={() => void i18n.changeLanguage(lng)}
        >
          {lng === 'pt-BR' ? 'PT' : 'EN'}
        </Button>
      ))}
    </div>
  )
}
