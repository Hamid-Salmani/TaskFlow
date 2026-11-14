import { useUiStore, type Theme } from '@/shared/store/ui-store';
import { selectUser, useSessionStore } from '@/shared/store/session-store';
import { Avatar } from '@/shared/ui';

const THEME_OPTIONS: Array<{ value: Theme; label: string; description: string }> = [
  { value: 'light', label: 'Light', description: 'Always use the light theme' },
  { value: 'dark', label: 'Dark', description: 'Always use the dark theme' },
  { value: 'system', label: 'System', description: 'Follow your device setting' },
];

export function SettingsPage() {
  const theme = useUiStore((state) => state.theme);
  const setTheme = useUiStore((state) => state.setTheme);
  const user = useSessionStore(selectUser);

  return (
    <div className="mx-auto w-full max-w-2xl space-y-8 p-4 sm:p-6">
      <h1 className="text-2xl font-semibold tracking-tight">Settings</h1>

      {user ? (
        <section
          aria-labelledby="profile-heading"
          className="rounded-lg border border-border bg-surface p-5"
        >
          <h2 id="profile-heading" className="mb-4 text-base font-semibold">
            Profile
          </h2>
          <div className="flex items-center gap-4">
            <Avatar name={user.displayName} size="md" />
            <dl className="text-sm">
              <dt className="sr-only">Name</dt>
              <dd className="font-medium">{user.displayName}</dd>
              <dt className="sr-only">Email</dt>
              <dd className="text-muted">{user.email}</dd>
              <dt className="sr-only">Mention handle</dt>
              <dd className="text-muted">@{user.handle}</dd>
            </dl>
          </div>
        </section>
      ) : null}

      <section className="rounded-lg border border-border bg-surface p-5">
        <fieldset>
          <legend className="mb-1 text-base font-semibold">Appearance</legend>
          <p className="mb-4 text-sm text-muted">Choose how TaskFlow looks on this device.</p>
          <div className="grid gap-3 sm:grid-cols-3">
            {THEME_OPTIONS.map((option) => (
              <label
                key={option.value}
                className="flex cursor-pointer items-start gap-3 rounded-md border border-border p-3 has-[:checked]:border-primary has-[:checked]:bg-primary/5 has-[:focus-visible]:ring-2 has-[:focus-visible]:ring-ring"
              >
                <input
                  type="radio"
                  name="theme"
                  value={option.value}
                  checked={theme === option.value}
                  onChange={() => setTheme(option.value)}
                  className="mt-1 accent-primary"
                />
                <span>
                  <span className="block text-sm font-medium">{option.label}</span>
                  <span className="block text-xs text-muted">{option.description}</span>
                </span>
              </label>
            ))}
          </div>
        </fieldset>
      </section>
    </div>
  );
}
