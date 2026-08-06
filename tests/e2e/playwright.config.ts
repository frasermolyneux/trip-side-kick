import { defineConfig } from '@playwright/test';

import { BASE_URL } from './support/env.ts';

/**
 * Hermetic E2E suite for Trip Side Kick's app surface (Journeys 1 & 2). No cloud, no real Entra,
 * no external network - see `docs/testing.md`. `globalSetup` owns the entire lifecycle (SQL
 * container, migrations, published app process) rather than the `webServer` option, because the
 * app can only start once the SQL container's connection string is known.
 *
 * Single worker: all specs share one app instance + one database, so tests must not run
 * concurrently against overlapping data (each spec creates its own trip(s) via distinct test
 * identities to avoid cross-test interference).
 */
export default defineConfig({
  testDir: './tests',
  // 30s (the Playwright default) is too tight for the multi-invite step in journey-1-and-2.spec.ts
  // (three sequential MUI select + fill + submit round trips against a real dotnet-publish app and
  // real SQL container - no mocking), especially on a cold-cache/first-run machine.
  timeout: 60_000,
  expect: { timeout: 5_000 },
  fullyParallel: false,
  workers: 1,
  retries: process.env.CI ? 1 : 0,
  reporter: process.env.CI ? [['github'], ['html', { open: 'never' }]] : [['list'], ['html', { open: 'never' }]],
  globalSetup: './global-setup.ts',
  use: {
    baseURL: BASE_URL,
    ignoreHTTPSErrors: true,
    trace: 'retain-on-failure',
    screenshot: 'only-on-failure'
  }
});
