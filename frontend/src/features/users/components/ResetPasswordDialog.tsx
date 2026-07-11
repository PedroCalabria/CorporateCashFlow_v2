import { useState } from 'react'
import { zodResolver } from '@hookform/resolvers/zod'
import { useForm } from 'react-hook-form'
import { useTranslation } from 'react-i18next'
import { Button } from '@/components/ui/button'
import { useResetPassword } from '../hooks/use-users'
import { resetPasswordSchema, type ResetPasswordFormValues } from '../schema'
import type { User } from '../types'
import { controlClassWithError } from './control-styles'
import { Field } from './fields'
import { Modal } from './Modal'

/**
 * Manager-set password reset. On success the user's outstanding sessions are revoked server-side
 * (design.md §D4) — surfaced to the Manager as a notice so they know old sessions end.
 */
export function ResetPasswordDialog({ user, onClose }: { user: User; onClose: () => void }) {
  const { t } = useTranslation('users')
  const resetPassword = useResetPassword()
  const [formError, setFormError] = useState<string | null>(null)

  const {
    register,
    handleSubmit,
    formState: { errors, isSubmitting },
  } = useForm<ResetPasswordFormValues>({
    resolver: zodResolver(resetPasswordSchema),
    defaultValues: { newPassword: '' },
  })

  async function onSubmit(values: ResetPasswordFormValues) {
    setFormError(null)
    try {
      await resetPassword.mutateAsync({ id: user.id, input: { newPassword: values.newPassword } })
      onClose()
    } catch {
      setFormError(t('error.actionFailed'))
    }
  }

  return (
    <Modal title={t('reset.title')} onClose={onClose}>
      <p className="text-sm text-muted-foreground">
        {user.name} — {user.email}
      </p>
      <p className="mt-2 text-sm text-muted-foreground">{t('reset.notice')}</p>
      <form className="mt-4 space-y-4" onSubmit={handleSubmit(onSubmit)} noValidate>
        <Field id="newPassword" label={t('fields.newPassword')} error={errors.newPassword?.message}>
          <input
            id="newPassword"
            type="text"
            autoComplete="off"
            className={controlClassWithError(!!errors.newPassword)}
            {...register('newPassword')}
          />
        </Field>

        {formError && (
          <p role="alert" className="text-sm text-destructive">
            {formError}
          </p>
        )}

        <div className="flex justify-end gap-2 pt-2">
          <Button type="button" variant="ghost" onClick={onClose}>
            {t('actions.cancel')}
          </Button>
          <Button type="submit" disabled={isSubmitting}>
            {isSubmitting ? t('actions.saving') : t('actions.reset')}
          </Button>
        </div>
      </form>
    </Modal>
  )
}
