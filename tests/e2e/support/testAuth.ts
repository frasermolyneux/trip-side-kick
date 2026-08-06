import type { Page } from '@playwright/test';

/**
 * Signs a Playwright `page` in as a deterministic test identity via the app's fail-closed
 * test-only sign-in endpoint (`GET /testauth/signin`) - see `TestAuthEndpoints.cs`. No real Entra
 * tenant, token, or redirect is involved; this only works because the harness starts the app
 * with `TestAuth__Enabled=true` in `ASPNETCORE_ENVIRONMENT=Development` (see `support/appProcess.ts`).
 *
 * The endpoint itself just returns a JSON acknowledgement (it is not a redirect target), so this
 * helper navigates on to `goToAfterSignIn` (default `/trips`) once the cookie is set.
 */
export async function signInAs(
  page: Page,
  identity: { subjectId: string; email: string; displayName?: string },
  overrides?: { goToAfterSignIn?: string }
): Promise<void> {
  const { subjectId, email, displayName = 'E2E Test User' } = identity;
  const goToAfterSignIn = overrides?.goToAfterSignIn ?? '/trips';

  const params = new URLSearchParams({ sub: subjectId, email, name: displayName });
  await page.goto(`/testauth/signin?${params.toString()}`);
  await page.goto(goToAfterSignIn);
}

/** Deterministic subject ids for the standing cast of E2E test identities. */
export const TEST_IDENTITIES = {
  owner: { subjectId: 'e2e-owner-0001', email: 'owner@e2e.tripsidekick.test', displayName: 'Olivia Owner' },
  editor: { subjectId: 'e2e-editor-0001', email: 'editor@e2e.tripsidekick.test', displayName: 'Eddie Editor' },
  viewer: { subjectId: 'e2e-viewer-0001', email: 'viewer@e2e.tripsidekick.test', displayName: 'Violet Viewer' },
  secondOwner: { subjectId: 'e2e-owner-0002', email: 'owner2@e2e.tripsidekick.test', displayName: 'Oscar Owner' },
  mismatched: { subjectId: 'e2e-mismatched-0001', email: 'not-invited@e2e.tripsidekick.test', displayName: 'Mallory Mismatched' }
} as const;
