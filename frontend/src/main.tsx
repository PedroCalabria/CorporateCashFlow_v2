import { StrictMode } from 'react'
import { createRoot } from 'react-dom/client'
import { QueryClientProvider } from '@tanstack/react-query'
import { I18nextProvider } from 'react-i18next'
import { RouterProvider } from 'react-router-dom'
import './index.css'
import i18n from './i18n/config'
import { ThemeProvider } from './app/ThemeProvider'
import { AuthProvider } from './features/auth/AuthProvider'
import { queryClient } from './lib/query-client'
import { router } from './app/router'

// Provider composition: ThemeProvider → i18n → QueryClient → AuthProvider → Router.
// QueryClientProvider wraps the router so every screen's server-state hooks share one client;
// AuthProvider stays inside it and above the router so the boot-time silent refresh and session
// state are available to the route guards and every screen.
createRoot(document.getElementById('root')!).render(
  <StrictMode>
    <ThemeProvider>
      <I18nextProvider i18n={i18n}>
        <QueryClientProvider client={queryClient}>
          <AuthProvider>
            <RouterProvider router={router} />
          </AuthProvider>
        </QueryClientProvider>
      </I18nextProvider>
    </ThemeProvider>
  </StrictMode>,
)
