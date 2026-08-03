import { ApplicationInsights } from '@microsoft/applicationinsights-web';

let appInsights: ApplicationInsights | undefined;

/**
 * Initialises browser telemetry. Never attach trip content, documents, booking references or
 * email addresses to telemetry — see docs/architecture-overview.md.
 */
export function initialiseTelemetry(connectionString: string | null): ApplicationInsights | undefined {
  if (!connectionString || appInsights) {
    return appInsights;
  }

  appInsights = new ApplicationInsights({
    config: {
      connectionString,
      enableAutoRouteTracking: true,
      disableCookiesUsage: true,
      disableExceptionTracking: false
    }
  });

  appInsights.loadAppInsights();
  appInsights.trackPageView();

  return appInsights;
}
