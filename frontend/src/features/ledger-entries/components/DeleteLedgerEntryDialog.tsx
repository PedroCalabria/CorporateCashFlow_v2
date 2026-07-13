import { useState } from 'react'
import { zodResolver } from '@hookform/resolvers/zod'
import { useForm } from 'react-hook-form'
import { useTranslation } from 'react-i18next'
import { Button } from '@/components/ui/button'
import { useDeleteLedgerEntry } from '../hooks/use-ledger-entries'
import { deleteLedgerEntrySchema, type DeleteLedgerEntryFormValues } from '../schema'
import type { LedgerEntry } from '../types'
import { controlClassWithError } from './control-styles'
import { Field } from './Field'
import { Modal } from './Modal'

/** Manager-only soft-delete confirmation that requires a reason (§1.2 rule 9). */
export function DeleteLedgerEntryDialog({ entry, onClose }: { entry: LedgerEntry; onClose: () => void }) {
  const { t } = useTranslation('ledger-entries')
  const deleteEntry = useDeleteLedgerEntry()
  const [formError, setFormError] = useState<string | null>(null)

  const {
    register,
    handleSubmit,
    formState: { errors, isSubmitting },
  } = useForm<DeleteLedgerEntryFormValues>({
    resolver: zodResolver(deleteLedgerEntrySchema),
    defaultValues: { deletionReason: '' },
  })

  async function onSubmit(values: DeleteLedgerEntryFormValues) {
    setFormError(null)
    try {
      await deleteEntry.mutateAsync({ id: entry.id, reason: values.deletionReason })
      onClose()
    } catch {
      setFormError(t('error.actionFailed'))
    }
  }

  return (
    <Modal title={t('delete.title')} onClose={onClose}>
      <p className="text-sm text-muted-foreground">{t('delete.notice')}</p>
      <p className="mt-2 text-sm">
        {entry.description} — {entry.amount.toFixed(2)} ({entry.date})
      </p>
      <form className="mt-4 space-y-4" onSubmit={handleSubmit(onSubmit)} noValidate>
        <Field id="deletionReason" label={t('fields.deletionReason')} error={errors.deletionReason?.message}>
          <input id="deletionReason" className={controlClassWithError(!!errors.deletionReason)} {...register('deletionReason')} />
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
            {isSubmitting ? t('actions.saving') : t('actions.delete')}
          </Button>
        </div>
      </form>
    </Modal>
  )
}
