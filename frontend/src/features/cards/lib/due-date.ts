export type DueStatus = 'overdue' | 'soon' | 'later';

const DAY_MS = 24 * 60 * 60 * 1000;

/** `overdue`: before now. `soon`: within the next 24 hours. Everything else is `later`. */
export function dueStatus(dueDate: string, now: Date): DueStatus {
  const due = new Date(dueDate).getTime();
  if (due < now.getTime()) {
    return 'overdue';
  }
  return due - now.getTime() <= DAY_MS ? 'soon' : 'later';
}

/** "Mar 5" within the current year, "Mar 5, 2027" otherwise. */
export function formatDueDate(dueDate: string, now: Date): string {
  const due = new Date(dueDate);
  return due.toLocaleDateString('en-US', {
    month: 'short',
    day: 'numeric',
    ...(due.getFullYear() === now.getFullYear() ? {} : { year: 'numeric' }),
  });
}
