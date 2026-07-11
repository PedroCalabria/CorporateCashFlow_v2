import { useState } from 'react'
import { zodResolver } from '@hookform/resolvers/zod'
import { useForm } from 'react-hook-form'
import { useTranslation } from 'react-i18next'
import { Button } from '@/components/ui/button'
import { isGlobalManager } from '@/features/auth/roles'
import { useAuth } from '@/features/auth/use-auth'
import { useSubsidiaryOptions } from '../hooks/use-subsidiary-options'
import { useCreateUser } from '../hooks/use-users'
import {
  ALL_ROLES,
  SUBSIDIARY_MANAGER_ROLES,
  createUserSchema,
  type CreateUserFormValues,
} from '../schema'
import type { Role } from '../types'
import { controlClassWithError } from './control-styles'
import { Field } from './fields'
import { Modal } from './Modal'

/**
 * Create-user form. Scope-aware: a Global Manager may pick any role and any subsidiary (or global);
 * a Subsidiary Manager may pick only Editor/Auditor and is locked to their own subsidiary. On
 * success the one-time generated password is shown once — the only time it is ever revealed.
 */
export function CreateUserDialog({ onClose }: { onClose: () => void }) {
  const { t } = useTranslation('users')
  const { user } = useAuth()
  const global = isGlobalManager(user)
  const createUser = useCreateUser()

  const [formError, setFormError] = useState<string | null>(null)
  const [initialPassword, setInitialPassword] = useState<string | null>(null)

  // Only a Global Manager can list subsidiaries; a Subsidiary Manager is locked to their own.
  const subsidiariesQuery = useSubsidiaryOptions(global)
  const roles = global ? ALL_ROLES : SUBSIDIARY_MANAGER_ROLES

  const {
    register,
    handleSubmit,
    formState: { errors, isSubmitting },
  } = useForm<CreateUserFormValues>({
    resolver: zodResolver(createUserSchema),
    defaultValues: {
      name: '',
      email: '',
      role: 'Editor',
      subsidiaryId: global ? '' : (user?.subsidiaryId ?? ''),
    },
  })

  async function onSubmit(values: CreateUserFormValues) {
    setFormError(null)
    try {
      const result = await createUser.mutateAsync({
        name: values.name,
        email: values.email,
        role: values.role as Role,
        subsidiaryId: values.subsidiaryId === '' ? null : values.subsidiaryId,
      })
      setInitialPassword(result.initialPassword)
    } catch {
      setFormError(t('error.saveFailed'))
    }
  }

  // Success view: reveal the one-time password, then close.
  if (initialPassword !== null) {
    return (
      <Modal title={t('create.createdTitle')} onClose={onClose}>
        <p className="text-sm text-muted-foreground">{t('create.passwordNotice')}</p>
        <p className="mt-3 select-all rounded-md border border-border bg-muted px-3 py-2 font-mono text-sm">
          {initialPassword}
        </p>
        <div className="mt-6 flex justify-end">
          <Button onClick={onClose}>{t('actions.done')}</Button>
        </div>
      </Modal>
    )
  }

  return (
    <Modal title={t('create.title')} onClose={onClose}>
      <form className="space-y-4" onSubmit={handleSubmit(onSubmit)} noValidate>
        <Field id="name" label={t('fields.name')} error={errors.name?.message}>
          <input id="name" className={controlClassWithError(!!errors.name)} {...register('name')} />
        </Field>

        <Field id="email" label={t('fields.email')} error={errors.email?.message}>
          <input
            id="email"
            type="email"
            autoComplete="off"
            className={controlClassWithError(!!errors.email)}
            {...register('email')}
          />
        </Field>

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
          // Subsidiary Manager: scope is fixed to their own subsidiary (hidden, not editable).
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
            {isSubmitting ? t('actions.saving') : t('actions.create')}
          </Button>
        </div>
      </form>
    </Modal>
  )
}
