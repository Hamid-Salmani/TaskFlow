import { ArrowLeft, Archive } from 'lucide-react';
import { useCallback } from 'react';
import { Link, useParams, useSearchParams } from 'react-router-dom';
import { CardModal } from '@/features/cards/components/CardModal';
import { FilterBar } from '@/features/cards/components/FilterBar';
import { EMPTY_FILTERS, hasActiveFilters } from '@/features/cards/filters';
import { useCardFilters } from '@/features/cards/hooks/useCardFilters';
import { useBoardCards } from '@/features/cards/hooks/useBoardCards';
import { ConnectionIndicator } from '@/features/realtime/ConnectionIndicator';
import { useBoardRealtime } from '@/features/realtime/useBoardRealtime';
import { can } from '@/features/workspaces/permissions';
import { toViewState } from '@/shared/lib/view-state';
import { Badge, EmptyState, QueryErrorState, Skeleton } from '@/shared/ui';
import { KanbanBoard } from '../components/KanbanBoard';
import { useBoard } from '../hooks/useBoard';

export function BoardPage() {
  const { boardId } = useParams();
  if (boardId === undefined) {
    return null; // the route always provides it; this narrows the type
  }
  return <BoardView boardId={boardId} />;
}

function BoardSkeleton() {
  return (
    <div className="flex gap-3 overflow-hidden px-4" aria-label="Loading board">
      {[0, 1, 2].map((index) => (
        <Skeleton key={index} className="h-64 w-72 shrink-0" />
      ))}
    </div>
  );
}

function BoardView({ boardId }: { boardId: string }) {
  const [searchParams, setSearchParams] = useSearchParams();
  const [filters, setFilters] = useCardFilters();
  const openCardId = searchParams.get('card');
  const board = useBoard(boardId);
  const cards = useBoardCards(boardId, filters);
  // The modal reads from the unfiltered list, so a card stays openable while a filter hides it.
  const allCards = useBoardCards(boardId, EMPTY_FILTERS);
  const realtime = useBoardRealtime(boardId);
  const boardView = toViewState(board);
  const cardsView = toViewState(cards);

  function openCard(cardId: string) {
    setSearchParams(
      (params) => {
        const next = new URLSearchParams(params);
        next.set('card', cardId);
        return next;
      },
      { replace: true },
    );
  }

  const closeCard = useCallback(() => {
    setSearchParams(
      (params) => {
        const next = new URLSearchParams(params);
        next.delete('card');
        return next;
      },
      { replace: true },
    );
  }, [setSearchParams]);

  if (boardView.status === 'error') {
    return (
      <div className="mx-auto w-full max-w-5xl px-4 py-8">
        <QueryErrorState
          error={boardView.error}
          onRetry={boardView.retry}
          notFoundMessage="This board does not exist or you are not a member of its workspace."
        />
        <p className="mt-4 text-center text-sm">
          <Link to="/workspaces" className="font-medium text-primary hover:underline">
            Back to workspaces
          </Link>
        </p>
      </div>
    );
  }

  if (boardView.status !== 'ready') {
    return (
      <div className="space-y-4 py-6">
        <Skeleton className="mx-4 h-8 w-56" />
        <BoardSkeleton />
      </div>
    );
  }

  const data = boardView.data;
  const readOnly = data.isArchived;

  return (
    <div className="flex h-[calc(100dvh-4rem)] flex-col">
      <header className="flex flex-wrap items-center gap-3 p-4">
        <Link
          to={`/workspaces/${data.workspaceId}`}
          className="inline-flex items-center gap-1 rounded text-sm text-muted hover:text-foreground focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring"
        >
          <ArrowLeft className="size-4" aria-hidden="true" />
          Workspace
        </Link>
        <h1 className="text-xl font-semibold">{data.name}</h1>
        {data.isArchived ? (
          <Badge>
            <Archive className="mr-1 size-3" aria-hidden="true" />
            Archived (read-only)
          </Badge>
        ) : null}
        <ConnectionIndicator status={realtime} />
      </header>

      <FilterBar
        filters={filters}
        onChange={setFilters}
        labels={data.labels}
        members={data.members}
      />

      {hasActiveFilters(filters) && cardsView.status === 'ready' && cardsView.data.length === 0 ? (
        <p role="status" className="px-4 pb-2 text-sm text-muted">
          No cards match these filters.
        </p>
      ) : null}

      <div className="min-h-0 flex-1">
        {cardsView.status === 'error' ? (
          <div className="mx-auto max-w-xl px-4">
            <QueryErrorState error={cardsView.error} onRetry={cardsView.retry} />
          </div>
        ) : cardsView.status === 'loading' ? (
          <BoardSkeleton />
        ) : data.columns.length === 0 && readOnly ? (
          <EmptyState title="No columns" description="This archived board has no columns." />
        ) : (
          <KanbanBoard
            boardId={boardId}
            columns={data.columns}
            cards={cardsView.status === 'ready' ? cardsView.data : []}
            labels={data.labels}
            members={data.members}
            readOnly={readOnly}
            canDeleteColumn={can(data.myRole, 'manageBoards')}
            onOpenCard={openCard}
          />
        )}
      </div>

      {openCardId !== null && allCards.data !== undefined ? (
        <CardModal
          boardId={boardId}
          card={allCards.data.find((card) => card.id === openCardId)}
          labels={data.labels}
          members={data.members}
          myRole={data.myRole}
          readOnly={readOnly}
          onClose={closeCard}
        />
      ) : null}
    </div>
  );
}
