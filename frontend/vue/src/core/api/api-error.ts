import { HttpError } from './http';

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
  if (!(error instanceof HttpError)) {
    return { status: 0, message: 'An unexpected error occurred.', details: [] };
  }

  if (error.status === 0) {
    return {
      status: 0,
      message: 'The API cannot be reached. Is the backend running on http://localhost:5080?',
      details: [],
    };
  }

  const problem: ProblemDetails =
    typeof error.error === 'object' && error.error !== null ? error.error : {};
  const details = Object.values(problem.errors ?? {}).flat();
  const message = problem.detail ?? problem.title ?? `Request failed (HTTP ${error.status}).`;

  return { status: error.status, message, details };
}
