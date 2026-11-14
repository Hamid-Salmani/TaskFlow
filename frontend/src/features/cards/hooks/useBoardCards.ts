import { useQuery } from '@tanstack/react-query';
import { boardKeys } from '@/features/workspaces/query-keys';
import { request } from '@/shared/api/request';
import type { CardDto, CardPage } from '@/shared/api/types';
import { filtersToSearchParams, type CardFilters } from '../filters';

const PAGE_SIZE = 200; // the API maximum

/** Loads every card of the board (all pages), so the Kanban can show complete columns. */
async function fetchAllCards(boardId: string, filters: CardFilters): Promise<CardDto[]> {
  const cards: CardDto[] = [];
  for (let page = 1; ; page += 1) {
    const result = await request<CardPage>({
      method: 'GET',
      url: `/boards/${boardId}/cards`,
      params: { ...filtersToSearchParams(filters), page, pageSize: PAGE_SIZE },
    });
    cards.push(...result.items);
    if (page >= result.totalPages) {
      return cards;
    }
  }
}

export function useBoardCards(boardId: string, filters: CardFilters) {
  return useQuery({
    queryKey: boardKeys.cards(boardId, filters),
    queryFn: () => fetchAllCards(boardId, filters),
  });
}
