import { fileURLToPath } from 'node:url';
import js from '@eslint/js';
import globals from 'globals';
import jsxA11y from 'eslint-plugin-jsx-a11y';
import reactHooks from 'eslint-plugin-react-hooks';
import tailwindcss from 'eslint-plugin-tailwindcss';
import tseslint from 'typescript-eslint';

export default tseslint.config(
  {
    ignores: [
      'dist',
      'coverage',
      'playwright-report',
      'test-results',
      'src/shared/api/schema.d.ts',
    ],
  },
  js.configs.recommended,
  ...tseslint.configs.recommended,
  jsxA11y.flatConfigs.recommended,
  ...tailwindcss.configs['flat/recommended'],
  {
    files: ['**/*.{ts,tsx}'],
    languageOptions: { globals: { ...globals.browser } },
    plugins: { 'react-hooks': reactHooks },
    settings: {
      tailwindcss: {
        callees: ['cn', 'cva', 'clsx'],
        config: fileURLToPath(new URL('./tailwind.config.ts', import.meta.url)),
      },
    },
    rules: {
      ...reactHooks.configs.recommended.rules,
      '@typescript-eslint/no-explicit-any': 'error',
      '@typescript-eslint/no-non-null-assertion': 'error',
      '@typescript-eslint/consistent-type-assertions': [
        'error',
        { assertionStyle: 'as', objectLiteralTypeAssertions: 'never' },
      ],
      '@typescript-eslint/consistent-type-imports': ['error', { prefer: 'type-imports' }],
      '@typescript-eslint/no-unused-vars': ['error', { argsIgnorePattern: '^_' }],
      // Class names are validated against the real config by tailwindcss/no-custom-classname.
      'tailwindcss/no-custom-classname': 'error',
      // Prettier owns class ordering (prettier-plugin-tailwindcss).
      'tailwindcss/classnames-order': 'off',
      // Labels here wrap the input plus nested text spans.
      'jsx-a11y/label-has-associated-control': ['error', { depth: 3 }],
    },
  },
  {
    // Tests use arbitrary strings as class names to exercise cn().
    files: ['**/*.test.{ts,tsx}'],
    rules: { 'tailwindcss/no-custom-classname': 'off' },
  },
  {
    files: [
      'e2e/**/*.ts',
      'eslint.config.js',
      'playwright.config.ts',
      'vite.config.ts',
      'vitest.config.ts',
      'tailwind.config.ts',
    ],
    languageOptions: { globals: { ...globals.node } },
  },
);
