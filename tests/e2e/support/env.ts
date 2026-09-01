/**
 * Central, fixed configuration for the hermetic E2E harness. Ports are pinned (rather than
 * dynamically chosen) so `playwright.config.ts`'s `baseURL` and the spec files can rely on a
 * single well-known origin; this only conflicts with a developer's own `dotnet run` session
 * (which defaults to 7207/5207 per `Properties/launchSettings.json`) if both are running at once,
 * which is an acceptable, documented local-dev constraint (see docs/testing.md).
 */
export const E2E_HTTPS_PORT = 8543;
export const E2E_HTTP_PORT = 8580;
export const BASE_URL = `https://localhost:${E2E_HTTPS_PORT}`;

/**
 * Plain-HTTP origin for the same app instance, used only by Node-side health polling
 * (`support/appProcess.ts`'s `waitForHealthy`). Node's built-in `fetch` validates TLS certificates
 * independently of Playwright's browser context (which is configured with `ignoreHTTPSErrors`), so
 * polling over HTTPS against the local ASP.NET Core dev certificate would fail in CI where the
 * cert isn't trusted by the OS/Node. Health endpoints aren't host- or scheme-restricted, so HTTP is
 * safe to use here even though the browser-driven specs always use `BASE_URL` (HTTPS).
 */
export const HTTP_BASE_URL = `http://localhost:${E2E_HTTP_PORT}`;

/** The app-surface host name the running instance is configured to recognise (see HostRouting). */
export const APP_HOST = 'localhost';

/** A host name deliberately NOT in the app's HostRouting allow-list, for negative host-routing assertions. */
export const UNKNOWN_HOST = 'unknown.example.invalid';

/** A host name that plays the role of the brochure/site surface for host-routing assertions. */
export const SITE_HOST = 'site.localhost';

export const SQL_DATABASE_NAME = 'TripSideKickE2E';
export const SQL_SA_PASSWORD = 'E2E-Test-Only-Passw0rd!';

/** Repo-relative paths, resolved from this file's location so the harness works from any cwd. */
export const REPO_ROOT = new URL('../../../', import.meta.url);
export const WEB_PROJECT_DIR = new URL('src/MX.TripSideKick.Web/', REPO_ROOT);
export const INFRASTRUCTURE_PROJECT_DIR = new URL('src/MX.TripSideKick.Infrastructure/', REPO_ROOT);
export const SOLUTION_PATH = new URL('src/MX.TripSideKick.slnx', REPO_ROOT);

/**
 * Environment variable a developer/CI job can set to point at an already-published
 * `MX.TripSideKick.Web` output directory (e.g. the artifact the `build-and-test` CI job already
 * produced), avoiding a second, slower `dotnet publish` (which itself runs the full Vite client
 * build via MSBuild). When unset, global-setup performs its own publish into `.app-publish/`.
 */
export const APP_PUBLISH_DIR_ENV_VAR = 'E2E_APP_PUBLISH_DIR';

/** Env var read by `TripSideKickDbContextFactory` to redirect `dotnet ef` tooling at a specific database. */
export const MIGRATION_CONNECTION_STRING_ENV_VAR = 'TRIPSIDEKICK_MIGRATION_CONNECTION_STRING';
