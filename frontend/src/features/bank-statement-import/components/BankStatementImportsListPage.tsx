import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import { Button } from '@/components/ui/button'
import { isGlobalManager } from '@/features/auth/roles'
import { useAuth } from '@/features/auth/use-auth'
import { useBankStatementBatches, useSubsidiaryOptions } from '../hooks/use-bank-statement-imports'
import type { BankStatementBatch, BankStatementBatchStatus } from '../types'
import { controlClass } from './control-styles'
import { ImportBankStatementDialog } from './ImportBankStatementDialog'
import { RejectBatchDialog } from './RejectBatchDialog'

const PAGE_SIZE = 20
const STATUSES: BankStatementBatchStatus[] = ['Processed', 'ProcessedWithErrors', 'Rejected']

/**
 * Bank-statement imports screen. Any authenticated user reaches it; the backend scopes the list and
 * the write actions. Actions are role-aware: import for writers (Editor/Manager), reject only for
 * Managers on a not-yet-rejected batch. Filters (subsidiary/status/date) and pagination drive the query.
 */
export function BankStatementImportsListPage() {
  const { t } = useTranslation('bank-statement-import')
  const { user } = useAuth()
  const global = isGlobalManager(user)
  const canWrite = user?.role !== 'Auditor'
  const canReject = user?.role === 'Manager'

  const [page, setPage] = useState(1)
  const [subsidiaryId, setSubsidiaryId] = useState('')
  const [status, setStatus] = useState('')
  const [dateFrom, setDateFrom] = useState('')
  const [dateTo, setDateTo] = useState('')

  const [importing, setImporting] = useState(false)
  const [rejecting, setRejecting] = useState<BankStatementBatch | null>(null)

  const subsidiariesQuery = useSubsidiaryOptions(global)

  const query = {
    page,
    pageSize: PAGE_SIZE,
    subsidiaryId: subsidiaryId || undefined,
    status: status || undefined,
    dateFrom: dateFrom || undefined,
    dateTo: dateTo || undefined,
  }
  const { data, isLoading, isError } = useBankStatementBatches(query)

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
        {canWrite && <Button onClick={() => setImporting(true)}>{t('actions.import')}</Button>}
      </div>

      {/* Filters */}
      <div className="mt-6 grid gap-3 sm:grid-cols-2 lg:grid-cols-4">
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
              <th className="px-4 py-3 font-semibold">{t('fields.importedAt')}</th>
              <th className="px-4 py-3 font-semibold">{t('fields.fileName')}</th>
              <th className="px-4 py-3 text-right font-semibold">{t('fields.lines')}</th>
              <th className="px-4 py-3 font-semibold">{t('fields.status')}</th>
              <th className="px-4 py-3 text-right font-semibold">{t('fields.actions')}</th>
            </tr>
          </thead>
          <tbody className="divide-y divide-border">
            {isLoading && (
              <tr>
                <td colSpan={5} className="px-4 py-6 text-center text-muted-foreground">
                  {t('loading')}
                </td>
              </tr>
            )}
            {isError && (
              <tr>
                <td colSpan={5} className="px-4 py-6 text-center text-destructive">
                  {t('error.loadFailed')}
                </td>
              </tr>
            )}
            {!isLoading && !isError && data?.items.length === 0 && (
              <tr>
                <td colSpan={5} className="px-4 py-6 text-center text-muted-foreground">
                  {t('empty')}
                </td>
              </tr>
            )}
            {data?.items.map((batch) => (
              <tr key={batch.id} className={batch.status === 'Rejected' ? 'text-muted-foreground' : ''}>
                <td className="px-4 py-3">{new Date(batch.importedAt).toLocaleString()}</td>
                <td className="px-4 py-3">{batch.fileName}</td>
                <td className="px-4 py-3 text-right tabular-nums">{batch.lineCount}</td>
                <td className="px-4 py-3">{t(`status.${batch.status}`)}</td>
                <td className="px-4 py-3">
                  <div className="flex justify-end gap-2">
                    {canReject && batch.status !== 'Rejected' && (
                      <Button variant="ghost" onClick={() => setRejecting(batch)}>
                        {t('actions.reject')}
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

      {importing && <ImportBankStatementDialog onClose={() => setImporting(false)} />}
      {rejecting && <RejectBatchDialog batch={rejecting} onClose={() => setRejecting(null)} />}
    </div>
  )
}
