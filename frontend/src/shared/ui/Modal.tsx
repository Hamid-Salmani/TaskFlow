import { X } from 'lucide-react';
import {
  useEffect,
  useId,
  useRef,
  type KeyboardEvent as ReactKeyboardEvent,
  type MouseEvent as ReactMouseEvent,
  type ReactNode,
} from 'react';
import { createPortal } from 'react-dom';
import { cn } from '@/shared/lib/cn';
import { Button } from './Button';

const FOCUSABLE_SELECTOR = [
  'a[href]',
  'button:not([disabled])',
  'textarea:not([disabled])',
  'input:not([disabled]):not([type="hidden"])',
  'select:not([disabled])',
  '[tabindex]:not([tabindex="-1"])',
].join(',');

const sizeClasses = {
  sm: 'max-w-md',
  md: 'max-w-lg',
  lg: 'max-w-2xl',
  xl: 'max-w-4xl',
} as const;

/** Open dialogs, oldest first. Only the topmost one reacts to keys that arrive while focus is outside every dialog. */
const openModals: symbol[] = [];

export interface ModalProps {
  open: boolean;
  onClose: () => void;
  /** Accessible name of the dialog. Always required; use `hideTitle` to keep it visually hidden. */
  title: string;
  description?: string;
  hideTitle?: boolean;
  size?: keyof typeof sizeClasses;
  children: ReactNode;
}

function getFocusable(container: HTMLElement): HTMLElement[] {
  return Array.from(container.querySelectorAll<HTMLElement>(FOCUSABLE_SELECTOR)).filter(
    (element) => element.getAttribute('aria-hidden') !== 'true',
  );
}

export function Modal({
  open,
  onClose,
  title,
  description,
  hideTitle = false,
  size = 'md',
  children,
}: ModalProps) {
  const titleId = useId();
  const descriptionId = useId();
  const panelRef = useRef<HTMLDivElement>(null);
  const onCloseRef = useRef(onClose);
  onCloseRef.current = onClose;
  const contentRef = useRef<HTMLDivElement>(null);

  // Focus management: move focus into the dialog on open, give it back to the opener on close.
  useEffect(() => {
    if (!open) {
      return undefined;
    }

    const previouslyFocused =
      document.activeElement instanceof HTMLElement ? document.activeElement : null;
    const panel = panelRef.current;
    if (panel) {
      // Prefer an explicit [data-autofocus] element, then the first control of the content, then the close button.
      const target =
        panel.querySelector<HTMLElement>('[data-autofocus]') ??
        (contentRef.current ? getFocusable(contentRef.current)[0] : undefined) ??
        getFocusable(panel)[0] ??
        panel;
      target.focus();
    }

    const previousOverflow = document.body.style.overflow;
    document.body.style.overflow = 'hidden';

    return () => {
      document.body.style.overflow = previousOverflow;
      if (previouslyFocused && document.contains(previouslyFocused)) {
        previouslyFocused.focus();
      }
    };
  }, [open]);

  // Safety net for when focus is lost (for example the focused button was removed): Escape still closes the topmost
  // dialog and Tab brings focus back inside. Keys pressed inside the dialog are handled by its own onKeyDown.
  useEffect(() => {
    if (!open) {
      return undefined;
    }

    const id = Symbol('modal');
    openModals.push(id);

    const onDocumentKeyDown = (event: KeyboardEvent) => {
      const panel = panelRef.current;
      const isTopmost = openModals[openModals.length - 1] === id;
      if (!panel || !isTopmost || panel.contains(document.activeElement)) {
        return;
      }
      if (event.key === 'Escape') {
        onCloseRef.current();
      } else if (event.key === 'Tab') {
        event.preventDefault();
        (getFocusable(panel)[0] ?? panel).focus();
      }
    };
    document.addEventListener('keydown', onDocumentKeyDown);

    return () => {
      document.removeEventListener('keydown', onDocumentKeyDown);
      openModals.splice(openModals.indexOf(id), 1);
    };
  }, [open]);

  if (!open) {
    return null;
  }

  const handleKeyDown = (event: ReactKeyboardEvent<HTMLDivElement>) => {
    if (event.key === 'Escape') {
      event.stopPropagation();
      onClose();
      return;
    }

    if (event.key !== 'Tab') {
      return;
    }

    // Focus trap: Tab and Shift+Tab cycle inside the dialog.
    const panel = panelRef.current;
    if (!panel) {
      return;
    }

    const focusable = getFocusable(panel);
    const first = focusable[0];
    const last = focusable[focusable.length - 1];
    if (!first || !last) {
      event.preventDefault();
      panel.focus();
      return;
    }

    const active = document.activeElement;
    if (event.shiftKey && (active === first || active === panel)) {
      event.preventDefault();
      last.focus();
    } else if (!event.shiftKey && active === last) {
      event.preventDefault();
      first.focus();
    }
  };

  const handleOverlayMouseDown = (event: ReactMouseEvent<HTMLDivElement>) => {
    if (event.target === event.currentTarget) {
      onClose();
    }
  };

  return createPortal(
    // The overlay only adds a pointer shortcut; keyboard users close the dialog with Escape or the close button.
    // eslint-disable-next-line jsx-a11y/no-static-element-interactions
    <div
      className="fixed inset-0 z-50 flex items-end justify-center overflow-y-auto bg-black/50 p-0 sm:items-start sm:p-6 sm:pt-[8vh]"
      onMouseDown={handleOverlayMouseDown}
    >
      {/* eslint-disable-next-line jsx-a11y/no-noninteractive-element-interactions */}
      <div
        ref={panelRef}
        role="dialog"
        aria-modal="true"
        aria-labelledby={titleId}
        aria-describedby={description ? descriptionId : undefined}
        tabIndex={-1}
        onKeyDown={handleKeyDown}
        className={cn(
          'w-full animate-scale-in rounded-t-lg bg-surface shadow-popover sm:rounded-lg',
          sizeClasses[size],
        )}
      >
        <div className="flex items-start justify-between gap-4 border-b border-border px-5 py-4">
          <div className={cn(hideTitle && 'sr-only')}>
            <h2 id={titleId} className="text-lg font-semibold leading-tight">
              {title}
            </h2>
            {description ? (
              <p id={descriptionId} className="mt-1 text-sm text-muted">
                {description}
              </p>
            ) : null}
          </div>
          <Button variant="ghost" size="icon" aria-label="Close dialog" onClick={onClose}>
            <X className="size-4" aria-hidden="true" />
          </Button>
        </div>
        <div ref={contentRef} className="px-5 py-4">
          {children}
        </div>
      </div>
    </div>,
    document.body,
  );
}
