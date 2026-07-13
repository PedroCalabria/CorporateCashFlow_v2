import { useState } from 'react'
import { zodResolver } from '@hookform/resolvers/zod'
import { useForm } from 'react-hook-form'
import { useTranslation } from 'react-i18next'
import { Button } from '@/components/ui/button'
import { useCategories, useUpdateLedgerEntry } from '../hooks/use-ledger-entries'
import { updateLedgerEntrySchema, type UpdateLedgerEntryFormValues } from '../schema'
import type { LedgerEntry } from '../types'
import { controlClassWithError } from './control-styles'
import { Field } from './Field'
import { Modal } from './Modal'

/** Edit form for an Open entry (category/amount/date/description). The backend rejects edits to non-Open entries. */
export function EditLedgerEntryDialog({ entry, onClose }: { entry: LedgerEntry; onClose: () => void }) {
  const { t } = useTranslation('ledger-entries')
  const updateEntry = useUpdateLedgerEntry()
  const categoriesQuery = useCategories()
  const [formError, setFormError] = useState<string | null>(null)

  const {
    register,
    handleSubmit,
    formState: { errors, isSubmitting },
  } = useForm<UpdateLedgerEntryFormValues>({
    resolver: zodResolver(updateLedgerEntrySchema),
    defaultValues: {
      categoryId: entry.categoryId,
      amount: entry.amount,
      date: entry.date,
      description: entry.description,
    },
  })

  async function onSubmit(values: UpdateLedgerEntryFormValues) {
    setFormError(null)
    try {
      await updateEntry.mutateAsync({ id: entry.id, input: values })
      onClose()
    } catch {
      setFormError(t('error.saveFailed'))
    }
  }

  return (
    <Modal title={t('edit.title')} onClose={onClose}>
      <form className="space-y-4" onSubmit={handleSubmit(onSubmit)} noValidate>
        <Field id="categoryId" label={t('fields.category')} error={errors.categoryId?.message}>
          <select id="categoryId" className={controlClassWithError(!!errors.categoryId)} {...register('categoryId')}>
            {categoriesQuery.data?.map((c) => (
              <option key={c.id} value={c.id}>
                {c.name} ({t(`categoryType.${c.type}`)})
              </option>
            ))}
          </select>
        </Field>

        <Field id="amount" label={t('fields.amount')} error={errors.amount?.message}>
          <input id="amount" type="number" step="0.01" className={controlClassWithError(!!errors.amount)} {...register('amount')} />
        </Field>

        <Field id="date" label={t('fields.date')} error={errors.date?.message}>
          <input id="date" type="date" className={controlClassWithError(!!errors.date)} {...register('date')} />
        </Field>

        <Field id="description" label={t('fields.description')} error={errors.description?.message}>
          <input id="description" className={controlClassWithError(!!errors.description)} {...register('description')} />
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
            {isSubmitting ? t('actions.saving') : t('actions.save')}
          </Button>
        </div>
      </form>
    </Modal>
  )
}
