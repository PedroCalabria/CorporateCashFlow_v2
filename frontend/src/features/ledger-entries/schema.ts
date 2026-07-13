import { z } from 'zod'

/**
 * Form schemas for the ledger-entries screens. Messages are i18n keys (resolved in the
 * `ledger-entries` namespace). The create form additionally carries a subsidiary (a Global Manager
 * picks it; an Editor/subsidiary-Manager is locked to their own); the edit form is fields-only.
 */
const entryFields = {
  categoryId: z.string().min(1, 'validation.categoryRequired'),
  amount: z.coerce.number({ invalid_type_error: 'validation.amountInvalid' }).positive('validation.amountPositive'),
  date: z.string().min(1, 'validation.dateRequired'),
  description: z.string().min(1, 'validation.descriptionRequired').max(500, 'validation.descriptionTooLong'),
}

export const createLedgerEntrySchema = z.object({
  subsidiaryId: z.string().min(1, 'validation.subsidiaryRequired'),
  ...entryFields,
})

export const updateLedgerEntrySchema = z.object(entryFields)

export const deleteLedgerEntrySchema = z.object({
  deletionReason: z.string().min(1, 'validation.reasonRequired').max(500, 'validation.reasonTooLong'),
})

export type CreateLedgerEntryFormValues = z.infer<typeof createLedgerEntrySchema>
export type UpdateLedgerEntryFormValues = z.infer<typeof updateLedgerEntrySchema>
export type DeleteLedgerEntryFormValues = z.infer<typeof deleteLedgerEntrySchema>
