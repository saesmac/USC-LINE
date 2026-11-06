import React from 'react'
import { createRoot } from 'react-dom/client'
import App from './App.jsx'
import AtendenteApp from './AtendenteApp.jsx'
import './index.css'

const page = window.location.pathname.replace(/\/$/, '')
createRoot(document.getElementById('root')).render(
  <React.StrictMode>
    {page === '/atendente' ? <AtendenteApp /> : <App />}
  </React.StrictMode>
)
