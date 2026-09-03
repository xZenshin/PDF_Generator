import { defineConfig } from 'vite'
import react from '@vitejs/plugin-react'

// The API is proxied so the app only ever talks to its own origin — the same
// shape it will have once the built frontend is served behind the backend.
export default defineConfig({
  plugins: [react()],
  server: {
    port: 5173,
    proxy: {
      '/api': {
        target: process.env.VITE_API_TARGET ?? 'http://localhost:5199',
        changeOrigin: true,
      },
    },
  },
})
