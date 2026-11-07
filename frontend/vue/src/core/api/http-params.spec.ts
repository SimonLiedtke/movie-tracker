import { describe, expect, it } from 'vitest';
import { toSearchParams } from './http-params';

describe('toSearchParams', () => {
  it('leaves out empty values', () => {
    const params = toSearchParams({
      search: 'alien',
      genreId: undefined,
      year: null,
      from: '',
      page: 2,
    });
    expect(params.toString()).toBe('search=alien&page=2');
  });

  it('encodes what needs it', () => {
    expect(toSearchParams({ query: 'the thing & co' }).toString()).toBe('query=the+thing+%26+co');
  });
});
