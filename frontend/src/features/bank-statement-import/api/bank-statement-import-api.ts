import { apiClient } from '@/lib/api-client'
import type { BankStatementBatch, BankStatementBatchQuery, ImportResult, PagedResult } from '../types'

/**
 * Typed API calls for the bank-statement-import capability. Reads are scoped by the backend to the
 * caller's role; write calls surface the backend's `403`/`409`/validation errors to the UI.
 */

export async function listBankStatementBatches(query: BankStatementBatchQuery): Promise<PagedResult<BankStatementBatch>> {
  const params: Record<string, string | number> = { page: query.page, pageSize: query.pageSize }
  if (query.subsidiaryId) params.subsidiaryId = query.subsidiaryId
  if (query.status) params.status = query.status
  if (query.dateFrom) params.dateFrom = query.dateFrom
  if (query.dateTo) params.dateTo = query.dateTo

  const { data } = await apiClient.get<PagedResult<BankStatementBatch>>('/bank-statement-imports', { params })
  return data
}

export async function importBankStatement(subsidiaryId: string, file: File): Promise<ImportResult> {
  const form = new FormData()
  form.append('subsidiaryId', subsidiaryId)
  form.append('file', file)
  const { data } = await apiClient.post<ImportResult>('/bank-statement-imports', form)
  return data
}

export async function rejectBatch(id: string, rejectionReason: string): Promise<void> {
  await apiClient.patch(`/bank-statement-imports/${id}/reject`, { rejectionReason })
}
