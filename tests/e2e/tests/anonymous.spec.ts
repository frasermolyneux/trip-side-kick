import { test, expect } from '@playwright/test';

import { BASE_URL } from '../support/env.ts';

/**
 * Journey 1/2 precondition: an anonymous visitor cannot reach any app-surface page or `/v1` data.
 *
 * `RequireAuth` (see `App.tsx`) redirects an anonymous visitor via `window.location.href` to
 * `/v1/auth/login`, which issues a real OpenID Connect challenge to Entra External ID
 * (`AuthController.Login`). To keep this test fully hermetic, the challenge endpoint is
 * intercepted via `page.route` *before* navigating, so the browser never actually leaves
 * `localhost` even though the app's redirect logic runs for real - this proves the redirect
 * attempt happens without making any live network call.
 */
test.describe('Anonymous access', () => {
  test('cannot read trip data via the API while signed out', async ({ request }) => {
    const response = await request.get(`${BASE_URL}/v1/trips`);
    expect(response.status()).toBe(401);
  });

  test('is redirected away from an app route rather than shown trip content', async ({ page }) => {
    await page.route('**/v1/auth/login**', (route) =>
      route.fulfill({ status: 200, contentType: 'text/plain', body: 'e2e-intercepted-oidc-challenge' })
    );

    await page.goto('/trips');

    // RequireAuth sets window.location.href synchronously during render; wait for that navigation
    // to complete against our intercepted route rather than a real Entra endpoint.
    await page.waitForURL('**/v1/auth/login**');
    await expect(page.getByText('e2e-intercepted-oidc-challenge')).toBeVisible();
  });
});
