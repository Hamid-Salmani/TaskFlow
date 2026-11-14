import type { WorkspaceMemberDto, WorkspaceRole } from '@/shared/api/types';

const RANK: Record<WorkspaceRole, number> = { Member: 1, Admin: 2, Owner: 3 };

/** What the UI may offer. The API enforces the same rules (see NOTES.md); this only decides what to show. */
const REQUIRED_ROLE = {
  createBoard: 'Member',
  manageBoards: 'Admin',
  updateWorkspace: 'Admin',
  manageMembers: 'Admin',
  deleteWorkspace: 'Owner',
} as const satisfies Record<string, WorkspaceRole>;

export type Permission = keyof typeof REQUIRED_ROLE;

export function hasRole(role: WorkspaceRole | undefined, minimum: WorkspaceRole): boolean {
  return role !== undefined && RANK[role] >= RANK[minimum];
}

export function can(role: WorkspaceRole | undefined, permission: Permission): boolean {
  return hasRole(role, REQUIRED_ROLE[permission]);
}

/** Roles the actor may hand out when inviting or changing a member. Only the owner can create admins. */
export function assignableRoles(actorRole: WorkspaceRole | undefined): WorkspaceRole[] {
  if (!can(actorRole, 'manageMembers')) {
    return [];
  }
  return actorRole === 'Owner' ? ['Member', 'Admin'] : ['Member'];
}

export function canChangeRole(
  actorRole: WorkspaceRole | undefined,
  target: WorkspaceMemberDto,
): boolean {
  if (!can(actorRole, 'manageMembers') || target.role === 'Owner') {
    return false;
  }
  // Touching the admin role (granting or revoking) is reserved for the owner.
  return target.role !== 'Admin' || actorRole === 'Owner';
}

export function canRemoveMember(
  actorRole: WorkspaceRole | undefined,
  target: WorkspaceMemberDto,
  isSelf: boolean,
): boolean {
  if (target.role === 'Owner') {
    return false;
  }
  if (isSelf) {
    return true; // leaving is always allowed for non-owners
  }
  return can(actorRole, 'manageMembers') && (target.role !== 'Admin' || actorRole === 'Owner');
}
