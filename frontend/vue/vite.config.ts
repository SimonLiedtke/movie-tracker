import vue from '@vitejs/plugin-vue';
import { defineConfig } from 'vitest/config';

export default defineConfig({
  plugins: [vue()],
  server: {
    // The same port as the Angular dev server (not meant to run at the same time): the Google OAuth
    // client already allows http://localhost:4200/api/auth/callback/google as redirect URI.
    port: 4200,
    // The backend needs no CORS setup: /api is forwarded to it.
    proxy: {
      '/api': { target: 'http://localhost:5080', secure: false },
    },
  },
  test: {
    environment: 'jsdom',
    include: ['src/**/*.spec.ts'],
  },
});
