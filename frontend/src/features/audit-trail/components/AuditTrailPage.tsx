import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import { Button } from '@/components/ui/button'
import { Tabs, TabsContent, TabsList, TabsTrigger } from '@/components/ui/tabs'
import { useAccessLog } from '../hooks/use-access-log'
import { useAuditLog } from '../hooks/use-audit-log'
import type { AccessLogEventType, AuditAction } from '../types'
import { controlClass } from './control-styles'

const PAGE_SIZE = 20
const AUDIT_ACTIONS: AuditAction[] = [
  'Created',
  'Updated',
  'JustificationSubmitted',
  'Approved',
  'Rejected',
  'Reverted',
  'Deleted',
]
const ACCESS_EVENT_TYPES: AccessLogEventType[] = ['LoginSuccess', 'LoginFailed', 'AccessDenied']

/**
 * Audit trail screen (Manager/Auditor only — gated by <RequireManagerOrAuditor>). Two independent
 * tabs, each with its own filter bar and pagination: "Ledger Activity" over `GET /api/audit-log`
 * and "Access Activity" over `GET /api/access-log`. The backend is the source of truth for scope
 * (own subsidiary or global); this screen renders whatever it returns.
 */
export function AuditTrailPage() {
  const { t } = useTranslation('audit-trail')
  const [tab, setTab] = useState('ledger')

  return (
    <div className="mx-auto max-w-6xl p-6">
      <div>
        <h1 className="text-2xl font-semibold tracking-tight">{t('title')}</h1>
        <p className="mt-1 text-sm text-muted-foreground">{t('subtitle')}</p>
      </div>

      <Tabs value={tab} onValueChange={setTab} className="mt-6">
        <TabsList>
          <TabsTrigger value="ledger">{t('tabs.ledgerActivity')}</TabsTrigger>
          <TabsTrigger value="access">{t('tabs.accessActivity')}</TabsTrigger>
        </TabsList>
        <TabsContent value="ledger">
          <LedgerActivityTab />
        </TabsContent>
        <TabsContent value="access">
          <AccessActivityTab />
        </TabsContent>
      </Tabs>
    </div>
  )
}

