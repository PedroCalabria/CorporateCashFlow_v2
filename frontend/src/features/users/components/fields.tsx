import type { ReactNode } from 'react'
import { useTranslation } from 'react-i18next'

/**
 * Labeled field wrapper for the user forms. Renders the field-level error (its message is an i18n
 * key resolved in the `users` namespace). The control itself (input/select) is passed as children
 * so both text inputs and dropdowns share one layout. Control styling lives in `control-styles.ts`.
 */
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
  const { t } = useTranslation('users')

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
