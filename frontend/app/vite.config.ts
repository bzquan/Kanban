import react from '@vitejs/plugin-react'
import { defineConfig } from 'vite'

// https://vite.dev/config/
export default defineConfig({
  plugins: [react()],
  server: {
    proxy: {
      // Dev-time API proxy; backend runs locally via `dotnet run --project Kanban.Api`.
      '/api': 'http://localhost:5000',
    },
  },
  build: {
    // SPA is published into the API's static web root; wired up for real in Task 12.
    outDir: '../Kanban.Api/wwwroot',
    emptyOutDir: true,
  },
})
