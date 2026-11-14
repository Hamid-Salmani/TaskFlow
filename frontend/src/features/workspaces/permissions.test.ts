import type { WorkspaceMemberDto, WorkspaceRole } from '@/shared/api/types';
import { assignableRoles, can, canChangeRole, canRemoveMember, hasRole } from './permissions';

function member(role: WorkspaceRole): WorkspaceMemberDto {
  return {
    userId: 'u',
    email: 'u@test.dev',
    displayName: 'U',
    handle: 'u',
    role,
    joinedAt: '2026-01-01T00:00:00Z',
  };
}

describe('permissions', () => {
  it.each([
    ['Member', 'createBoard', true],
    ['Member', 'manageBoards', false],
    ['Member', 'manageMembers', false],
    ['Member', 'updateWorkspace', false],
    ['Member', 'deleteWorkspace', false],
    ['Admin', 'createBoard', true],
    ['Admin', 'manageBoards', true],
    ['Admin', 'manageMembers', true],
    ['Admin', 'updateWorkspace', true],
    ['Admin', 'deleteWorkspace', false],
    ['Owner', 'deleteWorkspace', true],
    ['Owner', 'manageMembers', true],
  ] as const)('%s can %s: %s', (role, permission, expected) => {
    expect(can(role, permission)).toBe(expected);
  });

  it('grants nothing while the role is unknown', () => {
    expect(can(undefined, 'createBoard')).toBe(false);
    expect(hasRole(undefined, 'Member')).toBe(false);
  });

  it('orders roles by privilege', () => {
    expect(hasRole('Owner', 'Admin')).toBe(true);
    expect(hasRole('Admin', 'Owner')).toBe(false);
    expect(hasRole('Member', 'Member')).toBe(true);
  });

  it('lets only the owner hand out the admin role', () => {
    expect(assignableRoles('Owner')).toEqual(['Member', 'Admin']);
    expect(assignableRoles('Admin')).toEqual(['Member']);
    expect(assignableRoles('Member')).toEqual([]);
  });

  describe('canChangeRole', () => {
    it('never applies to the owner', () => {
      expect(canChangeRole('Owner', member('Owner'))).toBe(false);
    });

    it('lets admins manage plain members only', () => {
      expect(canChangeRole('Admin', member('Member'))).toBe(true);
      expect(canChangeRole('Admin', member('Admin'))).toBe(false);
      expect(canChangeRole('Owner', member('Admin'))).toBe(true);
    });

    it('is not available to members', () => {
      expect(canChangeRole('Member', member('Member'))).toBe(false);
    });
  });

  describe('canRemoveMember', () => {
    it('lets everyone but the owner leave', () => {
      expect(canRemoveMember('Member', member('Member'), true)).toBe(true);
      expect(canRemoveMember('Admin', member('Admin'), true)).toBe(true);
      expect(canRemoveMember('Owner', member('Owner'), true)).toBe(false);
    });

    it('lets admins remove members but not other admins; the owner can remove admins', () => {
      expect(canRemoveMember('Admin', member('Member'), false)).toBe(true);
      expect(canRemoveMember('Admin', member('Admin'), false)).toBe(false);
      expect(canRemoveMember('Owner', member('Admin'), false)).toBe(true);
    });

    it('never lets members remove others, and nobody removes the owner', () => {
      expect(canRemoveMember('Member', member('Member'), false)).toBe(false);
      expect(canRemoveMember('Admin', member('Owner'), false)).toBe(false);
    });
  });
});
