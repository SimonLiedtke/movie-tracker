import { describe, expect, it } from 'vitest';
import { AxiosError, type AxiosResponse } from 'axios';
import { toApiError } from './api-error';

/** What axios throws when the server answers with an error status. */
const failure = (status: number, data: unknown) =>
  new AxiosError('Request failed', AxiosError.ERR_BAD_REQUEST, undefined, undefined, {
    status,
    data,
  } as AxiosResponse);

describe('toApiError', () => {
  it('explains that the backend is unreachable', () => {
    const error = toApiError(new AxiosError('Network Error', AxiosError.ERR_NETWORK));
    expect(error.status).toBe(0);
    expect(error.message).toContain('cannot be reached');
  });

  it('uses the detail of problem details', () => {
    const error = toApiError(
      failure(409, {
        title: 'Duplicate TMDB ID',
        detail: 'Another movie with TMDB ID 348 already exists.',
      }),
    );
    expect(error).toEqual({
      status: 409,
      message: 'Another movie with TMDB ID 348 already exists.',
      details: [],
    });
  });

  it('lists validation errors', () => {
    const error = toApiError(
      failure(400, {
        title: 'One or more validation errors occurred.',
        errors: {
          title: ['The title field is required.'],
          genreIds: ['Unknown genre IDs: 99999.'],
        },
      }),
    );
    expect(error.message).toBe('One or more validation errors occurred.');
    expect(error.details).toEqual(['The title field is required.', 'Unknown genre IDs: 99999.']);
  });

  it('falls back to the status code', () => {
    expect(toApiError(failure(500, 'boom')).message).toBe('Request failed (HTTP 500).');
  });

  it('does not know what went wrong with anything else', () => {
    expect(toApiError(new Error('oops'))).toEqual({
      status: 0,
      message: 'An unexpected error occurred.',
      details: [],
    });
  });
});
