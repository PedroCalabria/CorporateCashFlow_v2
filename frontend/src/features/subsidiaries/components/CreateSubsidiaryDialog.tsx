import { useState } from 'react'
import { zodResolver } from '@hookform/resolvers/zod'
import { Controller, useForm } from 'react-hook-form'
import { useTranslation } from 'react-i18next'
import { Button } from '@/components/ui/button'
import { CurrencyInput } from '@/components/ui/currency-input'
import { hasFieldError } from '@/lib/api-error'
import { todayIso } from '@/lib/date'
import { useCreateSubsidiary } from '../hooks/use-subsidiaries'
import { createSubsidiarySchema, type CreateSubsidiaryFormValues } from '../schema'
import { subsidiaryControlClass } from './control-styles'
import { FormField } from './fields'
import { Modal } from './Modal'

/**
 * Create form (React Hook Form + Zod). Includes InitialBalance and ReferenceDate in the same
 * form — the only place the immutable baseline is ever entered (spec: create-with-baseline).
 */
export function CreateSubsidiaryDialog({ onClose }: { onClose: () => void }) {
  const { t } = useTranslation('subsidiaries')
  const createSubsidiary = useCreateSubsidiary()
  const [formError, setFormError] = useState<string | null>(null)

  const {
    register,
    control,
    handleSubmit,
    formState: { errors, isSubmitting },
  } = useForm<CreateSubsidiaryFormValues>({
    resolver: zodResolver(createSubsidiarySchema),
    defaultValues: { name: '', code: '', initialBalance: 0, referenceDate: '' },
  })

  async function onSubmit(values: CreateSubsidiaryFormValues) {
    setFormError(null)
    try {
      await createSubsidiary.mutateAsync(values)
      onClose()
    } catch (error) {
      // Map the backend's field-keyed ValidationProblemDetails to a specific message.
      if (hasFieldError(error, 'ReferenceDate')) {
        setFormError(t('error.referenceDateFuture'))
      } else if (hasFieldError(error, 'Code')) {
        setFormError(t('error.codeInUse'))
      } else {
        setFormError(t('error.saveFailed'))
      }
    }
  }

  return (
    <Modal title={t('create.title')} onClose={onClose}>
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
        <FormField id="initialBalance" label={t('fields.initialBalance')} error={errors.initialBalance}>
          <Controller
            control={control}
            name="initialBalance"
            render={({ field }) => (
              <CurrencyInput
                id="initialBalance"
                name={field.name}
                value={field.value}
                onChange={field.onChange}
                onBlur={field.onBlur}
                className={subsidiaryControlClass(!!errors.initialBalance)}
                allowNegative
              />
            )}
          />
        </FormField>
        <FormField
          id="referenceDate"
          label={t('fields.referenceDate')}
          type="date"
          max={todayIso()}
          error={errors.referenceDate}
          registration={register('referenceDate')}
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
            {isSubmitting ? t('actions.saving') : t('actions.create')}
          </Button>
        </div>
      </form>
    </Modal>
  )
}
