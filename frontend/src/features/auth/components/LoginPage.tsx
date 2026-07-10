import { useState } from 'react'
import { zodResolver } from '@hookform/resolvers/zod'
import { useForm } from 'react-hook-form'
import { useTranslation } from 'react-i18next'
import { Navigate, useNavigate } from 'react-router-dom'
import { Button } from '@/components/ui/button'
import { cn } from '@/lib/utils'
import { loginSchema, type LoginFormValues } from '../schema'
import { useAuth } from '../use-auth'

/**
 * Public login screen (outside the App Shell). React Hook Form + Zod block the submit on a
 * malformed email or empty password (no network call); a failed login shows a single
 * generic error, never revealing which field was wrong (spec: "Frontend login gate").
 */
export function LoginPage() {
  const { t } = useTranslation('auth')
  const navigate = useNavigate()
  const { login, status, isAuthenticated } = useAuth()
  const [genericError, setGenericError] = useState<string | null>(null)

  const {
    register,
    handleSubmit,
    formState: { errors, isSubmitting },
  } = useForm<LoginFormValues>({
    resolver: zodResolver(loginSchema),
    defaultValues: { email: '', password: '' },
  })

  // Already signed in (e.g. silent refresh succeeded) — skip the form.
  if (status === 'authenticated' && isAuthenticated) {
    return <Navigate to="/" replace />
  }

  async function onSubmit(values: LoginFormValues) {
    setGenericError(null)
    try {
      await login(values.email, values.password)
      navigate('/', { replace: true })
    } catch {
      setGenericError(t('error.invalid'))
    }
  }

  return (
    <div className="flex min-h-screen items-center justify-center bg-background px-4 text-foreground">
      <div className="w-full max-w-sm rounded-lg border border-border bg-card p-6 shadow-sm">
        <h1 className="text-xl font-semibold tracking-tight">{t('title')}</h1>
        <p className="mt-1 text-sm text-muted-foreground">{t('subtitle')}</p>

        <form className="mt-6 space-y-4" onSubmit={handleSubmit(onSubmit)} noValidate>
          <div className="space-y-1.5">
            <label htmlFor="email" className="text-sm font-medium">
              {t('email.label')}
            </label>
            <input
              id="email"
              type="email"
              autoComplete="username"
              placeholder={t('email.placeholder')}
              aria-invalid={errors.email ? true : undefined}
              className={cn(
                'flex h-9 w-full rounded-md border border-input bg-background px-3 py-1 text-sm shadow-xs outline-none focus-visible:border-ring focus-visible:ring-[3px] focus-visible:ring-ring/50',
                errors.email && 'border-destructive',
              )}
              {...register('email')}
            />
            {errors.email && (
              <p className="text-sm text-destructive">{t(errors.email.message ?? '')}</p>
            )}
          </div>

          <div className="space-y-1.5">
            <label htmlFor="password" className="text-sm font-medium">
              {t('password.label')}
            </label>
            <input
              id="password"
              type="password"
              autoComplete="current-password"
              aria-invalid={errors.password ? true : undefined}
              className={cn(
                'flex h-9 w-full rounded-md border border-input bg-background px-3 py-1 text-sm shadow-xs outline-none focus-visible:border-ring focus-visible:ring-[3px] focus-visible:ring-ring/50',
                errors.password && 'border-destructive',
              )}
              {...register('password')}
            />
            {errors.password && (
              <p className="text-sm text-destructive">{t(errors.password.message ?? '')}</p>
            )}
          </div>

          {genericError && (
            <p role="alert" className="text-sm text-destructive">
              {genericError}
            </p>
          )}

          <Button type="submit" className="w-full" disabled={isSubmitting}>
            {isSubmitting ? t('submitting') : t('submit')}
          </Button>
        </form>
      </div>
    </div>
  )
}