function LedgerActivityTab() {
  const { t } = useTranslation('audit-trail')
  const [page, setPage] = useState(1)
  const [entityType, setEntityType] = useState('')
  const [action, setAction] = useState('')
  const [dateFrom, setDateFrom] = useState('')
  const [dateTo, setDateTo] = useState('')

  function resetToFirstPage<T>(setter: (v: T) => void) {
    return (value: T) => {
      setter(value)
      setPage(1)
    }
  }

  const query = {
    page,
    pageSize: PAGE_SIZE,
    entityType: entityType || undefined,
    action: action || undefined,
    dateFrom: dateFrom || undefined,
    dateTo: dateTo || undefined,
  }
  const { data, isLoading, isError } = useAuditLog(query)

  return (
    <div>
      <div className="grid gap-3 sm:grid-cols-2 lg:grid-cols-4">
        <select
          className={controlClass}
          value={entityType}
          onChange={(e) => resetToFirstPage(setEntityType)(e.target.value)}
          aria-label={t('ledger.fields.entityType')}
        >
          <option value="">{t('ledger.filter.allEntityTypes')}</option>
          <option value="LedgerEntry">LedgerEntry</option>
          <option value="BankStatementImportBatch">BankStatementImportBatch</option>
        </select>
        <select
          className={controlClass}
          value={action}
          onChange={(e) => resetToFirstPage(setAction)(e.target.value)}
          aria-label={t('ledger.fields.action')}
        >
          <option value="">{t('ledger.filter.allActions')}</option>
          {AUDIT_ACTIONS.map((a) => (
            <option key={a} value={a}>
              {t(`ledger.action.${a}`)}
            </option>
          ))}
        </select>
        <input
          type="date"
          className={controlClass}
          value={dateFrom}
          onChange={(e) => resetToFirstPage(setDateFrom)(e.target.value)}
          aria-label={t('ledger.filter.dateFrom')}
        />
        <input
          type="date"
          className={controlClass}
          value={dateTo}
          onChange={(e) => resetToFirstPage(setDateTo)(e.target.value)}
          aria-label={t('ledger.filter.dateTo')}
        />
      </div>

      <div className="mt-4 overflow-x-auto rounded-lg border border-border">
        <table className="w-full text-sm">
          <thead className="bg-muted/50 text-left text-xs uppercase tracking-wide text-muted-foreground">
            <tr>
              <th className="px-4 py-3 font-semibold">{t('ledger.fields.performedAt')}</th>
              <th className="px-4 py-3 font-semibold">{t('ledger.fields.entityType')}</th>
              <th className="px-4 py-3 font-semibold">{t('ledger.fields.action')}</th>
              <th className="px-4 py-3 font-semibold">{t('ledger.fields.performedBy')}</th>
            </tr>
          </thead>
          <tbody className="divide-y divide-border">
            {isLoading && (
              <tr>
                <td colSpan={4} className="px-4 py-6 text-center text-muted-foreground">
                  {t('loading')}
                </td>
              </tr>
            )}
            {isError && (
              <tr>
                <td colSpan={4} className="px-4 py-6 text-center text-destructive">
                  {t('error.loadFailed')}
                </td>
              </tr>
            )}
            {!isLoading && !isError && data?.items.length === 0 && (
              <tr>
                <td colSpan={4} className="px-4 py-6 text-center text-muted-foreground">
                  {t('empty')}
                </td>
              </tr>
            )}
            {data?.items.map((row) => (
              <tr key={row.id}>
                <td className="px-4 py-3">{new Date(row.performedAt).toLocaleString()}</td>
                <td className="px-4 py-3">{row.entityType}</td>
                <td className="px-4 py-3">{t(`ledger.action.${row.action}`)}</td>
                <td className="px-4 py-3">{row.performedByName}</td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>

      <PaginationBar
        page={page}
        totalPages={data?.totalPages ?? 0}
        totalCount={data?.totalCount ?? 0}
        onPageChange={setPage}
      />
    </div>
  )
}

function AccessActivityTab() {
  const { t } = useTranslation('audit-trail')
  const [page, setPage] = useState(1)
  const [eventType, setEventType] = useState('')
  const [userId, setUserId] = useState('')
  const [dateFrom, setDateFrom] = useState('')
  const [dateTo, setDateTo] = useState('')

  function resetToFirstPage<T>(setter: (v: T) => void) {
    return (value: T) => {
      setter(value)
      setPage(1)
    }
  }

  const query = {
    page,
    pageSize: PAGE_SIZE,
    eventType: eventType || undefined,
    userId: userId || undefined,
    dateFrom: dateFrom || undefined,
    dateTo: dateTo || undefined,
  }
  const { data, isLoading, isError } = useAccessLog(query)

  return (
    <div>
      <div className="grid gap-3 sm:grid-cols-2 lg:grid-cols-4">
        <select
          className={controlClass}
          value={eventType}
          onChange={(e) => resetToFirstPage(setEventType)(e.target.value)}
          aria-label={t('access.fields.eventType')}
        >
          <option value="">{t('access.filter.allEventTypes')}</option>
          {ACCESS_EVENT_TYPES.map((e) => (
            <option key={e} value={e}>
              {t(`access.eventType.${e}`)}
            </option>
          ))}
        </select>
        <input
          className={controlClass}
          value={userId}
          onChange={(e) => resetToFirstPage(setUserId)(e.target.value)}
          placeholder={t('access.filter.userIdPlaceholder')}
          aria-label={t('access.fields.userId')}
        />
        <input
          type="date"
          className={controlClass}
          value={dateFrom}
          onChange={(e) => resetToFirstPage(setDateFrom)(e.target.value)}
          aria-label={t('access.filter.dateFrom')}
        />
        <input
          type="date"
          className={controlClass}
          value={dateTo}
          onChange={(e) => resetToFirstPage(setDateTo)(e.target.value)}
          aria-label={t('access.filter.dateTo')}
        />
      </div>

      <div className="mt-4 overflow-x-auto rounded-lg border border-border">
        <table className="w-full text-sm">
          <thead className="bg-muted/50 text-left text-xs uppercase tracking-wide text-muted-foreground">
            <tr>
              <th className="px-4 py-3 font-semibold">{t('access.fields.timestamp')}</th>
              <th className="px-4 py-3 font-semibold">{t('access.fields.eventType')}</th>
              <th className="px-4 py-3 font-semibold">{t('access.fields.user')}</th>
              <th className="px-4 py-3 font-semibold">{t('access.fields.ipAddress')}</th>
            </tr>
          </thead>
          <tbody className="divide-y divide-border">
            {isLoading && (
              <tr>
                <td colSpan={4} className="px-4 py-6 text-center text-muted-foreground">
                  {t('loading')}
                </td>
              </tr>
            )}
            {isError && (
              <tr>
                <td colSpan={4} className="px-4 py-6 text-center text-destructive">
                  {t('error.loadFailed')}
                </td>
              </tr>
            )}
            {!isLoading && !isError && data?.items.length === 0 && (
              <tr>
                <td colSpan={4} className="px-4 py-6 text-center text-muted-foreground">
                  {t('empty')}
                </td>
              </tr>
            )}
            {data?.items.map((row) => (
              <tr key={row.id}>
                <td className="px-4 py-3">{new Date(row.timestamp).toLocaleString()}</td>
                <td className="px-4 py-3">{t(`access.eventType.${row.eventType}`)}</td>
                <td className="px-4 py-3">{row.userName ?? t('access.unknownUser')}</td>
                <td className="px-4 py-3">{row.ipAddress ?? '—'}</td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>

      <PaginationBar
        page={page}
        totalPages={data?.totalPages ?? 0}
        totalCount={data?.totalCount ?? 0}
        onPageChange={setPage}
      />
    </div>
  )
}

function PaginationBar({
  page,
  totalPages,
  totalCount,
  onPageChange,
}: {
  page: number
  totalPages: number
  totalCount: number
  onPageChange: (page: number) => void
}) {
  const { t } = useTranslation('audit-trail')

  return (
    <div className="mt-4 flex items-center justify-between text-sm text-muted-foreground">
      <span>{t('pagination.total', { count: totalCount })}</span>
      <div className="flex items-center gap-2">
        <Button variant="ghost" disabled={page <= 1} onClick={() => onPageChange(Math.max(1, page - 1))}>
          {t('pagination.prev')}
        </Button>
        <span>{t('pagination.page', { page, totalPages: Math.max(1, totalPages) })}</span>
        <Button
          variant="ghost"
          disabled={totalPages === 0 || page >= totalPages}
          onClick={() => onPageChange(page + 1)}
        >
          {t('pagination.next')}
        </Button>
      </div>
    </div>
  )
}
