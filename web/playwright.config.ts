import { defineConfig } from '@playwright/test';

/**
 * E2E suite for MyStore (admin console :4201 + storefront :4200).
 *
 * Prerequisites — the full dev stack must be running:
 *   1. API:        dotnet run --project Store.Api --launch-profile https   (from extracted/MyStore)
 *   2. admin:      npx ng serve admin --port 4201                          (from extracted/MyStore/web)
 *   3. storefront: npx ng serve storefront --port 4200                     (from extracted/MyStore/web)
 *
 * Run:   npm run e2e           (headless)
 *        npm run e2e:headed    (visible Chrome — for watching audits)
 *
 * Uses the installed Google Chrome (channel) so no browser download is needed.
 * Workers = 1: some specs mutate shared dev data (refund, restore, media) and
 * must not interleave.
 */
export default defineConfig({
  testDir: './e2e',
  timeout: 45_000,
  expect: { timeout: 10_000 },
  fullyParallel: false,
  workers: 1,
  retries: 0,
  reporter: [['list'], ['html', { open: 'never' }]],
  use: {
    channel: 'chrome',
    screenshot: 'only-on-failure',
    trace: 'retain-on-failure',
  },
  projects: [
    {
      name: 'admin',
      testMatch: /admin-.*\.spec\.ts/,
      use: { baseURL: 'http://localhost:4201' },
    },
    {
      name: 'storefront',
      testMatch: /storefront-.*\.spec\.ts/,
      use: { baseURL: 'http://localhost:4200' },
    },
  ],
});
