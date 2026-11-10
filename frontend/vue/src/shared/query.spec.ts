import { describe, expect, it } from 'vitest';
import { mergeQuery, queryString, queryStrings } from './query';

describe('queryString', () => {
  it('is the value of the parameter', () => {
    expect(queryString('alien')).toBe('alien');
    expect(queryString('')).toBe('');
  });

  it('is the first value of a parameter that is given more than once', () => {
    expect(queryString(['alien', 'heat'])).toBe('alien');
  });

  it('is undefined for a missing parameter, or one without value', () => {
    expect(queryString(undefined)).toBeUndefined();
    expect(queryString(null)).toBeUndefined();
    expect(queryString([])).toBeUndefined();
  });
});

describe('queryStrings', () => {
  it('is all values of the parameter', () => {
    expect(queryStrings('27')).toEqual(['27']);
    expect(queryStrings(['27', '878'])).toEqual(['27', '878']);
  });

  it('is empty for a missing parameter, and skips values without text', () => {
    expect(queryStrings(undefined)).toEqual([]);
    expect(queryStrings(null)).toEqual([]);
    expect(queryStrings(['27', null])).toEqual(['27']);
  });
});

describe('mergeQuery', () => {
  it('keeps the parameters that are not changed', () => {
    expect(mergeQuery({ search: 'alien', page: '2' }, { year: 1979 })).toEqual({
      search: 'alien',
      page: '2',
      year: '1979',
    });
  });

  it('sets and removes parameters', () => {
    expect(mergeQuery({ search: 'alien', page: '2' }, { search: 'heat', page: undefined })).toEqual(
      { search: 'heat' },
    );
    expect(mergeQuery({ page: '2' }, { page: null })).toEqual({});
  });

  it('sets a parameter that is repeated for an array, and removes it for an empty one', () => {
    expect(mergeQuery({ page: '2' }, { genreId: [27, '878'] })).toEqual({
      page: '2',
      genreId: ['27', '878'],
    });
    expect(mergeQuery({ genreId: ['27', '878'] }, { genreId: [] })).toEqual({});
  });

  it('does not change the current query', () => {
    const current = { page: '2' };
    mergeQuery(current, { page: undefined });

    expect(current).toEqual({ page: '2' });
  });
});
