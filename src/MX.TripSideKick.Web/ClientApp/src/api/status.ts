export interface StatusResponse {
  environment: string;
  authenticated: boolean;
}

export async function fetchStatus(signal?: AbortSignal): Promise<StatusResponse> {
  const response = await fetch('/v1/status', {
    headers: { Accept: 'application/json' },
    credentials: 'same-origin',
    signal
  });

  if (!response.ok) {
    throw new Error(`Status request failed with ${response.status}`);
  }

  return (await response.json()) as StatusResponse;
}
