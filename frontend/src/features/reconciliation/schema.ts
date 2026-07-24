import { z } from 'zod'

/**
 * Form schemas for the divergence workflow. Messages are i18n keys (resolved in the `reconciliation`
 * namespace). The backend re-enforces both mandatory reasons (§1.2 transitions 5 and 7).
 */

export const justifySchema = z.object({
  justificationText: z.string().min(1, 'validation.justificationRequired').max(1000, 'validation.reasonTooLong'),
})

export type JustifyFormValues = z.infer<typeof justifySchema>

export const rejectSchema = z.object({
  rejectionReason: z.string().min(1, 'validation.reasonRequired').max(1000, 'validation.reasonTooLong'),
})

export type RejectFormValues = z.infer<typeof rejectSchema>
