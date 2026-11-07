import { describe, expect, it } from 'vitest';
import { mergeQuery, queryString } from './query';

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

  it('does not change the current query', () => {
    const current = { page: '2' };
    mergeQuery(current, { page: undefined });

    expect(current).toEqual({ page: '2' });
  });
});
