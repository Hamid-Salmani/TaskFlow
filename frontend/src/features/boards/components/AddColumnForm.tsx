import { Plus } from 'lucide-react';
import { useState } from 'react';
import { Button } from '@/shared/ui';
import { useCreateColumn } from '../hooks/useCreateColumn';
import { InlineTitleForm } from './InlineTitleForm';

export function AddColumnForm({ boardId }: { boardId: string }) {
  const [open, setOpen] = useState(false);
  const createColumn = useCreateColumn(boardId);

  return (
    <div className="w-[85vw] shrink-0 snap-start sm:w-72">
      {open ? (
        <div className="rounded-lg bg-surface-muted/60 p-2">
          <InlineTitleForm
            label="Column title"
            submitLabel="Add column"
            maxLength={100}
            keepOpen
            onCancel={() => setOpen(false)}
            onSubmit={(title) => createColumn.mutateAsync(title)}
          />
        </div>
      ) : (
        <Button variant="outline" className="w-full justify-start" onClick={() => setOpen(true)}>
          <Plus className="size-4" aria-hidden="true" />
          Add a column
        </Button>
      )}
    </div>
  );
}
