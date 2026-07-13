import { useMemo, useState } from 'react'
import { useTranslation } from 'react-i18next'
import { Button } from '@/components/ui/button'
import { isGlobalManager } from '@/features/auth/roles'
import { useAuth } from '@/features/auth/use-auth'
import { useCategories, useLedgerEntries, useSubsidiaryOptions } from '../hooks/use-ledger-entries'
import type { LedgerEntry, LedgerEntryStatus } from '../types'
import { controlClass } from './control-styles'
import { CreateLedgerEntryDialog } from './CreateLedgerEntryDialog'
import { DeleteLedgerEntryDialog } from './DeleteLedgerEntryDialog'
import { EditLedgerEntryDialog } from './EditLedgerEntryDialog'
import { ImportLedgerEntriesDialog } from './ImportLedgerEntriesDialog'

const PAGE_SIZE = 20
const STATUSES: LedgerEntryStatus[] = ['Open', 'PendingReconciliation', 'PendingApproval', 'Reconciled', 'Deleted']

/**
 * Ledger-entries screen. Any authenticated user reaches it; the backend scopes the list and the
 * write actions. Actions are role-aware: create/import for writers, edit only on Open entries,
 * delete only for Managers. Filters (subsidiary/category/date/status) and pagination drive the query.
 */
