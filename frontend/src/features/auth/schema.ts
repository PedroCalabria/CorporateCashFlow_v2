import { z } from 'zod'

/**
 * Login form schema. Messages are i18n keys (resolved in the `auth` namespace at render
 * time) so validation text is localized. Mirrors the backend validator: valid email +
 * non-empty password. Credential correctness is decided server-side (generic error).
 */
export const loginSchema = z.object({
  email: z
    .string()
    .min(1, 'validation.emailRequired')
    .email('validation.emailInvalid'),
  password: z.string().min(1, 'validation.passwordRequired'),
})

export type LoginFormValues = z.infer<typeof loginSchema>
