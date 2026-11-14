import { cva, type VariantProps } from 'class-variance-authority';
import { cn } from '@/shared/lib/cn';

const avatarVariants = cva(
  'inline-flex shrink-0 select-none items-center justify-center rounded-full font-semibold text-white',
  {
    variants: {
      size: { xs: 'size-6 text-[10px]', sm: 'size-8 text-xs', md: 'size-10 text-sm' },
    },
    defaultVariants: { size: 'sm' },
  },
);

export function getInitials(name: string): string {
  const parts = name.trim().split(/\s+/).filter(Boolean);
  const first = parts[0]?.[0] ?? '?';
  const last = parts.length > 1 ? (parts[parts.length - 1]?.[0] ?? '') : '';
  return (first + last).toUpperCase();
}

/** Stable color per name so the same person always gets the same avatar. */
export function hueFromName(name: string): number {
  let hash = 0;
  for (const char of name) {
    hash = (hash * 31 + char.charCodeAt(0)) % 360;
  }
  return hash;
}

export interface AvatarProps extends VariantProps<typeof avatarVariants> {
  name: string;
  className?: string;
}

export function Avatar({ name, size, className }: AvatarProps) {
  return (
    <span
      role="img"
      aria-label={name}
      title={name}
      className={cn(avatarVariants({ size }), className)}
      style={{ backgroundColor: `hsl(${hueFromName(name)} 55% 38%)` }}
    >
      {getInitials(name)}
    </span>
  );
}

export interface AvatarStackProps {
  names: string[];
  max?: number;
  size?: AvatarProps['size'];
}

export function AvatarStack({ names, max = 3, size = 'xs' }: AvatarStackProps) {
  const visible = names.slice(0, max);
  const hidden = names.length - visible.length;

  return (
    <span className="inline-flex -space-x-1.5">
      {visible.map((name) => (
        <Avatar key={name} name={name} size={size} className="ring-2 ring-surface" />
      ))}
      {hidden > 0 ? (
        <span
          className={cn(
            avatarVariants({ size }),
            'bg-surface-muted text-foreground ring-2 ring-surface',
          )}
          aria-label={`${hidden} more`}
        >
          +{hidden}
        </span>
      ) : null}
    </span>
  );
}
