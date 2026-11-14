import { zodResolver } from '@hookform/resolvers/zod';
import { UserMinus, UserPlus } from 'lucide-react';
import { useState } from 'react';
import { useForm } from 'react-hook-form';
import { toApiError } from '@/shared/api/errors';
import type { WorkspaceMemberDto, WorkspaceRole } from '@/shared/api/types';
import { applyServerFieldErrors } from '@/shared/lib/form-errors';
import { toViewState } from '@/shared/lib/view-state';
import { selectUser, useSessionStore } from '@/shared/store/session-store';
import {
  Avatar,
  Button,
  ConfirmDialog,
  QueryErrorState,
  Select,
  Skeleton,
  TextField,
  toast,
} from '@/shared/ui';
import { useChangeMemberRole } from '../hooks/useChangeMemberRole';
import { useInviteMember } from '../hooks/useInviteMember';
import { useRemoveMember } from '../hooks/useRemoveMember';
import { useWorkspaceMembers } from '../hooks/useWorkspaceMembers';
import { assignableRoles, can, canChangeRole, canRemoveMember } from '../permissions';
import { inviteMemberSchema, type InviteMemberValues } from '../schemas';
import { RoleBadge } from './RoleBadge';

interface MembersSectionProps {
  workspaceId: string;
  role: WorkspaceRole;
  /** Called after the current user left the workspace. */
  onLeft: () => void;
}

function InviteForm({ workspaceId, role }: { workspaceId: string; role: WorkspaceRole }) {
  const invite = useInviteMember(workspaceId);
  const roles = assignableRoles(role);
  const {
    register,
    handleSubmit,
    reset,
    setError,
    formState: { errors },
  } = useForm<InviteMemberValues>({
    resolver: zodResolver(inviteMemberSchema),
    defaultValues: { email: '', role: 'Member' },
  });

  const submit = handleSubmit((values) =>
    invite.mutate(values, {
      onSuccess: (member) => {
        toast.success('Member added', `${member.displayName} can now access this workspace.`);
        reset({ email: '', role: values.role });
      },
      onError: (error) => {
        if (!applyServerFieldErrors(error, ['email', 'role'], setError)) {
          // user.not_found / member.already_exists are about the email the admin typed.
          setError('email', { message: error.message });
        }
      },
    }),
  );

  return (
    <form onSubmit={submit} noValidate className="mb-4 flex flex-wrap items-start gap-2">
      <TextField
        label="Invite by email"
        type="email"
        autoComplete="off"
        containerClassName="min-w-56 flex-1"
        hint="They need to have an account already."
        error={errors.email?.message}
        {...register('email')}
      />
      <div className="space-y-1.5">
        <label htmlFor="invite-role" className="block text-sm font-medium">
          Role
        </label>
        <Select id="invite-role" {...register('role')}>
          {roles.map((option) => (
            <option key={option} value={option}>
              {option}
            </option>
          ))}
        </Select>
      </div>
      <Button type="submit" className="mt-[1.625rem]" loading={invite.isPending}>
        <UserPlus className="size-4" aria-hidden="true" />
        Invite
      </Button>
    </form>
  );
}

export function MembersSection({ workspaceId, role, onLeft }: MembersSectionProps) {
  const me = useSessionStore(selectUser);
  const members = useWorkspaceMembers(workspaceId);
  const changeRole = useChangeMemberRole(workspaceId);
  const removeMember = useRemoveMember(workspaceId);
  const [removing, setRemoving] = useState<WorkspaceMemberDto | null>(null);
  const view = toViewState(members);
  const removingSelf = removing !== null && removing.userId === me?.id;

  function confirmRemove() {
    if (removing === null) {
      return;
    }
    removeMember.mutate(removing.userId, {
      onSuccess: () => {
        toast.success(removingSelf ? 'You left the workspace' : 'Member removed');
        setRemoving(null);
        if (removingSelf) {
          onLeft();
        }
      },
      onError: (error) => toast.error('Could not remove the member', toApiError(error).message),
    });
  }

  return (
    <section aria-labelledby="members-heading">
      <h2 id="members-heading" className="mb-3 text-lg font-semibold">
        Members
      </h2>
      {can(role, 'manageMembers') ? <InviteForm workspaceId={workspaceId} role={role} /> : null}

      {view.status === 'loading' || view.status === 'empty' ? (
        <Skeleton className="h-24" aria-label="Loading members" />
      ) : null}
      {view.status === 'error' ? <QueryErrorState error={view.error} onRetry={view.retry} /> : null}
      {view.status === 'ready' ? (
        <ul className="divide-y divide-border rounded-lg border border-border bg-surface">
          {view.data.map((member) => {
            const isSelf = member.userId === me?.id;
            const roleChoices = canChangeRole(role, member) ? assignableRoles(role) : [];
            return (
              <li key={member.userId} className="flex flex-wrap items-center gap-3 px-4 py-3">
                <Avatar name={member.displayName} />
                <div className="min-w-0 flex-1">
                  <p className="truncate text-sm font-medium">
                    {member.displayName}
                    {isSelf ? <span className="ml-1 text-muted">(you)</span> : null}
                  </p>
                  <p className="truncate text-xs text-muted">
                    @{member.handle} · {member.email}
                  </p>
                </div>
                {roleChoices.length > 0 ? (
                  <Select
                    aria-label={`Role of ${member.displayName}`}
                    className="w-32"
                    value={member.role}
                    disabled={changeRole.isPending}
                    onChange={(event) => {
                      const next = roleChoices.find((choice) => choice === event.target.value);
                      if (next === undefined) {
                        return;
                      }
                      changeRole.mutate(
                        { userId: member.userId, role: next },
                        {
                          onError: (error) =>
                            toast.error('Could not change the role', error.message),
                        },
                      );
                    }}
                  >
                    {roleChoices.map((choice) => (
                      <option key={choice} value={choice}>
                        {choice}
                      </option>
                    ))}
                  </Select>
                ) : (
                  <RoleBadge role={member.role} />
                )}
                {canRemoveMember(role, member, isSelf) ? (
                  <Button
                    variant="ghost"
                    size="sm"
                    onClick={() => setRemoving(member)}
                    aria-label={isSelf ? 'Leave workspace' : `Remove ${member.displayName}`}
                  >
                    <UserMinus className="size-4" aria-hidden="true" />
                    {isSelf ? 'Leave' : 'Remove'}
                  </Button>
                ) : null}
              </li>
            );
          })}
        </ul>
      ) : null}

      <ConfirmDialog
        open={removing !== null}
        title={removingSelf ? 'Leave workspace' : 'Remove member'}
        description={
          removingSelf
            ? 'You will lose access to every board in this workspace.'
            : `${removing?.displayName ?? 'This member'} will lose access to every board in this workspace.`
        }
        confirmLabel={removingSelf ? 'Leave' : 'Remove'}
        destructive
        loading={removeMember.isPending}
        onCancel={() => setRemoving(null)}
        onConfirm={confirmRemove}
      />
    </section>
  );
}
