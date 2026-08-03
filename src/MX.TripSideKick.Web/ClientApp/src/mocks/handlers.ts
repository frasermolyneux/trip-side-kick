import { http, HttpResponse } from 'msw';

export const handlers = [
  http.get('/v1/client-config', () =>
    HttpResponse.json({
      applicationInsightsConnectionString: null,
      signInEnabled: false
    })
  ),
  http.get('/v1/status', () =>
    HttpResponse.json({
      environment: 'Test',
      authenticated: false,
      authenticationStubbed: true
    })
  )
];
