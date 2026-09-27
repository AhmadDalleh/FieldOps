import { defineConfig, devices } from '@playwright/test';

/**
 * End-to-end flows against a running API and `npm start` (see CLAUDE.md).
 * Set PLAYWRIGHT_CHROMIUM_PATH to use an already installed Chromium instead of Playwright's download.
 */
export default defineConfig({
  testDir: './e2e',
  timeout: 60_000,
  fullyParallel: false,
  retries: 0,
  reporter: 'list',
  use: {
    baseURL: process.env['E2E_BASE_URL'] ?? 'http://localhost:4200',
    trace: 'retain-on-failure',
    launchOptions: process.env['PLAYWRIGHT_CHROMIUM_PATH'] ? { executablePath: process.env['PLAYWRIGHT_CHROMIUM_PATH'] } : {},
  },
  projects: [
    { name: 'desktop', use: { ...devices['Desktop Chrome'] }, testIgnore: /technician/ },
    { name: 'phone', use: { ...devices['Pixel 7'] }, testMatch: /technician/ },
  ],
});
