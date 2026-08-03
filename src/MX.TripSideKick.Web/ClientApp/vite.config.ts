/// <reference types="vitest/config" />
import { defineConfig } from 'vite';
import react from '@vitejs/plugin-react';

// The ASP.NET Core host serves the built PWA from wwwroot, so Vite emits directly into it.
// wwwroot is generated output and is git-ignored.
export default defineConfig({
  plugins: [react()],
  base: '/',
  build: {
    outDir: '../wwwroot',
    emptyOutDir: true,
    sourcemap: false
  },
  server: {
    port: 5173,
    proxy: {
      '/v1': {
        target: 'https://localhost:7207',
        changeOrigin: false,
        secure: false
      },
      '/api': {
        target: 'https://localhost:7207',
        changeOrigin: false,
        secure: false
      }
    }
  },
  test: {
    environment: 'jsdom',
    globals: true,
    setupFiles: ['./src/setupTests.ts'],
    css: false,
    include: ['src/**/*.{test,spec}.{ts,tsx}']
  }
});
