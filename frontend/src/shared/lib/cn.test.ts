import { cn } from './cn';

describe('cn', () => {
  it('joins class names and skips falsy values', () => {
    expect(cn('a', false, null, undefined, 'b')).toBe('a b');
  });

  it('supports conditional objects and arrays', () => {
    expect(cn('base', { active: true, hidden: false }, ['x', 'y'])).toBe('base active x y');
  });

  it('lets the last conflicting Tailwind utility win', () => {
    expect(cn('p-2 text-sm', 'p-4')).toBe('text-sm p-4');
    expect(cn('bg-primary', 'bg-danger')).toBe('bg-danger');
  });

  it('keeps non-conflicting utilities', () => {
    expect(cn('px-2', 'py-4')).toBe('px-2 py-4');
  });

  it('returns an empty string for no input', () => {
    expect(cn()).toBe('');
  });
});
