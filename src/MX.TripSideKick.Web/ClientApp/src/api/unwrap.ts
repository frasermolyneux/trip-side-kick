/**
 * `openapi-fetch` never throws on non-2xx responses - it returns `{ data, error }`. Every query/
 * mutation function in `src/queries` funnels its result through this so TanStack Query's own
 * error handling (retries, `isError`, thrown-in-mutation rejection, etc.) works as expected.
 */
export function unwrap<T>(result: { data?: T; response: Response }): T {
  if (result.data === undefined) {
    throw new ApiError(result.response.status, result.response.statusText);
  }

  return result.data;
}

export class ApiError extends Error {
  readonly status: number;

  constructor(status: number, statusText: string) {
    super(`Request failed with status ${status} ${statusText}`);
    this.name = 'ApiError';
    this.status = status;
  }
}
