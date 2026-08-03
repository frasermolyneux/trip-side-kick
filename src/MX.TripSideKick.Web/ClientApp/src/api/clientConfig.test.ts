import { describe, expect, it } from 'vitest';
import { http, HttpResponse } from 'msw';

import { fetchClientConfig } from './clientConfig';
import { server } from '../mocks/server';

describe('fetchClientConfig', () => {
  it('maps the BFF payload', async () => {
    const config = await fetchClientConfig();

    expect(config.signInEnabled).toBe(false);
    expect(config.applicationInsightsConnectionString).toBeNull();
  });

  it('falls back to safe defaults when the endpoint fails', async () => {
    server.use(http.get('/v1/client-config', () => new HttpResponse(null, { status: 500 })));

    const config = await fetchClientConfig();

    expect(config).toEqual({ applicationInsightsConnectionString: null, signInEnabled: false });
  });
});
