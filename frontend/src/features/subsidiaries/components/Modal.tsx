import type { ReactNode } from 'react'

/**
 * Minimal accessible modal used by the subsidiaries dialogs. Kept feature-local (not a shared
 * shadcn primitive) to avoid pulling a dialog dependency in for a single screen; if more
 * capabilities need modals, promote this to `components/ui`.
 */
export function Modal({
  title,
  onClose,
  children,
}: {
  title: string
  onClose: () => void
  children: ReactNode
}) {
  return (
    <div
      className="fixed inset-0 z-50 flex items-center justify-center bg-black/50 p-4"
      role="dialog"
      aria-modal="true"
      aria-label={title}
      onClick={onClose}
    >
      <div
        className="w-full max-w-md rounded-lg border border-border bg-card p-6 shadow-lg"
        onClick={(event) => event.stopPropagation()}
      >
        <h2 className="text-lg font-semibold tracking-tight">{title}</h2>
        <div className="mt-4">{children}</div>
      </div>
    </div>
  )
}
