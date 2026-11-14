import type { WorkspaceRole } from '@/shared/api/types';
import { Badge } from '@/shared/ui';

const VARIANTS = { Owner: 'primary', Admin: 'success', Member: 'neutral' } as const;

export function RoleBadge({ role }: { role: WorkspaceRole }) {
  return <Badge variant={VARIANTS[role]}>{role}</Badge>;
}
