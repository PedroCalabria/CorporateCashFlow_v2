import { useState } from 'react'
import { zodResolver } from '@hookform/resolvers/zod'
import { useForm } from 'react-hook-form'
import { useTranslation } from 'react-i18next'
import { Button } from '@/components/ui/button'
import { useRejectEntry } from '../hooks/use-reconciliation'
import { rejectSchema, type RejectFormValues } from '../schema'
import type { PendingApprovalEntry } from '../types'
import { controlClassWithError } from './control-styles'
import { Field } from './Field'
import { Modal } from './Modal'

/**
 * Manager rejection of a justified entry (§1.2 transition 7). A non-empty reason is mandatory; on
 * success the entry returns to PendingReconciliation for a fresh justification cycle. The backend
 * re-enforces the Manager-only rule and the mandatory reason.
 */
export function RejectDialog({ entry, onClose }: { entry: PendingApprovalEntry; onClose: () => void }) {
  const { t } = useTranslation('reconciliation')
  const reject = useRejectEntry()
  const [formError, setFormError] = useState<string | null>(null)

  const {
    register,
    handleSubmit,
    formState: { errors, isSubmitting },
  } = useForm<RejectFormValues>({
    resolver: zodResolver(rejectSchema),
    defaultValues: { rejectionReason: '' },
  })

  async function onSubmit(values: RejectFormValues) {
    setFormError(null)
    try {
      await reject.mutateAsync({ id: entry.id, rejectionReason: values.rejectionReason })
      onClose()
    } catch {
      setFormError(t('error.actionFailed'))
    }
  }

  return (
    <Modal title={t('reject.title')} onClose={onClose}>
      <p className="text-sm text-muted-foreground">{t('reject.notice')}</p>
      <p className="mt-2 text-sm">
        {entry.description} — {entry.date}
      </p>
      <form className="mt-4 space-y-4" onSubmit={handleSubmit(onSubmit)} noValidate>
        <Field id="rejectionReason" label={t('fields.rejectionReason')} error={errors.rejectionReason?.message}>
          <input
            id="rejectionReason"
            className={controlClassWithError(!!errors.rejectionReason)}
            {...register('rejectionReason')}
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
            {isSubmitting ? t('actions.saving') : t('actions.reject')}
          </Button>
        </div>
      </form>
    </Modal>
  )
}
