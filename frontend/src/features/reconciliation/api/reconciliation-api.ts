import { apiClient } from '@/lib/api-client'
import type { ReconciliationBoard } from '../types'

/**
 * Typed API calls for the reconciliation capability. The board read is scoped by the backend to the
 * caller's role/subsidiary; the write calls surface the backend's `403`/`409`/validation errors.
 */

export async function getReconciliationBoard(): Promise<ReconciliationBoard> {
  const { data } = await apiClient.get<ReconciliationBoard>('/reconciliation')
  return data
}

export async function manualMatch(ledgerEntryId: string, bankStatementLineId: string): Promise<void> {
  await apiClient.post('/reconciliation/manual-match', { ledgerEntryId, bankStatementLineId })
}

export async function justifyEntry(ledgerEntryId: string, justificationText: string): Promise<void> {
  await apiClient.post(`/reconciliation/${ledgerEntryId}/justify`, { justificationText })
}

export async function approveEntry(ledgerEntryId: string): Promise<void> {
  await apiClient.post(`/reconciliation/${ledgerEntryId}/approve`)
}

export async function rejectEntry(ledgerEntryId: string, rejectionReason: string): Promise<void> {
  await apiClient.post(`/reconciliation/${ledgerEntryId}/reject`, { rejectionReason })
}
