import { useEffect } from 'react';
import { resolveIsDark, useUiStore } from '@/shared/store/ui-store';

/** Keeps the `dark` class on <html> in sync with the stored preference and the OS setting. */
export function useThemeEffect(): void {
  const theme = useUiStore((state) => state.theme);

  useEffect(() => {
    const query = window.matchMedia('(prefers-color-scheme: dark)');
    const apply = () =>
      document.documentElement.classList.toggle('dark', resolveIsDark(theme, query.matches));

    apply();
    if (theme !== 'system') {
      return undefined;
    }

    query.addEventListener('change', apply);
    return () => query.removeEventListener('change', apply);
  }, [theme]);
}
