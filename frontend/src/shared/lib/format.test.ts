import { formatRelativeTime, fromDateInputValue, toDateInputValue } from './format';

const NOW = new Date('2026-03-10T12:00:00Z');

describe('formatRelativeTime', () => {
  it.each([
    ['2026-03-10T11:59:40Z', 'just now'],
    ['2026-03-10T11:55:00Z', '5 min ago'],
    ['2026-03-10T09:00:00Z', '3 h ago'],
    ['2026-03-08T12:00:00Z', '2 d ago'],
    ['2026-01-02T12:00:00Z', 'Jan 2, 2026'],
  ])('%s -> %s', (iso, expected) => {
    expect(formatRelativeTime(iso, NOW)).toBe(expected);
  });
});

describe('date input conversion', () => {
  it('round-trips a date', () => {
    expect(fromDateInputValue('2026-05-04')).toBe('2026-05-04T00:00:00Z');
    expect(toDateInputValue('2026-05-04T00:00:00Z')).toBe('2026-05-04');
  });

  it('treats empty values as no date', () => {
    expect(fromDateInputValue('')).toBeNull();
    expect(toDateInputValue(null)).toBe('');
    expect(toDateInputValue(undefined)).toBe('');
  });
});
