import { describe, expect, it } from 'vitest';
import { formatDate, formatDay, formatMonth, todayIso, yearOf } from './dates';

describe('date formatting', () => {
  it('formats the month', () => {
    expect(formatMonth('2026-11-07')).toBe('November 2026');
  });

  it('formats the day with its weekday', () => {
    expect(formatDay('2026-11-07')).toBe('Sat 7');
  });

  it('formats a full date', () => {
    expect(formatDate('2026-01-31')).toBe('January 31, 2026');
  });

  it('takes the year of a date', () => {
    expect(yearOf('1979-05-25')).toBe('1979');
    expect(yearOf(null)).toBe('');
    expect(yearOf(undefined)).toBe('');
  });

  it("knows today's date in the local time zone", () => {
    expect(todayIso()).toMatch(/^\d{4}-\d{2}-\d{2}$/);
  });
});
