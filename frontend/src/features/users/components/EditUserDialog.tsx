import { useState } from 'react'
import { zodResolver } from '@hookform/resolvers/zod'
import { useForm } from 'react-hook-form'
import { useTranslation } from 'react-i18next'
import { Button } from '@/components/ui/button'
import { isGlobalManager } from '@/features/auth/roles'
import { useAuth } from '@/features/auth/use-auth'
import { useSubsidiaryOptions } from '../hooks/use-subsidiary-options'
import { useUpdateUser } from '../hooks/use-users'
import { ALL_ROLES, SUBSIDIARY_MANAGER_ROLES, updateUserSchema, type UpdateUserFormValues } from '../schema'
import type { Role, User } from '../types'
import { controlClassWithError } from './control-styles'
import { Field } from './fields'
import { Modal } from './Modal'

/**
 * Edit a user's role/subsidiary. Scope-aware, same as create: a Subsidiary Manager sees only
 * Editor/Auditor and is locked to their own subsidiary. Backend rejects any elevation with 403.
 */
export function EditUserDialog({ user, onClose }: { user: User; onClose: () => void }) {
  const { t } = useTranslation('users')
  const { user: actingUser } = useAuth()
  const global = isGlobalManager(actingUser)
  const updateUser = useUpdateUser()
  const [formError, setFormError] = useState<string | null>(null)

  const subsidiariesQuery = useSubsidiaryOptions(global)
  const roles = global ? ALL_ROLES : SUBSIDIARY_MANAGER_ROLES

  const {
    register,
    handleSubmit,
    formState: { errors, isSubmitting },
  } = useForm<UpdateUserFormValues>({
    resolver: zodResolver(updateUserSchema),
    defaultValues: {
      role: user.role,
      subsidiaryId: user.subsidiaryId ?? '',
    },
  })

  async function onSubmit(values: UpdateUserFormValues) {
    setFormError(null)
    try {
      await updateUser.mutateAsync({
        id: user.id,
        input: {
          role: values.role as Role,
          subsidiaryId: values.subsidiaryId === '' ? null : values.subsidiaryId,
        },
      })
      onClose()
    } catch {
      setFormError(t('error.saveFailed'))
    }
  }

  return (
    <Modal title={t('edit.title')} onClose={onClose}>
      <p className="text-sm text-muted-foreground">
        {user.name} — {user.email}
      </p>
      <form className="mt-4 space-y-4" onSubmit={handleSubmit(onSubmit)} noValidate>
        <Field id="role" label={t('fields.role')} error={errors.role?.message}>
          <select id="role" className={controlClassWithError(!!errors.role)} {...register('role')}>
            {roles.map((role) => (
              <option key={role} value={role}>
                {t(`roles.${role}`)}
              </option>
            ))}
          </select>
        </Field>

        {global ? (
          <Field id="subsidiaryId" label={t('fields.subsidiary')} error={errors.subsidiaryId?.message}>
            <select
              id="subsidiaryId"
              className={controlClassWithError(!!errors.subsidiaryId)}
              {...register('subsidiaryId')}
            >
              <option value="">{t('scope.global')}</option>
              {subsidiariesQuery.data?.map((s) => (
                <option key={s.id} value={s.id}>
                  {s.name} ({s.code})
                </option>
              ))}
            </select>
          </Field>
        ) : (
          <input type="hidden" {...register('subsidiaryId')} />
        )}

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
