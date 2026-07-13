import type { InputHTMLAttributes, ReactNode } from 'react'
import type { FieldError, UseFormRegisterReturn } from 'react-hook-form'
import { useTranslation } from 'react-i18next'
import { subsidiaryControlClass } from './control-styles'

/**
 * Shared labeled input for the subsidiary forms. Renders the field-level Zod error (its message is
 * an i18n key resolved in the `subsidiaries` namespace). Pass `children` to supply a custom control
 * (e.g. a currency-masked input driven by a `Controller`) instead of the default `<input>`; the
 * label and error rendering stay the same.
 */
export function FormField({
  id,
  label,
  error,
  registration,
  children,
  ...inputProps
}: {
  id: string
  label: string
  error?: FieldError
  registration?: UseFormRegisterReturn
  children?: ReactNode
} & InputHTMLAttributes<HTMLInputElement>) {
  const { t } = useTranslation('subsidiaries')

  return (
    <div className="space-y-1.5">
      <label htmlFor={id} className="text-sm font-medium">
        {label}
      </label>
      {children ?? (
        <input
          id={id}
          aria-invalid={error ? true : undefined}
          className={subsidiaryControlClass(!!error)}
          {...inputProps}
          {...registration}
        />
      )}
      {error && <p className="text-sm text-destructive">{t(error.message ?? '')}</p>}
    </div>
  )
}
