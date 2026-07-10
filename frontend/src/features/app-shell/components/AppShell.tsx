import { Outlet } from 'react-router-dom'
import { SideMenu } from './SideMenu'

/**
 * The persistent application layout: a side menu plus a content area that
 * renders the active route via <Outlet />. Every screen renders inside this
 * shell — the shell itself is declared once at the root route, never duplicated
 * per screen. See spec: "Persistent application layout".
 */
export function AppShell() {
  return (
    <div className="flex h-screen w-full overflow-hidden bg-background text-foreground">
      <SideMenu />
      <main className="flex-1 overflow-y-auto">
        <Outlet />
      </main>
    </div>
  )
}
