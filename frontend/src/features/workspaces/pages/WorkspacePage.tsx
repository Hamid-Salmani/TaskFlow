import { Pencil, Trash2 } from 'lucide-react';
import { useState } from 'react';
import { Link, useNavigate, useParams } from 'react-router-dom';
import { BoardsSection } from '@/features/boards/components/BoardsSection';
import { toViewState } from '@/shared/lib/view-state';
import { Button, ConfirmDialog, QueryErrorState, Skeleton, toast } from '@/shared/ui';
import { MembersSection } from '../components/MembersSection';
import { NameDescriptionModal } from '../components/NameDescriptionModal';
import { RoleBadge } from '../components/RoleBadge';
import { useDeleteWorkspace } from '../hooks/useDeleteWorkspace';
import { useUpdateWorkspace } from '../hooks/useUpdateWorkspace';
import { useWorkspace } from '../hooks/useWorkspace';
import { can } from '../permissions';

export function WorkspacePage() {
  const { workspaceId } = useParams();
  if (workspaceId === undefined) {
    return null; // the route always provides it; this narrows the type
  }
  return <WorkspaceView workspaceId={workspaceId} />;
}

function WorkspaceView({ workspaceId }: { workspaceId: string }) {
  const navigate = useNavigate();
  const [editing, setEditing] = useState(false);
  const [deleting, setDeleting] = useState(false);
  const workspace = useWorkspace(workspaceId);
  const update = useUpdateWorkspace(workspaceId);
  const remove = useDeleteWorkspace(workspaceId);
  const view = toViewState(workspace);

  if (view.status === 'loading' || view.status === 'empty') {
    return (
      <div className="mx-auto w-full max-w-5xl space-y-4 px-4 py-8" aria-label="Loading workspace">
        <Skeleton className="h-9 w-64" />
        <Skeleton className="h-40" />
      </div>
    );
  }

  if (view.status === 'error') {
    return (
      <div className="mx-auto w-full max-w-5xl px-4 py-8">
        <QueryErrorState
          error={view.error}
          onRetry={view.retry}
          notFoundMessage="This workspace does not exist or you are not a member of it."
        />
        <p className="mt-4 text-center text-sm">
          <Link to="/workspaces" className="font-medium text-primary hover:underline">
            Back to workspaces
          </Link>
        </p>
      </div>
    );
  }

  const { data } = view;

  return (
    <div className="mx-auto w-full max-w-5xl space-y-8 px-4 py-8">
      <header className="flex flex-wrap items-start justify-between gap-4">
        <div className="min-w-0">
          <nav aria-label="Breadcrumb" className="mb-1 text-sm text-muted">
            <Link to="/workspaces" className="hover:underline">
              Workspaces
            </Link>
          </nav>
          <div className="flex flex-wrap items-center gap-3">
            <h1 className="truncate text-2xl font-bold">{data.name}</h1>
            <RoleBadge role={data.myRole} />
          </div>
          {data.description ? <p className="mt-1 text-sm text-muted">{data.description}</p> : null}
        </div>
        <div className="flex gap-2">
          {can(data.myRole, 'updateWorkspace') ? (
            <Button variant="outline" size="sm" onClick={() => setEditing(true)}>
              <Pencil className="size-4" aria-hidden="true" />
              Edit
            </Button>
          ) : null}
          {can(data.myRole, 'deleteWorkspace') ? (
            <Button variant="outline" size="sm" onClick={() => setDeleting(true)}>
              <Trash2 className="size-4" aria-hidden="true" />
              Delete
            </Button>
          ) : null}
        </div>
      </header>

      <BoardsSection workspaceId={workspaceId} role={data.myRole} />
      <MembersSection
        workspaceId={workspaceId}
        role={data.myRole}
        onLeft={() => void navigate('/workspaces', { replace: true })}
      />

      <NameDescriptionModal
        open={editing}
        onClose={() => setEditing(false)}
        title="Edit workspace"
        submitLabel="Save"
        initialValues={{ name: data.name, description: data.description ?? '' }}
        onSubmit={(values) =>
          update.mutateAsync({ name: values.name, description: values.description || null })
        }
      />
      <ConfirmDialog
        open={deleting}
        title="Delete workspace"
        description={`"${data.name}" with all of its boards and cards will be deleted for every member.`}
        confirmLabel="Delete workspace"
        destructive
        loading={remove.isPending}
        onCancel={() => setDeleting(false)}
        onConfirm={() =>
          remove.mutate(undefined, {
            onSuccess: () => {
              toast.success('Workspace deleted');
              void navigate('/workspaces', { replace: true });
            },
            onError: (error) => toast.error('Could not delete the workspace', error.message),
          })
        }
      />
    </div>
  );
}
