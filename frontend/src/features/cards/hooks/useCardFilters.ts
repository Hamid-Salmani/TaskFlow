import { useCallback, useMemo } from 'react';
import { useSearchParams } from 'react-router-dom';
import { filtersFromSearchParams, filtersToUrlParams, type CardFilters } from '../filters';

/** The board filters live in the URL, so a filtered view can be shared and survives a reload. */
export function useCardFilters(): [CardFilters, (filters: CardFilters) => void] {
  const [searchParams, setSearchParams] = useSearchParams();
  const filters = useMemo(() => filtersFromSearchParams(searchParams), [searchParams]);

  const setFilters = useCallback(
    (next: CardFilters) =>
      setSearchParams((params) => filtersToUrlParams(next, params), { replace: true }),
    [setSearchParams],
  );

  return [filters, setFilters];
}
