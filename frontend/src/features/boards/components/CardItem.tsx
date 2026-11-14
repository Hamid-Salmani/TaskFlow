import { useSortable } from '@dnd-kit/sortable';
import { CSS } from '@dnd-kit/utilities';
import { CalendarDays, MessageSquare } from 'lucide-react';
import { memo, type KeyboardEvent } from 'react';
import { dueStatus, formatDueDate } from '@/features/cards/lib/due-date';
import type { CardDto, LabelDto } from '@/shared/api/types';
import { cn } from '@/shared/lib/cn';
import { AvatarStack, ColorBadge } from '@/shared/ui';

interface CardBodyProps {
  card: CardDto;
  labels: LabelDto[];
  assigneeNames: string[];
}

const DUE_CLASSES = {
  overdue: 'bg-danger/15 text-danger',
  soon: 'bg-warning/15 text-warning',
  later: 'bg-surface-muted text-muted',
} as const;

/** The visual content of a card, shared by the board and the drag overlay. */
export function CardBody({ card, labels, assigneeNames }: CardBodyProps) {
  const now = new Date();
  const status = card.dueDate ? dueStatus(card.dueDate, now) : null;

  return (
    <>
      {labels.length > 0 ? (
        <ul className="mb-2 flex flex-wrap gap-1" aria-label="Labels">
          {labels.map((label) => (
            <li key={label.id}>
              <ColorBadge color={label.color}>{label.name}</ColorBadge>
            </li>
          ))}
        </ul>
      ) : null}
      <p className="break-words text-sm font-medium">{card.title}</p>
      {card.dueDate || card.commentCount > 0 || assigneeNames.length > 0 ? (
        <div className="mt-2 flex items-center gap-2 text-xs">
          {card.dueDate && status ? (
            <span
              className={cn(
                'inline-flex items-center gap-1 rounded px-1.5 py-0.5',
                DUE_CLASSES[status],
              )}
              title={status === 'overdue' ? 'Overdue' : 'Due date'}
            >
              <CalendarDays className="size-3" aria-hidden="true" />
              <span className="sr-only">{status === 'overdue' ? 'Overdue: ' : 'Due '}</span>
              {formatDueDate(card.dueDate, now)}
            </span>
          ) : null}
          {card.commentCount > 0 ? (
            <span className="inline-flex items-center gap-1 text-muted">
              <MessageSquare className="size-3" aria-hidden="true" />
              <span className="sr-only">Comments: </span>
              {card.commentCount}
            </span>
          ) : null}
          {assigneeNames.length > 0 ? (
            <span className="ml-auto">
              <AvatarStack names={assigneeNames} />
            </span>
          ) : null}
        </div>
      ) : null}
    </>
  );
}

interface CardItemProps extends CardBodyProps {
  readOnly: boolean;
  onOpen: (cardId: string) => void;
}

/**
 * A draggable card. Pointer: drag anywhere. Keyboard: focus the card, Space picks it up, arrow keys move it,
 * Space or Enter drops it, Escape cancels. Enter on a card that is not being dragged opens it.
 */
export const CardItem = memo(function CardItem({
  card,
  labels,
  assigneeNames,
  readOnly,
  onOpen,
}: CardItemProps) {
  const { attributes, listeners, setNodeRef, transform, transition, isDragging } = useSortable({
    id: card.id,
    data: { type: 'card', columnId: card.columnId },
    disabled: readOnly,
  });

  function handleKeyDown(event: KeyboardEvent<HTMLDivElement>) {
    listeners?.onKeyDown?.(event);
    if (event.key === 'Enter' && !event.defaultPrevented) {
      onOpen(card.id);
    }
  }

  return (
    <li>
      <div
        ref={setNodeRef}
        {...attributes}
        {...listeners}
        role="button"
        tabIndex={0}
        aria-label={`${card.title}. Press Enter to open${readOnly ? '' : ', Space to move'}.`}
        onKeyDown={handleKeyDown}
        onClick={() => onOpen(card.id)}
        style={{ transform: CSS.Transform.toString(transform), transition }}
        className={cn(
          'cursor-pointer touch-manipulation rounded-md border border-border bg-surface p-3 shadow-sm transition-colors hover:border-primary/50',
          isDragging && 'opacity-40',
        )}
      >
        <CardBody card={card} labels={labels} assigneeNames={assigneeNames} />
      </div>
    </li>
  );
});
