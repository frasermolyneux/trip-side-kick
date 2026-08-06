import { test, expect } from '@playwright/test';

import { BASE_URL, UNKNOWN_HOST } from '../support/env.ts';

/**
 * Proves the real host-routing split (`HostSurfaceMiddleware`) without needing real multi-hostname
 * DNS locally: only `localhost` resolves in this harness, so these assertions send an explicit
 * `Host` header via a raw HTTP client (`request`, not `page.goto`) - browsers refuse to let page
 * JavaScript override the `Host` header, but Kestrel only ever inspects the header value it
 * receives, regardless of what DNS name/IP the TCP connection actually arrived on.
 */
test.describe('Host-routing split', () => {
  test('an unrecognised Host header is rejected with 400, even for an otherwise-valid path', async ({ request }) => {
    const response = await request.get(`${BASE_URL}/api/health/live`, {
      headers: { Host: UNKNOWN_HOST }
    });

    expect(response.status()).toBe(400);
    expect(await response.text()).toContain('Unrecognised host');
  });

  test('operational endpoints are reachable regardless of which configured host is used', async ({ request }) => {
    const live = await request.get(`${BASE_URL}/api/health/live`);
    expect(live.status()).toBe(200);

    const ready = await request.get(`${BASE_URL}/api/health/ready`);
    // SQL readiness may or may not be fully warmed up yet; either way this must not be blocked by
    // host routing, and must never be a host-rejection 400.
    expect(ready.status()).not.toBe(400);
  });

  test('v1 API endpoints require the app host', async ({ request }) => {
    // The app host (`localhost`, configured via HostRouting__AppHosts__0 - see support/appProcess.ts)
    // can reach /v1 routes (this request is unauthenticated, so 401 is the expected app-host
    // behaviour - the point here is that it is NOT the host-rejection 400).
    const onAppHost = await request.get(`${BASE_URL}/v1/trips`);
    expect(onAppHost.status()).toBe(401);

    // The same path via a host that isn't in any allow-list is rejected before routing even runs.
    const onUnknownHost = await request.get(`${BASE_URL}/v1/trips`, {
      headers: { Host: UNKNOWN_HOST }
    });
    expect(onUnknownHost.status()).toBe(400);
  });
});
