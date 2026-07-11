import { useState } from 'react'
import { isAxiosError } from 'axios'
import { useTranslation } from 'react-i18next'
import { Button } from '@/components/ui/button'
import {
  useDeactivateSubsidiary,
  useReactivateSubsidiary,
  useSubsidiaries,
} from '../hooks/use-subsidiaries'
import type { Subsidiary } from '../types'
import { CreateSubsidiaryDialog } from './CreateSubsidiaryDialog'
import { EditSubsidiaryDialog } from './EditSubsidiaryDialog'

/**
 * Subsidiary management screen (Global-Manager-only, gated by the route). Lists all subsidiaries
 * with their baseline and active/inactive state, and exposes create, edit, and activate/deactivate
 * actions. A blocked deactivation (active users still assigned) surfaces a clear message.
 */
export function SubsidiariesListPage() {
  const { t } = useTranslation('subsidiaries')
  const { data: subsidiaries, isLoading, isError } = useSubsidiaries()
  const deactivate = useDeactivateSubsidiary()
  const reactivate = useReactivateSubsidiary()

  const [creating, setCreating] = useState(false)
  const [editing, setEditing] = useState<Subsidiary | null>(null)
  const [actionError, setActionError] = useState<string | null>(null)

  async function onToggleActive(subsidiary: Subsidiary) {
    setActionError(null)
    try {
      if (subsidiary.isActive) {
        await deactivate.mutateAsync(subsidiary.id)
      } else {
        await reactivate.mutateAsync(subsidiary.id)
      }
    } catch (error) {
      // The deactivation guard returns 409 with code SUBSIDIARY_HAS_ACTIVE_USERS.
      const code = isAxiosError(error) ? error.response?.data?.code : undefined
      setActionError(
        code === 'SUBSIDIARY_HAS_ACTIVE_USERS' ? t('error.hasActiveUsers') : t('error.actionFailed'),
      )
    }
  }

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
              <th className="px-4 py-3 font-semibold">{t('fields.code')}</th>
              <th className="px-4 py-3 font-semibold">{t('fields.initialBalance')}</th>
              <th className="px-4 py-3 font-semibold">{t('fields.referenceDate')}</th>
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
            {!isLoading && !isError && subsidiaries?.length === 0 && (
              <tr>
                <td colSpan={6} className="px-4 py-6 text-center text-muted-foreground">
                  {t('empty')}
                </td>
              </tr>
            )}
            {subsidiaries?.map((subsidiary) => (
              <tr key={subsidiary.id} className={subsidiary.isActive ? '' : 'text-muted-foreground'}>
                <td className="px-4 py-3 font-medium">{subsidiary.name}</td>
                <td className="px-4 py-3">{subsidiary.code}</td>
                <td className="px-4 py-3">{subsidiary.initialBalance.toFixed(2)}</td>
                <td className="px-4 py-3">{subsidiary.referenceDate}</td>
                <td className="px-4 py-3">
                  <span
                    className={
                      subsidiary.isActive
                        ? 'inline-flex rounded-full bg-primary/10 px-2 py-0.5 text-xs font-medium text-primary'
                        : 'inline-flex rounded-full bg-muted px-2 py-0.5 text-xs font-medium text-muted-foreground'
                    }
                  >
                    {subsidiary.isActive ? t('status.active') : t('status.inactive')}
                  </span>
                </td>
                <td className="px-4 py-3">
                  <div className="flex justify-end gap-2">
                    <Button variant="ghost" onClick={() => setEditing(subsidiary)}>
                      {t('actions.edit')}
                    </Button>
                    <Button
                      variant="ghost"
                      disabled={deactivate.isPending || reactivate.isPending}
                      onClick={() => void onToggleActive(subsidiary)}
                    >
                      {subsidiary.isActive ? t('actions.deactivate') : t('actions.reactivate')}
                    </Button>
                  </div>
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>

      {creating && <CreateSubsidiaryDialog onClose={() => setCreating(false)} />}
      {editing && <EditSubsidiaryDialog subsidiary={editing} onClose={() => setEditing(null)} />}
    </div>
  )
}
