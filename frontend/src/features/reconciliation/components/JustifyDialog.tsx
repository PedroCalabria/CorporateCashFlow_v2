import { useState } from 'react'
import { zodResolver } from '@hookform/resolvers/zod'
import { useForm } from 'react-hook-form'
import { useTranslation } from 'react-i18next'
import { Button } from '@/components/ui/button'
import { useJustifyEntry } from '../hooks/use-reconciliation'
import { justifySchema, type JustifyFormValues } from '../schema'
import type { PendingLedgerEntry } from '../types'
import { controlClassWithError } from './control-styles'
import { Field } from './Field'
import { Modal } from './Modal'

/**
 * Editor justification for a PendingReconciliation entry (§1.2 transition 5). A non-empty justification
 * is mandatory; on success the entry moves to PendingApproval and is locked from Editor edits. The
 * backend re-enforces the role/scope and the mandatory text.
 */
export function JustifyDialog({ entry, onClose }: { entry: PendingLedgerEntry; onClose: () => void }) {
  const { t } = useTranslation('reconciliation')
  const justify = useJustifyEntry()
  const [formError, setFormError] = useState<string | null>(null)

  const {
    register,
    handleSubmit,
    formState: { errors, isSubmitting },
  } = useForm<JustifyFormValues>({
    resolver: zodResolver(justifySchema),
    defaultValues: { justificationText: '' },
  })

  async function onSubmit(values: JustifyFormValues) {
    setFormError(null)
    try {
      await justify.mutateAsync({ id: entry.id, justificationText: values.justificationText })
      onClose()
    } catch {
      setFormError(t('error.actionFailed'))
    }
  }

  return (
    <Modal title={t('justify.title')} onClose={onClose}>
      <p className="text-sm text-muted-foreground">{t('justify.notice')}</p>
      <p className="mt-2 text-sm">
        {entry.description} — {entry.date}
      </p>
      <form className="mt-4 space-y-4" onSubmit={handleSubmit(onSubmit)} noValidate>
        <Field id="justificationText" label={t('fields.justificationText')} error={errors.justificationText?.message}>
          <textarea
            id="justificationText"
            rows={4}
            className={controlClassWithError(!!errors.justificationText) + ' h-auto'}
            {...register('justificationText')}
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
            {isSubmitting ? t('actions.saving') : t('actions.submitJustification')}
          </Button>
        </div>
      </form>
    </Modal>
  )
}
