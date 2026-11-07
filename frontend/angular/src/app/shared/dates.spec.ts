import { formatDate, formatDay, formatMonth } from './dates';

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
});
