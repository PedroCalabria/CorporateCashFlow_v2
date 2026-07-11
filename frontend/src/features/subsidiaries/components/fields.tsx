import type { InputHTMLAttributes } from 'react'
import type { FieldError, UseFormRegisterReturn } from 'react-hook-form'
import { useTranslation } from 'react-i18next'
import { cn } from '@/lib/utils'

/**
 * Shared labeled input for the subsidiary forms. Renders the field-level Zod error (its message
 * is an i18n key resolved in the `subsidiaries` namespace). Matches the login form's input styling.
 */
export function FormField({
  id,
  label,
  error,
  registration,
  ...inputProps
}: {
  id: string
  label: string
  error?: FieldError
  registration: UseFormRegisterReturn
} & InputHTMLAttributes<HTMLInputElement>) {
  const { t } = useTranslation('subsidiaries')

  return (
    <div className="space-y-1.5">
      <label htmlFor={id} className="text-sm font-medium">
        {label}
      </label>
      <input
        id={id}
        aria-invalid={error ? true : undefined}
        className={cn(
          'flex h-9 w-full rounded-md border border-input bg-background px-3 py-1 text-sm shadow-xs outline-none focus-visible:border-ring focus-visible:ring-[3px] focus-visible:ring-ring/50',
          error && 'border-destructive',
        )}
        {...inputProps}
        {...registration}
      />
      {error && <p className="text-sm text-destructive">{t(error.message ?? '')}</p>}
    </div>
  )
}
