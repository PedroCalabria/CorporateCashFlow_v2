import { cn } from '@/lib/utils'

/** Input styling shared by the subsidiary forms (matches the login form). Exported so a custom control (e.g. a masked input) can look identical to the built-in ones. */
export function subsidiaryControlClass(hasError: boolean): string {
  return cn(
    'flex h-9 w-full rounded-md border border-input bg-background px-3 py-1 text-sm shadow-xs outline-none focus-visible:border-ring focus-visible:ring-[3px] focus-visible:ring-ring/50',
    hasError && 'border-destructive',
  )
}
