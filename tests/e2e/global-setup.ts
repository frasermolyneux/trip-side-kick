import type { FullConfig } from '@playwright/test';

import { HTTP_BASE_URL } from './support/env.ts';
import { restoreDotnetTools, applyMigrations } from './support/migrate.ts';
import { startSqlContainer, buildConnectionString } from './support/sqlContainer.ts';
import { ensureAppPublished, ensureDevCertificate, startApp, stopApp, waitForHealthy } from './support/appProcess.ts';

/**
 * Hermetic, self-contained global setup for the whole Playwright suite: no cloud, no real Entra,
 * no external network. Starts an ephemeral SQL Server container, applies EF Core migrations to
 * it, then starts the real built app configured to use it, with the deterministic test-auth
 * sign-in path opted in (`TestAuth__Enabled=true`) - see docs/testing.md.
 *
 * Deliberately NOT using Playwright's `webServer` config option: it starts before this function
 * runs, but the app can only be started once the SQL container's connection string is known - so
 * this file owns the app process directly instead.
 *
 * Returns a teardown function (Playwright's supported alternative to a separate
 * `globalTeardown` file) so the container/child-process handles never need to be serialised
 * across a process boundary.
 */
export default async function globalSetup(_config: FullConfig): Promise<() => Promise<void>> {
  console.log('[global-setup] Restoring local dotnet tools (dotnet-ef)...');
  await restoreDotnetTools();

  console.log('[global-setup] Starting ephemeral SQL Server container...');
  const container = await startSqlContainer();
  const connectionString = buildConnectionString(container);

  console.log('[global-setup] Applying EF Core migrations...');
  await applyMigrations(connectionString);

  console.log('[global-setup] Ensuring HTTPS development certificate exists...');
  await ensureDevCertificate();

  console.log('[global-setup] Publishing (or reusing a pre-built) MX.TripSideKick.Web...');
  const publishDir = await ensureAppPublished();

  console.log('[global-setup] Starting the app...');
  const app = startApp(publishDir, connectionString);

  console.log('[global-setup] Waiting for /api/health/live...');
  await waitForHealthy(HTTP_BASE_URL);

  console.log('[global-setup] Ready.');

  return async () => {
    console.log('[global-teardown] Stopping app...');
    await stopApp(app);

    console.log('[global-teardown] Stopping SQL container...');
    await container.stop();
  };
}
