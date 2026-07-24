import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import { Button } from '@/components/ui/button'
import { isManager } from '@/features/auth/roles'
import { useAuth } from '@/features/auth/use-auth'
import { cn } from '@/lib/utils'
import { useApproveEntry, useManualMatch, useReconciliationBoard } from '../hooks/use-reconciliation'
import type { PendingApprovalEntry, PendingLedgerEntry, UnmatchedLine } from '../types'
import { JustifyDialog } from './JustifyDialog'
import { RejectDialog } from './RejectDialog'

/**
 * Reconciliation board. Any authenticated user reaches it; the backend scopes the data and gates the
 * write actions. Left/right columns show pending ledger entries vs. unmatched bank statement lines of
 * the same subsidiary for manual matching (writers). Entries in PendingReconciliation can be justified
 * (writers); a Managers-only queue lists PendingApproval entries with approve/reject. Auditors see a
 * read-only view.
 */
export function ReconciliationBoardPage() {
  const { t } = useTranslation('reconciliation')
  const { user } = useAuth()
  const canWrite = user?.role !== 'Auditor'
  const canApprove = isManager(user)

  const { data, isLoading, isError } = useReconciliationBoard()
  const manualMatch = useManualMatch()
  const approve = useApproveEntry()

  const [selectedEntryId, setSelectedEntryId] = useState<string | null>(null)
  const [selectedLineId, setSelectedLineId] = useState<string | null>(null)
  const [justifying, setJustifying] = useState<PendingLedgerEntry | null>(null)
  const [rejecting, setRejecting] = useState<PendingApprovalEntry | null>(null)
  const [actionError, setActionError] = useState<string | null>(null)

  const selectedEntry = data?.pendingEntries.find((e) => e.id === selectedEntryId) ?? null
  const selectedLine = data?.unmatchedLines.find((l) => l.id === selectedLineId) ?? null
  const sameSubsidiary =
    selectedEntry !== null && selectedLine !== null && selectedEntry.subsidiaryId === selectedLine.subsidiaryId
  const canMatch = canWrite && sameSubsidiary && !manualMatch.isPending

  async function onMatch() {
    if (!selectedEntry || !selectedLine) return
    setActionError(null)
    try {
      await manualMatch.mutateAsync({ ledgerEntryId: selectedEntry.id, bankStatementLineId: selectedLine.id })
      setSelectedEntryId(null)
      setSelectedLineId(null)
    } catch {
      setActionError(t('error.actionFailed'))
    }
  }

  async function onApprove(id: string) {
    setActionError(null)
    try {
      await approve.mutateAsync(id)
    } catch {
      setActionError(t('error.actionFailed'))
    }
  }

  return (
    <div className="mx-auto max-w-6xl p-6">
      <div>
        <h1 className="text-2xl font-semibold tracking-tight">{t('title')}</h1>
        <p className="mt-1 text-sm text-muted-foreground">{t('subtitle')}</p>
      </div>

      {isLoading && <p className="mt-6 text-sm text-muted-foreground">{t('loading')}</p>}
      {isError && <p className="mt-6 text-sm text-destructive">{t('error.loadFailed')}</p>}

      {data && (
        <>
          {/* Manual matching: pending entries vs. unmatched lines. */}
          <div className="mt-6 grid gap-4 lg:grid-cols-2">
            <section aria-label={t('entries.heading')} className="rounded-lg border border-border">
              <header className="border-b border-border px-4 py-2 text-sm font-semibold">
                {t('entries.heading')} ({data.pendingEntries.length})
              </header>
              <ul className="divide-y divide-border">
                {data.pendingEntries.length === 0 && (
                  <li className="px-4 py-3 text-sm text-muted-foreground">{t('entries.empty')}</li>
                )}
                {data.pendingEntries.map((entry) => (
                  <li key={entry.id} className="flex items-start justify-between gap-3 px-4 py-3">
                    <button
                      type="button"
                      disabled={!canWrite}
                      onClick={() => setSelectedEntryId(entry.id === selectedEntryId ? null : entry.id)}
                      className={cn(
                        'flex-1 rounded-md p-2 text-left text-sm',
                        entry.id === selectedEntryId ? 'bg-accent' : 'hover:bg-muted',
                        !canWrite && 'cursor-default hover:bg-transparent',
                      )}
                    >
                      <span className="font-medium">{entry.description}</span>
                      <span className="mt-0.5 block text-xs text-muted-foreground">
                        {entry.date} · {entry.type} · {entry.amount} · {t(`status.${entry.status}`)}
                      </span>
                    </button>
                    {canWrite && entry.status === 'PendingReconciliation' && (
                      <Button variant="outline" size="sm" onClick={() => setJustifying(entry)}>
                        {t('actions.justify')}
                      </Button>
                    )}
                  </li>
                ))}
              </ul>
            </section>

            <section aria-label={t('lines.heading')} className="rounded-lg border border-border">
              <header className="border-b border-border px-4 py-2 text-sm font-semibold">
                {t('lines.heading')} ({data.unmatchedLines.length})
              </header>
              <ul className="divide-y divide-border">
                {data.unmatchedLines.length === 0 && (
                  <li className="px-4 py-3 text-sm text-muted-foreground">{t('lines.empty')}</li>
                )}
                {data.unmatchedLines.map((line: UnmatchedLine) => (
                  <li key={line.id} className="px-4 py-3">
                    <button
                      type="button"
                      disabled={!canWrite}
                      onClick={() => setSelectedLineId(line.id === selectedLineId ? null : line.id)}
                      className={cn(
                        'w-full rounded-md p-2 text-left text-sm',
                        line.id === selectedLineId ? 'bg-accent' : 'hover:bg-muted',
                        !canWrite && 'cursor-default hover:bg-transparent',
                      )}
                    >
                      <span className="font-medium">{line.description}</span>
                      <span className="mt-0.5 block text-xs text-muted-foreground">
                        {line.date} · {line.type} · {line.amount}
                        {line.documentNumber ? ` · ${line.documentNumber}` : ''}
                      </span>
                    </button>
                  </li>
                ))}
              </ul>
            </section>
          </div>

          {canWrite && (
            <div className="mt-4 flex items-center gap-3">
              <Button onClick={onMatch} disabled={!canMatch}>
                {t('actions.match')}
              </Button>
              {selectedEntry && selectedLine && !sameSubsidiary && (
                <p className="text-sm text-destructive">{t('match.subsidiaryMismatch')}</p>
              )}
            </div>
          )}

          {/* Manager-only approval queue. */}
          {canApprove && (
            <section aria-label={t('approvals.heading')} className="mt-8 rounded-lg border border-border">
              <header className="border-b border-border px-4 py-2 text-sm font-semibold">
                {t('approvals.heading')} ({data.pendingApprovals.length})
              </header>
              <ul className="divide-y divide-border">
                {data.pendingApprovals.length === 0 && (
                  <li className="px-4 py-3 text-sm text-muted-foreground">{t('approvals.empty')}</li>
                )}
                {data.pendingApprovals.map((entry) => (
                  <li key={entry.id} className="flex items-start justify-between gap-3 px-4 py-3">
                    <div className="min-w-0 text-sm">
                      <span className="font-medium">{entry.description}</span>
                      <span className="mt-0.5 block text-xs text-muted-foreground">
                        {entry.date} · {entry.type} · {entry.amount}
                      </span>
                      {entry.justificationText && (
                        <p className="mt-1 text-xs text-muted-foreground">
                          {t('approvals.justification')}: {entry.justificationText}
                        </p>
                      )}
                    </div>
                    <div className="flex shrink-0 gap-2">
                      <Button variant="outline" size="sm" onClick={() => void onApprove(entry.id)} disabled={approve.isPending}>
                        {t('actions.approve')}
                      </Button>
                      <Button variant="outline" size="sm" onClick={() => setRejecting(entry)}>
                        {t('actions.reject')}
                      </Button>
                    </div>
                  </li>
                ))}
              </ul>
            </section>
          )}

          {actionError && (
            <p role="alert" className="mt-4 text-sm text-destructive">
              {actionError}
            </p>
          )}
        </>
      )}

      {justifying && <JustifyDialog entry={justifying} onClose={() => setJustifying(null)} />}
      {rejecting && <RejectDialog entry={rejecting} onClose={() => setRejecting(null)} />}
    </div>
  )
}
