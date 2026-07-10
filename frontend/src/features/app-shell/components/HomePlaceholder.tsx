import { useTranslation } from 'react-i18next'

/**
 * Stand-in home screen rendered at the index route. Exists only so the shell's
 * content outlet has something to show until real business screens arrive.
 */
export function HomePlaceholder() {
  const { t } = useTranslation('app-shell')

  return (
    <div className="mx-auto max-w-2xl px-6 py-12">
      <h1 className="text-2xl font-semibold tracking-tight">{t('home.title')}</h1>
      <p className="mt-2 text-muted-foreground">{t('home.subtitle')}</p>
    </div>
  )
}
