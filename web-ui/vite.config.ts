import { defineConfig } from 'vite';
import react from '@vitejs/plugin-react';

export default defineConfig({
  plugins: [react()],
  server: {
    port: 5173,
    // The client's base URL is the relative path /api in dev and production alike, so there
    // is no base-URL environment variable and no CORS surface. This proxy is what makes the
    // relative path resolve while the API runs on its own port.
    proxy: {
      '/api': { target: 'http://localhost:5231', changeOrigin: false },
    },
  },
});
