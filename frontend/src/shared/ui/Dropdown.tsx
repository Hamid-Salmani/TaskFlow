import {
  useCallback,
  useEffect,
  useId,
  useRef,
  useState,
  type KeyboardEvent as ReactKeyboardEvent,
  type ReactNode,
} from 'react';
import { cn } from '@/shared/lib/cn';

export interface DropdownItem {
  id: string;
  label: ReactNode;
  onSelect: () => void;
  icon?: ReactNode;
  destructive?: boolean;
  disabled?: boolean;
}

export interface DropdownProps {
  /** Accessible name of the trigger button (needed when the trigger is only an icon). */
  triggerLabel: string;
  trigger: ReactNode;
  items: DropdownItem[];
  align?: 'start' | 'end';
  header?: ReactNode;
  emptyMessage?: ReactNode;
  triggerClassName?: string;
  menuClassName?: string;
  onOpenChange?: (open: boolean) => void;
}

/**
 * Menu button following the WAI-ARIA menu pattern: ArrowUp/ArrowDown/Home/End move focus (roving tabindex),
 * Enter/Space activate, Escape closes and returns focus to the trigger, clicking outside closes.
 */
export function Dropdown({
  triggerLabel,
  trigger,
  items,
  align = 'end',
  header,
  emptyMessage,
  triggerClassName,
  menuClassName,
  onOpenChange,
}: DropdownProps) {
  const [open, setOpenState] = useState(false);
  const [activeIndex, setActiveIndex] = useState(0);
  const containerRef = useRef<HTMLDivElement>(null);
  const triggerRef = useRef<HTMLButtonElement>(null);
  const itemRefs = useRef<Array<HTMLButtonElement | null>>([]);
  const menuId = useId();
  const triggerId = useId();

  const setOpen = useCallback(
    (next: boolean) => {
      setOpenState(next);
      onOpenChange?.(next);
    },
    [onOpenChange],
  );

  const enabledIndexes = items.flatMap((item, index) => (item.disabled ? [] : [index]));

  const focusItem = useCallback((index: number) => {
    setActiveIndex(index);
    itemRefs.current[index]?.focus();
  }, []);

  // Move focus into the menu when it opens.
  useEffect(() => {
    if (open) {
      const first = items.findIndex((item) => !item.disabled);
      if (first >= 0) {
        focusItem(first);
      }
    }
    // Only react to opening, not to item changes while it is open.
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [open]);

  useEffect(() => {
    if (!open) {
      return undefined;
    }

    const handlePointerDown = (event: MouseEvent) => {
      if (
        containerRef.current &&
        event.target instanceof Node &&
        !containerRef.current.contains(event.target)
      ) {
        setOpen(false);
      }
    };

    document.addEventListener('mousedown', handlePointerDown);
    return () => document.removeEventListener('mousedown', handlePointerDown);
  }, [open, setOpen]);

  const closeAndRestoreFocus = () => {
    setOpen(false);
    triggerRef.current?.focus();
  };

  const handleTriggerKeyDown = (event: ReactKeyboardEvent<HTMLButtonElement>) => {
    if (event.key === 'ArrowDown' || event.key === 'ArrowUp') {
      event.preventDefault();
      setOpen(true);
    }
  };

  const handleMenuKeyDown = (event: ReactKeyboardEvent<HTMLDivElement>) => {
    const position = enabledIndexes.indexOf(activeIndex);
    const count = enabledIndexes.length;

    switch (event.key) {
      case 'ArrowDown': {
        event.preventDefault();
        const next = enabledIndexes[(position + 1) % count];
        if (next !== undefined) {
          focusItem(next);
        }
        break;
      }
      case 'ArrowUp': {
        event.preventDefault();
        const previous = enabledIndexes[(position - 1 + count) % count];
        if (previous !== undefined) {
          focusItem(previous);
        }
        break;
      }
      case 'Home': {
        event.preventDefault();
        const first = enabledIndexes[0];
        if (first !== undefined) {
          focusItem(first);
        }
        break;
      }
      case 'End': {
        event.preventDefault();
        const last = enabledIndexes[count - 1];
        if (last !== undefined) {
          focusItem(last);
        }
        break;
      }
      case 'Escape':
        // Stop here so an enclosing Modal does not also close.
        event.stopPropagation();
        closeAndRestoreFocus();
        break;
      case 'Tab':
        setOpen(false);
        break;
      default:
        break;
    }
  };

  return (
    <div ref={containerRef} className="relative inline-block">
      <button
        ref={triggerRef}
        id={triggerId}
        type="button"
        aria-label={triggerLabel}
        aria-haspopup="menu"
        aria-expanded={open}
        aria-controls={open ? menuId : undefined}
        onClick={() => setOpen(!open)}
        onKeyDown={handleTriggerKeyDown}
        className={cn(
          'inline-flex items-center gap-2 rounded-md transition-colors hover:bg-surface-muted',
          triggerClassName,
        )}
      >
        {trigger}
      </button>

      {open ? (
        // The wrapper only receives bubbled key events from the menu items (roving focus).
        <div
          id={menuId}
          role="menu"
          tabIndex={-1}
          aria-labelledby={triggerId}
          onKeyDown={handleMenuKeyDown}
          className={cn(
            'absolute z-40 mt-1 min-w-56 animate-scale-in overflow-hidden rounded-md border border-border bg-surface py-1 shadow-popover',
            align === 'end' ? 'right-0' : 'left-0',
            menuClassName,
          )}
        >
          {header ? <div className="border-b border-border px-3 py-2">{header}</div> : null}
          {items.length === 0 ? (
            <p className="px-3 py-4 text-center text-sm text-muted">
              {emptyMessage ?? 'Nothing here'}
            </p>
          ) : null}
          {items.map((item, index) => (
            <button
              key={item.id}
              ref={(element) => {
                itemRefs.current[index] = element;
              }}
              type="button"
              role="menuitem"
              tabIndex={index === activeIndex ? 0 : -1}
              aria-disabled={item.disabled || undefined}
              disabled={item.disabled}
              onClick={() => {
                item.onSelect();
                closeAndRestoreFocus();
              }}
              className={cn(
                'flex w-full items-center gap-2 px-3 py-2 text-left text-sm hover:bg-surface-muted focus-visible:bg-surface-muted focus-visible:ring-inset focus-visible:ring-offset-0',
                item.destructive && 'text-danger',
                item.disabled && 'opacity-50',
              )}
            >
              {item.icon}
              <span className="min-w-0 flex-1">{item.label}</span>
            </button>
          ))}
        </div>
      ) : null}
    </div>
  );
}
