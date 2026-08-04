import { http, HttpResponse } from 'msw';

export const handlers = [
  http.get('/v1/client-config', () =>
    HttpResponse.json({
      applicationInsightsConnectionString: null,
      signInEnabled: true,
      loginUrl: '/v1/auth/login',
      logoutUrl: '/v1/auth/logout'
    })
  ),
  http.get('/v1/status', () =>
    HttpResponse.json({
      environment: 'Test',
      authenticated: false
    })
  ),
  http.get('/v1/auth/me', () =>
    HttpResponse.json({
      isAuthenticated: false,
      displayName: null
    })
  ),
  http.get('/v1/auth/antiforgery', () => HttpResponse.json({ token: 'test-antiforgery-token' }))
];
