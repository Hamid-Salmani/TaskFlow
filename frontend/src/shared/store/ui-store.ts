import { create } from 'zustand';
import { persist } from 'zustand/middleware';

export type Theme = 'light' | 'dark' | 'system';

interface UiState {
  theme: Theme;
  setTheme: (theme: Theme) => void;
}

export const useUiStore = create<UiState>()(
  persist(
    (set) => ({
      theme: 'system',
      setTheme: (theme) => set({ theme }),
    }),
    // The key is also read by the inline script in index.html to avoid a flash of the wrong theme.
    { name: 'taskflow-ui' },
  ),
);

export function resolveIsDark(theme: Theme, prefersDark: boolean): boolean {
  return theme === 'dark' || (theme === 'system' && prefersDark);
}
