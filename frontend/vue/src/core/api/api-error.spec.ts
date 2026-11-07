import { describe, expect, it } from 'vitest';
import { toApiError } from './api-error';
import { HttpError } from './http';

describe('toApiError', () => {
  it('explains that the backend is unreachable', () => {
    const error = toApiError(new HttpError(0, '/api/movies', new TypeError('Failed to fetch')));
    expect(error.status).toBe(0);
    expect(error.message).toContain('cannot be reached');
  });

  it('uses the detail of problem details', () => {
    const error = toApiError(
      new HttpError(409, '/api/movies', {
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
      new HttpError(400, '/api/movies', {
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
    expect(toApiError(new HttpError(500, '/api/movies', 'boom')).message).toBe(
      'Request failed (HTTP 500).',
    );
  });

  it('does not know what went wrong with anything else', () => {
    expect(toApiError(new Error('oops'))).toEqual({
      status: 0,
      message: 'An unexpected error occurred.',
      details: [],
    });
  });
});
