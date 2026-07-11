import { useMemo, useState } from 'react'
import { isAxiosError } from 'axios'
import { useTranslation } from 'react-i18next'
import { Button } from '@/components/ui/button'
import { isGlobalManager } from '@/features/auth/roles'
import { useAuth } from '@/features/auth/use-auth'
import { useSubsidiaryOptions } from '../hooks/use-subsidiary-options'
import { useDeactivateUser, useReactivateUser, useUsers } from '../hooks/use-users'
import type { User } from '../types'
import { CreateUserDialog } from './CreateUserDialog'
import { EditUserDialog } from './EditUserDialog'
import { ResetPasswordDialog } from './ResetPasswordDialog'

/**
 * User-management screen (Manager-only, gated by the route). Lists users within the caller's scope
 * and exposes create, edit role/subsidiary, activate/deactivate, and reset-password. A Subsidiary
 * Manager sees only their own subsidiary's users; the backend stays authoritative on every rule.
 */
export function UsersListPage() {
  const { t } = useTranslation('users')
  const { user: actingUser } = useAuth()
  const global = isGlobalManager(actingUser)

  const { data: users, isLoading, isError } = useUsers()
  const subsidiariesQuery = useSubsidiaryOptions(global)
  const deactivate = useDeactivateUser()
  const reactivate = useReactivateUser()

  const [creating, setCreating] = useState(false)
  const [editing, setEditing] = useState<User | null>(null)
  const [resetting, setResetting] = useState<User | null>(null)
  const [actionError, setActionError] = useState<string | null>(null)

  // id → "Name (CODE)" for display; only populated for a Global Manager (who can list subsidiaries).
  const subsidiaryLabels = useMemo(() => {
    const map = new Map<string, string>()
    for (const s of subsidiariesQuery.data ?? []) {
      map.set(s.id, `${s.name} (${s.code})`)
    }
    return map
  }, [subsidiariesQuery.data])

  function subsidiaryCell(user: User): string {
    if (user.subsidiaryId === null) return t('scope.global')
    return subsidiaryLabels.get(user.subsidiaryId) ?? t('scope.subsidiary')
  }

  async function onToggleActive(user: User) {
    setActionError(null)
    try {
      if (user.isActive) {
        await deactivate.mutateAsync(user.id)
      } else {
        await reactivate.mutateAsync(user.id)
      }
    } catch (error) {
      const status = isAxiosError(error) ? error.response?.status : undefined
      setActionError(status === 403 ? t('error.forbidden') : t('error.actionFailed'))
    }
  }

  const busy = deactivate.isPending || reactivate.isPending

  return (
    <div className="mx-auto max-w-5xl p-6">
      <div className="flex items-center justify-between">
        <div>
          <h1 className="text-2xl font-semibold tracking-tight">{t('title')}</h1>
          <p className="mt-1 text-sm text-muted-foreground">{t('subtitle')}</p>
        </div>
        <Button onClick={() => setCreating(true)}>{t('actions.new')}</Button>
      </div>

      {actionError && (
        <p role="alert" className="mt-4 rounded-md border border-destructive/50 bg-destructive/10 px-3 py-2 text-sm text-destructive">
          {actionError}
        </p>
      )}

      <div className="mt-6 overflow-x-auto rounded-lg border border-border">
        <table className="w-full text-sm">
          <thead className="bg-muted/50 text-left text-xs uppercase tracking-wide text-muted-foreground">
            <tr>
              <th className="px-4 py-3 font-semibold">{t('fields.name')}</th>
              <th className="px-4 py-3 font-semibold">{t('fields.email')}</th>
              <th className="px-4 py-3 font-semibold">{t('fields.role')}</th>
              <th className="px-4 py-3 font-semibold">{t('fields.subsidiary')}</th>
              <th className="px-4 py-3 font-semibold">{t('fields.status')}</th>
              <th className="px-4 py-3 text-right font-semibold">{t('fields.actions')}</th>
            </tr>
          </thead>
          <tbody className="divide-y divide-border">
            {isLoading && (
              <tr>
                <td colSpan={6} className="px-4 py-6 text-center text-muted-foreground">
                  {t('loading')}
                </td>
              </tr>
            )}
            {isError && (
              <tr>
                <td colSpan={6} className="px-4 py-6 text-center text-destructive">
                  {t('error.loadFailed')}
                </td>
              </tr>
            )}
            {!isLoading && !isError && users?.length === 0 && (
              <tr>
                <td colSpan={6} className="px-4 py-6 text-center text-muted-foreground">
                  {t('empty')}
                </td>
              </tr>
            )}
            {users?.map((user) => {
              const isSelf = user.id === actingUser?.id
              return (
                <tr key={user.id} className={user.isActive ? '' : 'text-muted-foreground'}>
                  <td className="px-4 py-3 font-medium">{user.name}</td>
                  <td className="px-4 py-3">{user.email}</td>
                  <td className="px-4 py-3">{t(`roles.${user.role}`)}</td>
                  <td className="px-4 py-3">{subsidiaryCell(user)}</td>
                  <td className="px-4 py-3">
                    <span
                      className={
                        user.isActive
                          ? 'inline-flex rounded-full bg-primary/10 px-2 py-0.5 text-xs font-medium text-primary'
                          : 'inline-flex rounded-full bg-muted px-2 py-0.5 text-xs font-medium text-muted-foreground'
                      }
                    >
                      {user.isActive ? t('status.active') : t('status.inactive')}
                    </span>
                  </td>
                  <td className="px-4 py-3">
                    <div className="flex justify-end gap-2">
                      <Button variant="ghost" onClick={() => setEditing(user)}>
                        {t('actions.edit')}
                      </Button>
                      <Button variant="ghost" onClick={() => setResetting(user)}>
                        {t('actions.resetPassword')}
                      </Button>
                      <Button
                        variant="ghost"
                        // A Manager cannot deactivate their own account (backend also blocks it).
                        disabled={busy || (isSelf && user.isActive)}
                        onClick={() => void onToggleActive(user)}
                      >
                        {user.isActive ? t('actions.deactivate') : t('actions.reactivate')}
                      </Button>
                    </div>
                  </td>
                </tr>
              )
            })}
          </tbody>
        </table>
      </div>

      {creating && <CreateUserDialog onClose={() => setCreating(false)} />}
      {editing && <EditUserDialog user={editing} onClose={() => setEditing(null)} />}
      {resetting && <ResetPasswordDialog user={resetting} onClose={() => setResetting(null)} />}
    </div>
  )
}
