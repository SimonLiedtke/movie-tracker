import { isAxiosError } from 'axios';

/** RFC 9457 problem details, as returned by the backend for errors. */
interface ProblemDetails {
  title?: string;
  detail?: string;
  status?: number;
  errors?: Record<string, string[]>;
}

export interface ApiError {
  status: number;
  message: string;
  /** Validation messages, one per line. */
  details: string[];
}

/** Turns any error from an API call into a message that can be shown to the user. */
export function toApiError(error: unknown): ApiError {
  if (!isAxiosError(error)) {
    return { status: 0, message: 'An unexpected error occurred.', details: [] };
  }

  // No response: the request did not get through.
  if (!error.response) {
    return {
      status: 0,
      message: 'The API cannot be reached. Is the backend running on http://localhost:5080?',
      details: [],
    };
  }

  const { status, data } = error.response;
  const problem: ProblemDetails = typeof data === 'object' && data !== null ? data : {};
  const details = Object.values(problem.errors ?? {}).flat();
  const message = problem.detail ?? problem.title ?? `Request failed (HTTP ${status}).`;

  return { status, message, details };
}
