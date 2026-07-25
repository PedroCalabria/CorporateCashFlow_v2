import * as React from 'react'

import { cn } from '@/lib/utils'

/**
 * A minimal, dependency-free tabs primitive (no Radix package is installed in this project yet,
 * and a two-tab toggle doesn't warrant adding one — consistent with the hand-rolled controls used
 * elsewhere, e.g. the plain `<select>` filters in ledger-entries). Controlled via `value`/
 * `onValueChange`, so each screen owns its own active-tab state.
 */

interface TabsContextValue {
  value: string
  setValue: (value: string) => void
}

const TabsContext = React.createContext<TabsContextValue | null>(null)

function useTabsContext(): TabsContextValue {
  const ctx = React.useContext(TabsContext)
  if (!ctx) {
    throw new Error('Tabs.* components must be rendered within <Tabs>')
  }
  return ctx
}

interface TabsProps extends Omit<React.ComponentProps<'div'>, 'onChange'> {
  value: string
  onValueChange: (value: string) => void
}

function Tabs({ value, onValueChange, className, children, ...props }: TabsProps) {
  return (
    <TabsContext.Provider value={{ value, setValue: onValueChange }}>
      <div data-slot="tabs" className={cn('flex flex-col gap-4', className)} {...props}>
        {children}
      </div>
    </TabsContext.Provider>
  )
}

function TabsList({ className, ...props }: React.ComponentProps<'div'>) {
  return (
    <div
      role="tablist"
      data-slot="tabs-list"
      className={cn('inline-flex w-fit items-center gap-1 rounded-lg bg-muted p-1', className)}
      {...props}
    />
  )
}

interface TabsTriggerProps extends React.ComponentProps<'button'> {
  value: string
}

function TabsTrigger({ value, className, ...props }: TabsTriggerProps) {
  const { value: active, setValue } = useTabsContext()
  const isActive = active === value

  return (
    <button
      type="button"
      role="tab"
      aria-selected={isActive}
      data-slot="tabs-trigger"
      data-state={isActive ? 'active' : 'inactive'}
      onClick={() => setValue(value)}
      className={cn(
        'inline-flex items-center justify-center rounded-md px-3 py-1.5 text-sm font-medium transition-all',
        isActive ? 'bg-background text-foreground shadow-xs' : 'text-muted-foreground hover:text-foreground',
        className,
      )}
      {...props}
    />
  )
}

interface TabsContentProps extends React.ComponentProps<'div'> {
  value: string
}

function TabsContent({ value, className, ...props }: TabsContentProps) {
  const { value: active } = useTabsContext()

  // Hidden (not unmounted) when inactive, so each tab's own filter/pagination state survives
  // switching away and back instead of resetting on remount.
  return (
    <div
      role="tabpanel"
      data-slot="tabs-content"
      hidden={active !== value}
      className={className}
      {...props}
    />
  )
}

export { Tabs, TabsList, TabsTrigger, TabsContent }
