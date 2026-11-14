import { dueStatus, formatDueDate } from './due-date';

const now = new Date('2026-03-10T12:00:00Z');

describe('dueStatus', () => {
  it.each([
    ['2026-03-10T11:59:59Z', 'overdue'],
    ['2026-03-10T12:00:01Z', 'soon'],
    ['2026-03-11T12:00:00Z', 'soon'],
    ['2026-03-11T12:00:01Z', 'later'],
  ])('%s is %s', (due, expected) => {
    expect(dueStatus(due, now)).toBe(expected);
  });
});

describe('formatDueDate', () => {
  it('omits the year for the current year only', () => {
    expect(formatDueDate('2026-05-01T12:00:00Z', now)).toBe('May 1');
    expect(formatDueDate('2027-05-01T12:00:00Z', now)).toBe('May 1, 2027');
  });
});
