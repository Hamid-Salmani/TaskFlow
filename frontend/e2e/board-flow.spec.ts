import { expect, test } from '@playwright/test';
import { PASSWORD, uniqueEmail } from './helpers';

test('register, create a workspace and a board, add a card and move it to another column', async ({
  page,
}) => {
  // --- register through the UI ---
  await page.goto('/register');
  await page.getByLabel('Name', { exact: true }).fill('Ada Lovelace');
  await page.getByLabel('Email').fill(uniqueEmail('ada'));
  await page.getByLabel('Password', { exact: true }).fill(PASSWORD);
  await page.getByLabel('Confirm password').fill(PASSWORD);
  await page.getByRole('button', { name: 'Create account' }).click();
  await expect(page.getByRole('heading', { name: 'Workspaces' })).toBeVisible();

  // --- workspace ---
  await page
    .getByRole('button', { name: /new workspace|create workspace/i })
    .first()
    .click();
  await page.getByLabel('Name').fill('Acme');
  await page.getByRole('button', { name: 'Create workspace' }).click();
  await expect(page.getByRole('heading', { name: 'Acme' })).toBeVisible();

  // --- board ---
  await page
    .getByRole('button', { name: /new board|create board/i })
    .first()
    .click();
  await page.getByLabel('Name').fill('Launch plan');
  await page.getByRole('button', { name: 'Create board' }).click();
  await page.getByRole('link', { name: /Launch plan/ }).click();
  await expect(page.getByRole('heading', { name: 'Launch plan' })).toBeVisible();

  // --- add a card to "To do" ---
  const todo = page.getByRole('region', { name: /^To do,/ });
  await todo.getByRole('button', { name: 'Add a card' }).click();
  await todo.getByLabel('Card title').fill('Write the launch post');
  await todo.getByLabel('Card title').press('Enter');
  await expect(page.getByRole('region', { name: 'To do, 1 card' })).toBeVisible();

  // --- drag it to "In progress" with the real pointer ---
  const card = page.getByRole('button', { name: /Write the launch post/ });
  const target = page.getByRole('region', { name: /^In progress,/ });
  const from = await card.boundingBox();
  const to = await target.boundingBox();
  if (!from || !to) {
    throw new Error('Could not measure the drag source and target.');
  }
  await page.mouse.move(from.x + from.width / 2, from.y + from.height / 2);
  await page.mouse.down();
  await page.mouse.move(from.x + from.width / 2 + 20, from.y + from.height / 2 + 10, { steps: 5 });
  await page.mouse.move(to.x + to.width / 2, to.y + 80, { steps: 20 });
  await page.mouse.up();

  await expect(page.getByRole('region', { name: 'In progress, 1 card' })).toBeVisible();
  await expect(page.getByRole('region', { name: 'To do, 0 cards' })).toBeVisible();

  // --- the move was persisted ---
  await page.reload();
  await expect(page.getByRole('region', { name: 'In progress, 1 card' })).toBeVisible();
});

test('a card can be opened, edited and commented on', async ({ page, request }) => {
  const { registerViaApi, seedBoard, signInAs } = await import('./helpers');
  const owner = await registerViaApi(request, 'Grace');
  const seeded = await seedBoard(request, owner);
  await signInAs(page, owner);

  await page.goto(`/boards/${seeded.boardId}`);
  await page.getByRole('button', { name: /Write the report/ }).click();

  const dialog = page.getByRole('dialog', { name: 'Write the report' });
  await dialog.getByLabel('Description').fill('Quarterly numbers');
  await dialog.getByRole('button', { name: 'Save changes' }).click();
  await expect(page.getByText('Card saved')).toBeVisible();

  await dialog.getByLabel('Write a comment').fill('First draft is ready');
  await dialog.getByRole('button', { name: 'Comment' }).click();
  await expect(dialog.getByText('First draft is ready')).toBeVisible();

  await page.keyboard.press('Escape');
  await expect(dialog).toBeHidden();
  await expect(page).not.toHaveURL(/card=/);
});

test('signed-out visitors are sent to the login page, and bad credentials are explained', async ({
  page,
}) => {
  await page.goto('/workspaces');
  await expect(page).toHaveURL(/\/login/);

  await page.getByLabel('Email').fill('nobody@e2e.dev');
  await page.getByLabel('Password').fill('WrongPassword1');
  await page.getByRole('button', { name: 'Sign in' }).click();
  await expect(page.getByRole('alert')).toContainText('Invalid email or password');
});

test('a card can be moved with the keyboard alone', async ({ page, request }) => {
  const { registerViaApi, seedBoard, signInAs } = await import('./helpers');
  const owner = await registerViaApi(request, 'Kai');
  const seeded = await seedBoard(request, owner);
  await signInAs(page, owner);
  await page.goto(`/boards/${seeded.boardId}`);

  await page.getByRole('button', { name: /Write the report/ }).focus();
  await page.keyboard.press('Space'); // pick up
  await page.keyboard.press('ArrowRight'); // next column
  await page.keyboard.press('Space'); // drop

  await expect(page.getByRole('region', { name: 'In progress, 1 card' })).toBeVisible();
  await expect(page.getByRole('region', { name: 'To do, 0 cards' })).toBeVisible();
  await page.reload();
  await expect(page.getByRole('region', { name: 'In progress, 1 card' })).toBeVisible();
});
