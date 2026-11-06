import { defineConfig } from 'vite'
import react from '@vitejs/plugin-react'
import { resolve } from 'node:path'

export default defineConfig({
  plugins: [react()],
  base: '/',
  build: {
    outDir: '../AtendenteWindows/wwwroot',
    emptyOutDir: true,
    rollupOptions: { input: resolve(import.meta.dirname, 'atendente-desktop.html') },
  },
})
