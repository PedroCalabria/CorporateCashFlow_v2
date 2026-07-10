import { StrictMode } from 'react'
import { createRoot } from 'react-dom/client'
import { I18nextProvider } from 'react-i18next'
import { RouterProvider } from 'react-router-dom'
import './index.css'
import i18n from './i18n/config'
import { ThemeProvider } from './app/ThemeProvider'
import { AuthProvider } from './features/auth/AuthProvider'
import { router } from './app/router'

// Provider composition (design.md §D1): ThemeProvider → i18n → AuthProvider → Router.
// AuthProvider wraps the router so the boot-time silent refresh and session state are
// available to the route guard and every screen.
createRoot(document.getElementById('root')!).render(
  <StrictMode>
    <ThemeProvider>
      <I18nextProvider i18n={i18n}>
        <AuthProvider>
          <RouterProvider router={router} />
        </AuthProvider>
      </I18nextProvider>
    </ThemeProvider>
  </StrictMode>,
)
