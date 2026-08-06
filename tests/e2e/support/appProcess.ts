import { spawn, type ChildProcess } from 'node:child_process';
import { existsSync } from 'node:fs';
import { fileURLToPath } from 'node:url';

import {
  APP_HOST,
  APP_PUBLISH_DIR_ENV_VAR,
  E2E_HTTP_PORT,
  E2E_HTTPS_PORT,
  REPO_ROOT,
  SITE_HOST,
  WEB_PROJECT_DIR
} from './env.ts';

const PUBLISH_DIR = new URL('.app-publish/', import.meta.url);

/**
 * Publishes `MX.TripSideKick.Web` (full publish, including the Vite client build via the
 * project's MSBuild targets - see `EnsureClientDependencies`/`BuildClient`/`PublishClientAssets`)
 * unless `E2E_APP_PUBLISH_DIR` already points at a pre-built output (e.g. the artifact the
 * `build-and-test` CI job already produced - see the Playwright CI job in
 * `.github/workflows/pr-verify.yml`, which downloads it instead of publishing again).
 */
export async function ensureAppPublished(): Promise<string> {
  const override = process.env[APP_PUBLISH_DIR_ENV_VAR];
  if (override && existsSync(override)) {
    return override;
  }

  const outDir = fileURLToPath(PUBLISH_DIR);

  await new Promise<void>((resolve, reject) => {
    const child = spawn(
      'dotnet',
      ['publish', fileURLToPath(WEB_PROJECT_DIR), '-c', 'Release', '-o', outDir],
      {
        cwd: fileURLToPath(REPO_ROOT),
        stdio: 'inherit',
        shell: process.platform === 'win32'
      }
    );

    child.on('error', reject);
    child.on('exit', (code) => (code === 0 ? resolve() : reject(new Error(`dotnet publish exited with code ${code}`))));
  });

  return outDir;
}

/** Ensures a local HTTPS development certificate exists for Kestrel to bind with. */
export async function ensureDevCertificate(): Promise<void> {
  await new Promise<void>((resolve, reject) => {
    const child = spawn('dotnet', ['dev-certs', 'https'], {
      stdio: 'inherit',
      shell: process.platform === 'win32'
    });

    child.on('error', reject);
    child.on('exit', (code) => (code === 0 ? resolve() : reject(new Error(`dotnet dev-certs https exited with code ${code}`))));
  });
}

/**
 * Starts the published app as a child process with explicit env vars (rather than relying on
 * `Properties/launchSettings.json`, which `dotnet <dll>` never reads) so behaviour is identical
 * locally and in CI. Test-auth is opted in via `TestAuth__Enabled=true` - see
 * `TestAuthEndpoints`'s fail-closed gating (also requires `ASPNETCORE_ENVIRONMENT=Development`,
 * set below).
 */
export function startApp(publishDir: string, sqlConnectionString: string): ChildProcess {
  const child = spawn('dotnet', [`${publishDir}/MX.TripSideKick.Web.dll`], {
    // Without an explicit cwd, spawn() defaults to this process's own working directory (the e2e
    // test runner's), NOT the publish output - and ASP.NET Core resolves ContentRootPath from the
    // process's current directory when no --contentRoot is passed. That silently broke the
    // site-surface SiteAssets PhysicalFileProvider (Program.cs), which only ever needs a directory
    // relative to the real content root.
    cwd: publishDir,
    env: {
      ...process.env,
      ASPNETCORE_ENVIRONMENT: 'Development',
      ASPNETCORE_URLS: `https://localhost:${E2E_HTTPS_PORT};http://localhost:${E2E_HTTP_PORT}`,
      TestAuth__Enabled: 'true',
      Sql__ConnectionString: sqlConnectionString,
      'HostRouting__AppHosts__0': APP_HOST,
      'HostRouting__SiteHosts__0': SITE_HOST,
      // Deterministic, non-secret placeholders so Microsoft.Identity.Web can bind its options at
      // startup - no real Entra tenant is ever contacted (E2E signs in via /testauth/signin only).
      // Mirrors TripSideKickApplicationFactory's test configuration.
      'AzureAd__Instance': 'https://login.microsoftonline.com/',
      'AzureAd__TenantId': '00000000-0000-0000-0000-000000000000',
      'AzureAd__ClientId': '11111111-1111-1111-1111-111111111111',
      'AzureAd__CallbackPath': '/signin-oidc',
      'AzureAd__SignedOutCallbackPath': '/signout-callback-oidc',
      'ApplicationInsights__ClientConnectionString': '',
      'BlobStorage__ServiceUri': ''
    },
    stdio: 'inherit',
    shell: process.platform === 'win32'
  });

  return child;
}

export async function waitForHealthy(baseUrl: string, timeoutMs = 60_000): Promise<void> {
  const deadline = Date.now() + timeoutMs;
  let lastError: unknown;

  while (Date.now() < deadline) {
    try {
      // manual redirect: Program.cs's UseHttpsRedirection() 307s every plain-HTTP request
      // (including this liveness probe) to the HTTPS origin, and Node's fetch would otherwise
      // follow that redirect and fail TLS validation against the untrusted local dev certificate.
      // A 307 here already proves Kestrel is up and the request pipeline is running, which is all
      // this liveness gate needs - the actual health check body is exercised over HTTPS by the
      // browser-driven specs (whose Playwright context sets ignoreHTTPSErrors).
      const response = await fetch(`${baseUrl}/api/health/live`, { redirect: 'manual' });
      if (response.ok || (response.status >= 300 && response.status < 400)) {
        return;
      }
    } catch (error) {
      lastError = error;
    }

    await new Promise((resolve) => setTimeout(resolve, 1_000));
  }

  throw new Error(`App did not become healthy within ${timeoutMs}ms: ${String(lastError)}`);
}

export async function stopApp(child: ChildProcess): Promise<void> {
  if (child.exitCode !== null || child.pid === undefined) {
    return;
  }

  await new Promise<void>((resolve) => {
    child.once('exit', () => resolve());
    child.kill();

    // Force-kill after a grace period in case the app doesn't shut down promptly.
    setTimeout(() => {
      if (child.exitCode === null) {
        child.kill('SIGKILL');
      }
    }, 5_000);
  });
}
