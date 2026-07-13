import { useState } from 'react'
import { zodResolver } from '@hookform/resolvers/zod'
import { useForm } from 'react-hook-form'
import { useTranslation } from 'react-i18next'
import { Button } from '@/components/ui/button'
import { useRejectBatch } from '../hooks/use-bank-statement-imports'
import { rejectBatchSchema, type RejectBatchFormValues } from '../schema'
import type { BankStatementBatch } from '../types'
import { controlClassWithError } from './control-styles'
import { Field } from './Field'
import { Modal } from './Modal'

/**
 * Manager-only full-batch rejection that requires a reason (§2.2 transition 2). Rejecting invalidates
 * every line in the batch (all-or-nothing); the backend re-enforces the Manager-only rule and the
 * mandatory reason.
 */
export function RejectBatchDialog({ batch, onClose }: { batch: BankStatementBatch; onClose: () => void }) {
  const { t } = useTranslation('bank-statement-import')
  const rejectBatch = useRejectBatch()
  const [formError, setFormError] = useState<string | null>(null)

  const {
    register,
    handleSubmit,
    formState: { errors, isSubmitting },
  } = useForm<RejectBatchFormValues>({
    resolver: zodResolver(rejectBatchSchema),
    defaultValues: { rejectionReason: '' },
  })

  async function onSubmit(values: RejectBatchFormValues) {
    setFormError(null)
    try {
      await rejectBatch.mutateAsync({ id: batch.id, reason: values.rejectionReason })
      onClose()
    } catch {
      setFormError(t('error.actionFailed'))
    }
  }

  return (
    <Modal title={t('reject.title')} onClose={onClose}>
      <p className="text-sm text-muted-foreground">{t('reject.notice')}</p>
      <p className="mt-2 text-sm">
        {batch.fileName} — {t('reject.lineSummary', { count: batch.lineCount })}
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
