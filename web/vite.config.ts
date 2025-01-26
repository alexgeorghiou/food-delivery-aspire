import { defineConfig } from 'vite';
import react from '@vitejs/plugin-react';

// When run under the Aspire AppHost these are injected from the service endpoints.
// The fallbacks match each project's launchSettings.json for running services by hand.
const restaurantsApi = process.env.RESTAURANTS_API_URL ?? 'http://localhost:5101';
const ordersApi = process.env.ORDERS_API_URL ?? 'http://localhost:5102';
const dispatchApi = process.env.DISPATCH_API_URL ?? 'http://localhost:5103';

const stripApiPrefix = (path: string) => path.replace(/^\/api/, '');

export default defineConfig({
  plugins: [react()],
  server: {
    port: Number(process.env.PORT) || 5173,
    strictPort: Boolean(process.env.PORT),
    proxy: {
      '/api/restaurants': { target: restaurantsApi, changeOrigin: true, rewrite: stripApiPrefix },
      '/api/orders': { target: ordersApi, changeOrigin: true, rewrite: stripApiPrefix },
      '/api/couriers': { target: dispatchApi, changeOrigin: true, rewrite: stripApiPrefix },
    },
  },
  build: {
    outDir: 'dist',
    sourcemap: true,
  },
});
