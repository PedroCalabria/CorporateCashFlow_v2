import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import { Button } from '@/components/ui/button'
import { isGlobalManager } from '@/features/auth/roles'
import { useAuth } from '@/features/auth/use-auth'
import { useImportLedgerEntries, useSubsidiaryOptions } from '../hooks/use-ledger-entries'
import type { ImportResult } from '../types'
import { controlClass } from './control-styles'
import { Modal } from './Modal'

/**
 * Spreadsheet (CSV) import. A Global Manager picks the target subsidiary; others are locked to their
 * own. After upload it shows how many entries were created and lists every rejected row with its
 * reason (never silently dropped).
 */
export function ImportLedgerEntriesDialog({ onClose }: { onClose: () => void }) {
  const { t } = useTranslation('ledger-entries')
  const { user } = useAuth()
  const global = isGlobalManager(user)
  const importEntries = useImportLedgerEntries()
  const subsidiariesQuery = useSubsidiaryOptions(global)

  const [subsidiaryId, setSubsidiaryId] = useState(global ? '' : (user?.subsidiaryId ?? ''))
  const [file, setFile] = useState<File | null>(null)
  const [result, setResult] = useState<ImportResult | null>(null)
  const [formError, setFormError] = useState<string | null>(null)

  async function onSubmit() {
    setFormError(null)
    if (!subsidiaryId || !file) {
      setFormError(t('import.missingInput'))
      return
    }
    try {
      setResult(await importEntries.mutateAsync({ subsidiaryId, file }))
    } catch {
      setFormError(t('error.actionFailed'))
    }
  }

  return (
    <Modal title={t('import.title')} onClose={onClose}>
      <p className="text-sm text-muted-foreground">{t('import.help')}</p>

      <div className="mt-4 space-y-4">
        {global && (
          <div className="space-y-1.5">
            <label htmlFor="import-subsidiary" className="text-sm font-medium">
              {t('fields.subsidiary')}
            </label>
            <select
              id="import-subsidiary"
              className={controlClass}
              value={subsidiaryId}
              onChange={(e) => setSubsidiaryId(e.target.value)}
            >
              <option value="">{t('placeholder.selectSubsidiary')}</option>
              {subsidiariesQuery.data?.map((s) => (
                <option key={s.id} value={s.id}>
                  {s.name} ({s.code})
                </option>
              ))}
            </select>
          </div>
        )}

        <div className="space-y-1.5">
          <label htmlFor="import-file" className="text-sm font-medium">
            {t('import.file')}
          </label>
          <input
            id="import-file"
            type="file"
            accept=".csv,.xlsx,text/csv,application/vnd.openxmlformats-officedocument.spreadsheetml.sheet"
            className="block w-full text-sm"
            onChange={(e) => setFile(e.target.files?.[0] ?? null)}
          />
        </div>

        {formError && (
          <p role="alert" className="text-sm text-destructive">
            {formError}
          </p>
        )}

        {result && (
          <div className="rounded-md border border-border p-3 text-sm">
            <p className="font-medium">{t('import.created', { count: result.createdCount })}</p>
            {result.errors.length > 0 && (
              <div className="mt-2">
                <p className="text-muted-foreground">{t('import.rejected', { count: result.errors.length })}</p>
                <ul className="mt-1 max-h-40 space-y-1 overflow-y-auto">
                  {result.errors.map((err) => (
                    <li key={err.rowNumber} className="text-destructive">
                      {t('import.rowLabel', { row: err.rowNumber })}: {err.message}
                    </li>
                  ))}
                </ul>
              </div>
            )}
          </div>
        )}
      </div>

      <div className="mt-6 flex justify-end gap-2">
        <Button type="button" variant="ghost" onClick={onClose}>
          {result ? t('actions.done') : t('actions.cancel')}
        </Button>
        {!result && (
          <Button type="button" disabled={importEntries.isPending} onClick={() => void onSubmit()}>
            {importEntries.isPending ? t('actions.saving') : t('import.upload')}
          </Button>
        )}
      </div>
    </Modal>
  )
}
