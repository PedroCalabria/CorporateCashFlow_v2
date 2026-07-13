import { AxiosError } from 'axios'

/**
 * ASP.NET Core returns validation failures as RFC-7807 `ValidationProblemDetails`:
 * `{ errors: { FieldName: ["message", ...] } }`. These helpers read that shape off an axios error so
 * the UI can show a message tailored to the field that failed instead of a single generic string.
 */
export function getValidationErrors(error: unknown): Record<string, string[]> | null {
  if (error instanceof AxiosError) {
    const data = error.response?.data as { errors?: Record<string, string[]> } | undefined
    if (data?.errors && typeof data.errors === 'object') {
      return data.errors
    }
  }
  return null
}

/** True when the validation response carries an error for `field` (case-insensitive, since the key may be camelCased). */
export function hasFieldError(error: unknown, field: string): boolean {
  const errors = getValidationErrors(error)
  if (!errors) {
    return false
  }
  return Object.keys(errors).some((key) => key.toLowerCase() === field.toLowerCase())
}
