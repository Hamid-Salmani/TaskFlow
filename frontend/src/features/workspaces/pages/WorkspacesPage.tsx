import { Layers, Plus, Users } from 'lucide-react';
import { useState } from 'react';
import { Link, useNavigate } from 'react-router-dom';
import type { WorkspaceDto } from '@/shared/api/types';
import { toViewState } from '@/shared/lib/view-state';
import { Button, EmptyState, QueryErrorState, Skeleton } from '@/shared/ui';
import { NameDescriptionModal } from '../components/NameDescriptionModal';
import { RoleBadge } from '../components/RoleBadge';
import { useCreateWorkspace } from '../hooks/useCreateWorkspace';
import { useWorkspaces } from '../hooks/useWorkspaces';

function WorkspaceCard({ workspace }: { workspace: WorkspaceDto }) {
  return (
    <li>
      <Link
        to={`/workspaces/${workspace.id}`}
        className="block h-full rounded-lg border border-border bg-surface p-4 transition-shadow hover:shadow-card"
      >
        <div className="flex items-start justify-between gap-2">
          <h2 className="truncate text-base font-semibold">{workspace.name}</h2>
          <RoleBadge role={workspace.myRole} />
        </div>
        {workspace.description ? (
          <p className="mt-1 line-clamp-2 text-sm text-muted">{workspace.description}</p>
        ) : null}
        <p className="mt-4 flex items-center gap-4 text-xs text-muted">
          <span className="inline-flex items-center gap-1">
            <Users className="size-3.5" aria-hidden="true" />
            {workspace.memberCount} {workspace.memberCount === 1 ? 'member' : 'members'}
          </span>
          <span className="inline-flex items-center gap-1">
            <Layers className="size-3.5" aria-hidden="true" />
            {workspace.boardCount} {workspace.boardCount === 1 ? 'board' : 'boards'}
          </span>
        </p>
      </Link>
    </li>
  );
}

export function WorkspacesPage() {
  const navigate = useNavigate();
  const [creating, setCreating] = useState(false);
  const workspaces = useWorkspaces();
  const create = useCreateWorkspace();
  const view = toViewState(workspaces, (data) => data.length === 0);

  const newWorkspaceButton = (
    <Button onClick={() => setCreating(true)}>
      <Plus className="size-4" aria-hidden="true" />
      New workspace
    </Button>
  );

  return (
    <div className="mx-auto w-full max-w-5xl px-4 py-8">
      <div className="mb-6 flex items-center justify-between gap-4">
        <h1 className="text-2xl font-bold">Workspaces</h1>
        {view.status === 'ready' ? newWorkspaceButton : null}
      </div>

      {view.status === 'loading' ? (
        <ul className="grid gap-4 sm:grid-cols-2 lg:grid-cols-3" aria-label="Loading workspaces">
          {[0, 1, 2].map((key) => (
            <li key={key}>
              <Skeleton className="h-28" />
            </li>
          ))}
        </ul>
      ) : null}
      {view.status === 'error' ? <QueryErrorState error={view.error} onRetry={view.retry} /> : null}
      {view.status === 'empty' ? (
        <EmptyState
          icon={<Layers className="size-8" />}
          title="Create your first workspace"
          description="Workspaces group boards and the people working on them."
          action={newWorkspaceButton}
        />
      ) : null}
      {view.status === 'ready' ? (
        <ul className="grid gap-4 sm:grid-cols-2 lg:grid-cols-3">
          {view.data.map((workspace) => (
            <WorkspaceCard key={workspace.id} workspace={workspace} />
          ))}
        </ul>
      ) : null}

      <NameDescriptionModal
        open={creating}
        onClose={() => setCreating(false)}
        title="New workspace"
        submitLabel="Create workspace"
        onSubmit={async (values) => {
          const workspace = await create.mutateAsync({
            name: values.name,
            description: values.description || null,
          });
          void navigate(`/workspaces/${workspace.id}`);
        }}
      />
    </div>
  );
}
