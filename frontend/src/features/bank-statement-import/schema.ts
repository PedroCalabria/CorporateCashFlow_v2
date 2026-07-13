import { z } from 'zod'

/**
 * Form schema for the reject-batch modal. The message is an i18n key (resolved in the
 * `bank-statement-import` namespace). The backend re-enforces the mandatory reason (§2.2 transition 2).
 */
export const rejectBatchSchema = z.object({
  rejectionReason: z.string().min(1, 'validation.reasonRequired').max(1000, 'validation.reasonTooLong'),
})

export type RejectBatchFormValues = z.infer<typeof rejectBatchSchema>
