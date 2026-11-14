import { create } from 'zustand';

export type ToastKind = 'success' | 'error' | 'info';

export interface ToastItem {
  id: string;
  kind: ToastKind;
  title: string;
  description?: string;
  durationMs: number;
}

interface ToastInput {
  kind: ToastKind;
  title: string;
  description?: string;
  durationMs?: number;
}

interface ToastState {
  toasts: ToastItem[];
  push: (input: ToastInput) => string;
  dismiss: (id: string) => void;
  clear: () => void;
}

const MAX_VISIBLE = 4;
let counter = 0;

export const useToastStore = create<ToastState>((set) => ({
  toasts: [],
  push: ({ kind, title, description, durationMs }) => {
    counter += 1;
    const id = `toast-${counter}`;
    const item: ToastItem = {
      id,
      kind,
      title,
      durationMs: durationMs ?? (kind === 'error' ? 7000 : 4000),
      ...(description === undefined ? {} : { description }),
    };
    set((state) => ({ toasts: [...state.toasts, item].slice(-MAX_VISIBLE) }));
    return id;
  },
  dismiss: (id) => set((state) => ({ toasts: state.toasts.filter((toast) => toast.id !== id) })),
  clear: () => set({ toasts: [] }),
}));

/** Imperative API usable from anywhere (hooks, interceptors) without React context. */
export const toast = {
  success: (title: string, description?: string) =>
    useToastStore
      .getState()
      .push({ kind: 'success', title, ...(description ? { description } : {}) }),
  error: (title: string, description?: string) =>
    useToastStore
      .getState()
      .push({ kind: 'error', title, ...(description ? { description } : {}) }),
  info: (title: string, description?: string) =>
    useToastStore.getState().push({ kind: 'info', title, ...(description ? { description } : {}) }),
};
