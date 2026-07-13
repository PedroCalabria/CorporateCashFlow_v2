import { useState } from 'react'
import { zodResolver } from '@hookform/resolvers/zod'
import { useForm } from 'react-hook-form'
import { useTranslation } from 'react-i18next'
import { Button } from '@/components/ui/button'
import { hasFieldError } from '@/lib/api-error'
import { useUpdateSubsidiary } from '../hooks/use-subsidiaries'
import { updateSubsidiarySchema, type UpdateSubsidiaryFormValues } from '../schema'
import type { Subsidiary } from '../types'
import { FormField } from './fields'
import { Modal } from './Modal'

/**
 * Edit form — Name/Code only. There are deliberately no InitialBalance/ReferenceDate fields: the
 * bank-account baseline is immutable and exposed on no edit contract (design.md §D4).
 */
export function EditSubsidiaryDialog({
  subsidiary,
  onClose,
}: {
  subsidiary: Subsidiary
  onClose: () => void
}) {
  const { t } = useTranslation('subsidiaries')
  const updateSubsidiary = useUpdateSubsidiary()
  const [formError, setFormError] = useState<string | null>(null)

  const {
    register,
    handleSubmit,
    formState: { errors, isSubmitting },
  } = useForm<UpdateSubsidiaryFormValues>({
    resolver: zodResolver(updateSubsidiarySchema),
    defaultValues: { name: subsidiary.name, code: subsidiary.code },
  })

  async function onSubmit(values: UpdateSubsidiaryFormValues) {
    setFormError(null)
    try {
      await updateSubsidiary.mutateAsync({ id: subsidiary.id, input: values })
      onClose()
    } catch (error) {
      setFormError(hasFieldError(error, 'Code') ? t('error.codeInUse') : t('error.saveFailed'))
    }
  }

  return (
    <Modal title={t('edit.title')} onClose={onClose}>
      <form className="space-y-4" onSubmit={handleSubmit(onSubmit)} noValidate>
        <FormField
          id="name"
          label={t('fields.name')}
          error={errors.name}
          registration={register('name')}
        />
        <FormField
          id="code"
          label={t('fields.code')}
          error={errors.code}
          registration={register('code')}
        />

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
