import { HttpErrorResponse } from '@angular/common/http';
import { toApiError } from './api-error';

describe('toApiError', () => {
  it('explains that the backend is unreachable', () => {
    const error = toApiError(new HttpErrorResponse({ status: 0 }));
    expect(error.status).toBe(0);
    expect(error.message).toContain('cannot be reached');
  });

  it('uses the detail of problem details', () => {
    const error = toApiError(
      new HttpErrorResponse({
        status: 409,
        error: {
          title: 'Duplicate TMDB ID',
          detail: 'Another movie with TMDB ID 348 already exists.',
        },
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
      new HttpErrorResponse({
        status: 400,
        error: {
          title: 'One or more validation errors occurred.',
          errors: {
            title: ['The title field is required.'],
            genreIds: ['Unknown genre IDs: 99999.'],
          },
        },
      }),
    );
    expect(error.message).toBe('One or more validation errors occurred.');
    expect(error.details).toEqual(['The title field is required.', 'Unknown genre IDs: 99999.']);
  });

  it('falls back to the status code', () => {
    expect(toApiError(new HttpErrorResponse({ status: 500, error: 'boom' })).message).toBe(
      'Request failed (HTTP 500).',
    );
  });
});
