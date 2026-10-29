import { defineConfig } from '@playwright/test';

export default defineConfig({
  testDir: '.',
  timeout: 30000,
  reporter: [['list']],
  use: {
    baseURL: 'http://localhost:5299',
    viewport: { width: 1440, height: 900 },
  },
});