export function LedgerEntriesListPage() {
  const { t } = useTranslation('ledger-entries')
  const { user } = useAuth()
  const global = isGlobalManager(user)
  const canWrite = user?.role !== 'Auditor'
  const canDelete = user?.role === 'Manager'

  const [page, setPage] = useState(1)
  const [subsidiaryId, setSubsidiaryId] = useState('')
  const [categoryId, setCategoryId] = useState('')
  const [status, setStatus] = useState('')
  const [dateFrom, setDateFrom] = useState('')
  const [dateTo, setDateTo] = useState('')

  const [creating, setCreating] = useState(false)
  const [importing, setImporting] = useState(false)
  const [editing, setEditing] = useState<LedgerEntry | null>(null)
  const [deleting, setDeleting] = useState<LedgerEntry | null>(null)

  const categoriesQuery = useCategories()
  const subsidiariesQuery = useSubsidiaryOptions(global)

  const query = {
    page,
    pageSize: PAGE_SIZE,
    subsidiaryId: subsidiaryId || undefined,
    categoryId: categoryId || undefined,
    status: status || undefined,
    dateFrom: dateFrom || undefined,
    dateTo: dateTo || undefined,
  }
  const { data, isLoading, isError } = useLedgerEntries(query)

  const categoryNames = useMemo(() => {
    const map = new Map<string, string>()
    for (const c of categoriesQuery.data ?? []) map.set(c.id, c.name)
    return map
  }, [categoriesQuery.data])

  function resetToFirstPage<T>(setter: (v: T) => void) {
    return (value: T) => {
      setter(value)
      setPage(1)
    }
  }

  const totalPages = data?.totalPages ?? 0

  return (
    <div className="mx-auto max-w-6xl p-6">
      <div className="flex items-center justify-between">
        <div>
          <h1 className="text-2xl font-semibold tracking-tight">{t('title')}</h1>
          <p className="mt-1 text-sm text-muted-foreground">{t('subtitle')}</p>
        </div>
        {canWrite && (
          <div className="flex gap-2">
            <Button variant="ghost" onClick={() => setImporting(true)}>
              {t('actions.import')}
            </Button>
            <Button onClick={() => setCreating(true)}>{t('actions.new')}</Button>
          </div>
        )}
      </div>

      {/* Filters */}
      <div className="mt-6 grid gap-3 sm:grid-cols-2 lg:grid-cols-5">
        {global && (
          <select
            className={controlClass}
            value={subsidiaryId}
            onChange={(e) => resetToFirstPage(setSubsidiaryId)(e.target.value)}
            aria-label={t('fields.subsidiary')}
          >
            <option value="">{t('filter.allSubsidiaries')}</option>
            {subsidiariesQuery.data?.map((s) => (
              <option key={s.id} value={s.id}>
                {s.name}
              </option>
            ))}
          </select>
        )}
        <select
          className={controlClass}
          value={categoryId}
          onChange={(e) => resetToFirstPage(setCategoryId)(e.target.value)}
          aria-label={t('fields.category')}
        >
          <option value="">{t('filter.allCategories')}</option>
          {categoriesQuery.data?.map((c) => (
            <option key={c.id} value={c.id}>
              {c.name}
            </option>
          ))}
        </select>
        <select
          className={controlClass}
          value={status}
          onChange={(e) => resetToFirstPage(setStatus)(e.target.value)}
          aria-label={t('fields.status')}
        >
          <option value="">{t('filter.allStatuses')}</option>
          {STATUSES.map((s) => (
            <option key={s} value={s}>
              {t(`status.${s}`)}
            </option>
          ))}
        </select>
        <input
          type="date"
          className={controlClass}
          value={dateFrom}
          onChange={(e) => resetToFirstPage(setDateFrom)(e.target.value)}
          aria-label={t('filter.dateFrom')}
        />
        <input
          type="date"
          className={controlClass}
          value={dateTo}
          onChange={(e) => resetToFirstPage(setDateTo)(e.target.value)}
          aria-label={t('filter.dateTo')}
        />
      </div>

      <div className="mt-4 overflow-x-auto rounded-lg border border-border">
        <table className="w-full text-sm">
          <thead className="bg-muted/50 text-left text-xs uppercase tracking-wide text-muted-foreground">
            <tr>
              <th className="px-4 py-3 font-semibold">{t('fields.date')}</th>
              <th className="px-4 py-3 font-semibold">{t('fields.description')}</th>
              <th className="px-4 py-3 font-semibold">{t('fields.category')}</th>
              <th className="px-4 py-3 font-semibold">{t('fields.type')}</th>
              <th className="px-4 py-3 text-right font-semibold">{t('fields.amount')}</th>
              <th className="px-4 py-3 font-semibold">{t('fields.status')}</th>
              <th className="px-4 py-3 text-right font-semibold">{t('fields.actions')}</th>
            </tr>
          </thead>
          <tbody className="divide-y divide-border">
            {isLoading && (
              <tr>
                <td colSpan={7} className="px-4 py-6 text-center text-muted-foreground">
                  {t('loading')}
                </td>
              </tr>
            )}
            {isError && (
              <tr>
                <td colSpan={7} className="px-4 py-6 text-center text-destructive">
                  {t('error.loadFailed')}
                </td>
              </tr>
            )}
            {!isLoading && !isError && data?.items.length === 0 && (
              <tr>
                <td colSpan={7} className="px-4 py-6 text-center text-muted-foreground">
                  {t('empty')}
                </td>
              </tr>
            )}
            {data?.items.map((entry) => (
              <tr key={entry.id} className={entry.status === 'Deleted' ? 'text-muted-foreground line-through' : ''}>
                <td className="px-4 py-3">{entry.date}</td>
                <td className="px-4 py-3">{entry.description}</td>
                <td className="px-4 py-3">{categoryNames.get(entry.categoryId) ?? '—'}</td>
                <td className="px-4 py-3">{t(`entryType.${entry.type}`)}</td>
                <td className="px-4 py-3 text-right tabular-nums">{entry.amount.toFixed(2)}</td>
                <td className="px-4 py-3">{t(`status.${entry.status}`)}</td>
                <td className="px-4 py-3">
                  <div className="flex justify-end gap-2">
                    {canWrite && entry.status === 'Open' && (
                      <Button variant="ghost" onClick={() => setEditing(entry)}>
                        {t('actions.edit')}
                      </Button>
                    )}
                    {canDelete && entry.status !== 'Deleted' && (
                      <Button variant="ghost" onClick={() => setDeleting(entry)}>
                        {t('actions.delete')}
                      </Button>
                    )}
                  </div>
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>

      {/* Pagination */}
      <div className="mt-4 flex items-center justify-between text-sm text-muted-foreground">
        <span>{t('pagination.total', { count: data?.totalCount ?? 0 })}</span>
        <div className="flex items-center gap-2">
          <Button variant="ghost" disabled={page <= 1} onClick={() => setPage((p) => Math.max(1, p - 1))}>
            {t('pagination.prev')}
          </Button>
          <span>{t('pagination.page', { page, totalPages: Math.max(1, totalPages) })}</span>
          <Button variant="ghost" disabled={totalPages === 0 || page >= totalPages} onClick={() => setPage((p) => p + 1)}>
            {t('pagination.next')}
          </Button>
        </div>
      </div>

      {creating && <CreateLedgerEntryDialog onClose={() => setCreating(false)} />}
      {importing && <ImportLedgerEntriesDialog onClose={() => setImporting(false)} />}
      {editing && <EditLedgerEntryDialog entry={editing} onClose={() => setEditing(null)} />}
      {deleting && <DeleteLedgerEntryDialog entry={deleting} onClose={() => setDeleting(null)} />}
    </div>
  )
}
