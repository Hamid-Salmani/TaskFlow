import { expect, test } from '@playwright/test';
import { moveCardViaApi, registerViaApi, seedBoard, signInAs } from './helpers';

test("a teammate's change appears on an open board without reloading", async ({
  page,
  request,
}) => {
  const owner = await registerViaApi(request, 'Olivia');
  const viewer = await registerViaApi(request, 'Victor');
  const seeded = await seedBoard(request, owner, viewer);
  const inProgress = seeded.columns[1];
  if (!inProgress) {
    throw new Error('Expected a second column.');
  }

  await signInAs(page, viewer);
  await page.goto(`/boards/${seeded.boardId}`);
  await expect(page.getByRole('region', { name: 'To do, 1 card' })).toBeVisible();

  // The owner moves the card over HTTP; the viewer's page learns about it through SignalR.
  await moveCardViaApi(request, owner, seeded.card, inProgress.id);

  await expect(page.getByRole('region', { name: 'In progress, 1 card' })).toBeVisible();
  await expect(page.getByRole('region', { name: 'To do, 0 cards' })).toBeVisible();
});
