import { describe, expect, it } from 'vitest';
import { RATING_OPTIONS, formatRating } from './rating-format';

describe('formatRating', () => {
  it('formats half stars', () => {
    expect(formatRating(1)).toBe('½');
    expect(formatRating(7)).toBe('★★★½');
    expect(formatRating(10)).toBe('★★★★★');
  });

  it('returns an empty string without a rating', () => {
    expect(formatRating(null)).toBe('');
  });

  it('offers ratings from 0.5 to 5 stars', () => {
    expect(RATING_OPTIONS.map((option) => option.value)).toEqual([1, 2, 3, 4, 5, 6, 7, 8, 9, 10]);
    expect(RATING_OPTIONS[6].label).toBe('★★★½ (3.5)');
  });
});
