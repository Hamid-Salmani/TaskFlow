import { Archive, ArchiveRestore, MoreHorizontal, Pencil, Trash2 } from 'lucide-react';
import { Link } from 'react-router-dom';
import type { BoardSummaryDto } from '@/shared/api/types';
import { Badge, Dropdown, type DropdownItem } from '@/shared/ui';

interface BoardCardProps {
  board: BoardSummaryDto;
  /** Rename / archive / delete are offered to admins only. */
  canManage: boolean;
  onRename: (board: BoardSummaryDto) => void;
  onToggleArchive: (board: BoardSummaryDto) => void;
  onDelete: (board: BoardSummaryDto) => void;
}

export function BoardCard({
  board,
  canManage,
  onRename,
  onToggleArchive,
  onDelete,
}: BoardCardProps) {
  const items: DropdownItem[] = [
    {
      id: 'rename',
      label: 'Rename',
      icon: <Pencil className="size-4" />,
      onSelect: () => onRename(board),
    },
    {
      id: 'archive',
      label: board.isArchived ? 'Restore' : 'Archive',
      icon: board.isArchived ? (
        <ArchiveRestore className="size-4" />
      ) : (
        <Archive className="size-4" />
      ),
      onSelect: () => onToggleArchive(board),
    },
    {
      id: 'delete',
      label: 'Delete',
      icon: <Trash2 className="size-4" />,
      destructive: true,
      onSelect: () => onDelete(board),
    },
  ];

  return (
    <li className="relative">
      <Link
        to={`/boards/${board.id}`}
        className="block h-full rounded-lg border border-border bg-surface p-4 pr-12 transition-shadow hover:shadow-card"
      >
        <h3 className="truncate text-base font-semibold">{board.name}</h3>
        {board.description ? (
          <p className="mt-1 line-clamp-2 text-sm text-muted">{board.description}</p>
        ) : null}
        {board.isArchived ? (
          <Badge variant="outline" className="mt-3">
            Archived
          </Badge>
        ) : null}
      </Link>
      {canManage ? (
        <div className="absolute right-2 top-2">
          <Dropdown
            triggerLabel={`Actions for ${board.name}`}
            trigger={<MoreHorizontal className="size-4" />}
            triggerClassName="size-8 justify-center"
            align="end"
            items={items}
          />
        </div>
      ) : null}
    </li>
  );
}
