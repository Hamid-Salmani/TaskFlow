/** "just now", "5 min ago", "3 h ago", "2 d ago", then a calendar date. */
export function formatRelativeTime(iso: string, now: Date): string {
  const then = new Date(iso);
  const seconds = Math.round((now.getTime() - then.getTime()) / 1000);

  if (seconds < 60) {
    return 'just now';
  }
  const minutes = Math.floor(seconds / 60);
  if (minutes < 60) {
    return `${minutes} min ago`;
  }
  const hours = Math.floor(minutes / 60);
  if (hours < 24) {
    return `${hours} h ago`;
  }
  const days = Math.floor(hours / 24);
  if (days < 7) {
    return `${days} d ago`;
  }
  return then.toLocaleDateString('en-US', { month: 'short', day: 'numeric', year: 'numeric' });
}

/** `<input type="date">` value (YYYY-MM-DD, UTC) of an ISO timestamp; empty string for no date. */
export function toDateInputValue(iso: string | null | undefined): string {
  return iso ? iso.slice(0, 10) : '';
}

/** ISO timestamp (midnight UTC) of a `<input type="date">` value; `null` for an empty field. */
export function fromDateInputValue(value: string): string | null {
  return value === '' ? null : `${value}T00:00:00Z`;
}
