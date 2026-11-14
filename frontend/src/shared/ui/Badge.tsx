import { cva, type VariantProps } from 'class-variance-authority';
import type { HTMLAttributes } from 'react';
import { cn } from '@/shared/lib/cn';

const badgeVariants = cva(
  'inline-flex items-center gap-1 rounded-full px-2 py-0.5 text-xs font-medium',
  {
    variants: {
      variant: {
        neutral: 'bg-surface-muted text-foreground',
        primary: 'bg-primary/15 text-primary',
        success: 'bg-success/15 text-success',
        warning: 'bg-warning/15 text-warning',
        danger: 'bg-danger/15 text-danger',
        outline: 'border border-border text-muted',
      },
    },
    defaultVariants: { variant: 'neutral' },
  },
);

export interface BadgeProps
  extends HTMLAttributes<HTMLSpanElement>, VariantProps<typeof badgeVariants> {}

export function Badge({ className, variant, ...props }: BadgeProps) {
  return <span className={cn(badgeVariants({ variant }), className)} {...props} />;
}

/** Black or white, whichever is more readable on the given #rrggbb background (WCAG relative luminance). */
export function readableTextColor(hex: string): '#000000' | '#ffffff' {
  const match = /^#?([0-9a-f]{6})$/i.exec(hex);
  if (!match?.[1]) {
    return '#ffffff';
  }

  const value = parseInt(match[1], 16);
  const channels = [(value >> 16) & 255, (value >> 8) & 255, value & 255].map((channel) => {
    const normalized = channel / 255;
    return normalized <= 0.03928 ? normalized / 12.92 : ((normalized + 0.055) / 1.055) ** 2.4;
  });
  const [r = 0, g = 0, b = 0] = channels;
  const luminance = 0.2126 * r + 0.7152 * g + 0.0722 * b;
  return luminance > 0.4 ? '#000000' : '#ffffff';
}

export interface ColorBadgeProps extends HTMLAttributes<HTMLSpanElement> {
  color: string;
}

/** Badge with a user-defined color (board labels). */
export function ColorBadge({ color, className, style, ...props }: ColorBadgeProps) {
  return (
    <span
      className={cn(
        'inline-flex items-center rounded-md px-2 py-0.5 text-xs font-medium',
        className,
      )}
      style={{ backgroundColor: color, color: readableTextColor(color), ...style }}
      {...props}
    />
  );
}
