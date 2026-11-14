export type DueFilter = 'any' | 'with' | 'without';

/** What the filter bar controls. Empty strings / `null` / `'any'` mean "no filter". */
export interface CardFilters {
  q: string;
  labelId: string | null;
  assigneeId: string | null;
  due: DueFilter;
}

export const EMPTY_FILTERS: CardFilters = { q: '', labelId: null, assigneeId: null, due: 'any' };

export function hasActiveFilters(filters: CardFilters): boolean {
  return (
    filters.q.trim() !== '' ||
    filters.labelId !== null ||
    filters.assigneeId !== null ||
    filters.due !== 'any'
  );
}

export interface CardSearchParams {
  q?: string;
  labelId?: string;
  assigneeId?: string;
  hasDueDate?: boolean;
}

/** Query-string parameters of `GET /boards/{id}/cards` for the given filters (unset filters are omitted). */
export function filtersToSearchParams(filters: CardFilters): CardSearchParams {
  const params: CardSearchParams = {};
  const q = filters.q.trim();
  if (q !== '') {
    params.q = q;
  }
  if (filters.labelId !== null) {
    params.labelId = filters.labelId;
  }
  if (filters.assigneeId !== null) {
    params.assigneeId = filters.assigneeId;
  }
  if (filters.due !== 'any') {
    params.hasDueDate = filters.due === 'with';
  }
  return params;
}

const DUE_VALUES: readonly DueFilter[] = ['any', 'with', 'without'];

function isDueFilter(value: string | null): value is DueFilter {
  return DUE_VALUES.some((due) => due === value);
}

/** Reads the filters from the URL (`q`, `label`, `assignee`, `due`). Unknown or malformed values are ignored. */
export function filtersFromSearchParams(params: URLSearchParams): CardFilters {
  const due = params.get('due');
  return {
    q: params.get('q') ?? '',
    labelId: params.get('label') || null,
    assigneeId: params.get('assignee') || null,
    due: isDueFilter(due) ? due : 'any',
  };
}

/** Writes the filters into a copy of `base`, leaving unrelated params (like `card`) untouched. */
export function filtersToUrlParams(filters: CardFilters, base: URLSearchParams): URLSearchParams {
  const next = new URLSearchParams(base);
  const entries: Array<[string, string | null]> = [
    ['q', filters.q.trim() === '' ? null : filters.q],
    ['label', filters.labelId],
    ['assignee', filters.assigneeId],
    ['due', filters.due === 'any' ? null : filters.due],
  ];
  for (const [key, value] of entries) {
    if (value === null) {
      next.delete(key);
    } else {
      next.set(key, value);
    }
  }
  return next;
}
