import type { ReactNode } from 'react'
import { useTranslation } from 'react-i18next'

/** Labeled field wrapper; renders the field-level error (its message is an i18n key in the `reconciliation` namespace). */
export function Field({
  id,
  label,
  error,
  children,
}: {
  id: string
  label: string
  error?: string
  children: ReactNode
}) {
  const { t } = useTranslation('reconciliation')

  return (
    <div className="space-y-1.5">
      <label htmlFor={id} className="text-sm font-medium">
        {label}
      </label>
      {children}
      {error && <p className="text-sm text-destructive">{t(error)}</p>}
    </div>
  )
}
