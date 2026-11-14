import {
  EMPTY_FILTERS,
  filtersFromSearchParams,
  filtersToSearchParams,
  filtersToUrlParams,
  hasActiveFilters,
} from './filters';

describe('filters <-> URL', () => {
  it('reads every filter from the query string', () => {
    const filters = filtersFromSearchParams(
      new URLSearchParams('q=bug&label=l1&assignee=u1&due=with'),
    );

    expect(filters).toEqual({ q: 'bug', labelId: 'l1', assigneeId: 'u1', due: 'with' });
  });

  it('falls back to "no filter" for missing or invalid values', () => {
    expect(filtersFromSearchParams(new URLSearchParams(''))).toEqual(EMPTY_FILTERS);
    expect(filtersFromSearchParams(new URLSearchParams('due=sometimes&label='))).toEqual(
      EMPTY_FILTERS,
    );
  });

  it('writes only active filters and keeps unrelated params like the open card', () => {
    const next = filtersToUrlParams(
      { q: 'bug', labelId: null, assigneeId: 'u1', due: 'without' },
      new URLSearchParams('card=c1&label=old'),
    );

    expect(Object.fromEntries(next)).toEqual({
      card: 'c1',
      q: 'bug',
      assignee: 'u1',
      due: 'without',
    });
  });

  it('round-trips', () => {
    const filters = { q: 'a b', labelId: 'l1', assigneeId: null, due: 'with' } as const;

    expect(filtersFromSearchParams(filtersToUrlParams(filters, new URLSearchParams()))).toEqual(
      filters,
    );
  });
});

describe('filtersToSearchParams / hasActiveFilters', () => {
  it('maps the due filter to hasDueDate and trims text', () => {
    expect(
      filtersToSearchParams({ q: '  x ', labelId: 'l', assigneeId: 'u', due: 'without' }),
    ).toEqual({
      q: 'x',
      labelId: 'l',
      assigneeId: 'u',
      hasDueDate: false,
    });
    expect(filtersToSearchParams(EMPTY_FILTERS)).toEqual({});
  });

  it('knows when nothing is filtered', () => {
    expect(hasActiveFilters(EMPTY_FILTERS)).toBe(false);
    expect(hasActiveFilters({ ...EMPTY_FILTERS, q: '   ' })).toBe(false);
    expect(hasActiveFilters({ ...EMPTY_FILTERS, due: 'with' })).toBe(true);
  });
});
