import AxeBuilder from '@axe-core/playwright';
import { expect, test, type Page } from '@playwright/test';
import { registerViaApi, seedBoard, signInAs } from './helpers';

/** WCAG 2.1 A/AA rules. Colour contrast is included, so both themes are scanned. */
async function scan(page: Page) {
  const results = await new AxeBuilder({ page })
    .withTags(['wcag2a', 'wcag2aa', 'wcag21a', 'wcag21aa'])
    .analyze();
  return results.violations.map(
    (violation) =>
      `${violation.id}: ${violation.nodes.map((node) => node.target.join(' ')).join(' | ')}`,
  );
}

for (const theme of ['light', 'dark'] as const) {
  test.describe(`${theme} theme`, () => {
    test.beforeEach(async ({ page }) => {
      await page.addInitScript((value) => {
        window.localStorage.setItem(
          'taskflow-ui',
          JSON.stringify({ state: { theme: value }, version: 0 }),
        );
      }, theme);
    });

    test('login page has no detectable accessibility violations', async ({ page }) => {
      await page.goto('/login');
      await expect(page.getByRole('button', { name: 'Sign in' })).toBeVisible();

      expect(await scan(page)).toEqual([]);
    });

    test('board and card dialog have no detectable accessibility violations', async ({
      page,
      request,
    }) => {
      const owner = await registerViaApi(request, 'Axel');
      const seeded = await seedBoard(request, owner);
      await signInAs(page, owner);

      await page.goto(`/boards/${seeded.boardId}`);
      await expect(page.getByRole('button', { name: /Write the report/ })).toBeVisible();
      expect(await scan(page), 'board').toEqual([]);

      await page.getByRole('button', { name: /Write the report/ }).click();
      await expect(page.getByRole('dialog', { name: 'Write the report' })).toBeVisible();
      expect(await scan(page), 'card dialog').toEqual([]);
    });
  });
}
