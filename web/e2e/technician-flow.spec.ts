import { Browser, Page, expect, test } from '@playwright/test';

// Seeded by DevSeeder in Development (CLAUDE.md).
const PASSWORD = 'Pass123!';
const ADMIN = 'admin@fieldops.local';
const DISPATCHER = 'dispatcher@fieldops.local';
const TECHNICIAN = 'tech2@fieldops.local';

async function logIn(page: Page, email: string): Promise<void> {
  await page.goto('/login');
  await page.getByLabel('Email').fill(email);
  await page.getByLabel('Password').fill(PASSWORD);
  await page.getByRole('button', { name: 'Log in' }).click();
  await page.waitForURL(/\/(office|tech)\//);
}

async function desktopPage(browser: Browser): Promise<Page> {
  const context = await browser.newContext({ viewport: { width: 1400, height: 1000 } });
  return context.newPage();
}

// The technician's phone shares its location when they tap On my way.
test.use({ geolocation: { latitude: 25.2048, longitude: 55.2708 }, permissions: ['geolocation'] });

test('dispatcher schedules a job, the technician completes it, and the office invoices it', async ({ page, browser }) => {
  const title = `E2E boiler check ${Date.now() % 1_000_000}`;

  // Office: create, schedule and dispatch.
  const office = await desktopPage(browser);
  await logIn(office, DISPATCHER);
  await office.goto('/office/work-orders');
  await office.getByRole('button', { name: 'New work order' }).click();
  const create = office.getByRole('dialog');
  await create.getByLabel('Customer').fill('Al Noor Trading');
  await office.getByRole('option', { name: /Al Noor Trading/ }).click();
  await office.getByRole('listbox').waitFor({ state: 'detached' });
  await create.getByLabel('Site').click();
  await office.getByRole('option').first().click();
  await create.getByLabel('Title', { exact: true }).fill(title);
  await create.getByRole('button', { name: 'Create' }).click();
  await office.waitForURL(/\/office\/work-orders\/[0-9a-f-]+$/);
  const jobUrl = office.url();

  await office.getByRole('button', { name: 'Schedule', exact: true }).click();
  const schedule = office.getByRole('dialog', { name: /Schedule/ });
  await schedule.getByLabel('Technician').click();
  await office.getByRole('option', { name: /Tech Two/ }).click();
  await schedule.getByRole('button', { name: 'Schedule' }).click();
  const overlap = office.getByRole('dialog', { name: 'Overlapping jobs' });
  if (await overlap.waitFor({ timeout: 2000 }).then(() => true, () => false)) {
    await overlap.getByRole('button', { name: 'Schedule anyway' }).click();
  }
  await office.getByRole('button', { name: 'Dispatch' }).click();
  await expect(office.locator('app-status-chip').first()).toHaveText('Dispatched');

  // Technician on a phone.
  await logIn(page, TECHNICIAN);
  await expect(page).toHaveURL(/\/tech\/my-jobs/);
  // Late in the evening the next full hour falls on tomorrow's list.
  await page.locator('a.card, .empty').first().waitFor();
  const card = page.locator('a.card', { hasText: title });
  if ((await card.count()) === 0) await page.getByRole('radio', { name: 'Tomorrow' }).click();
  await card.click();
  await expect(page.getByRole('heading', { name: title })).toBeVisible();

  await page.getByRole('button', { name: 'On my way' }).click();
  await expect(page.locator('app-status-chip')).toHaveText('En route');
  await page.getByRole('button', { name: /start job/ }).click();
  await expect(page.locator('app-status-chip')).toHaveText('In progress');

  // A tiny PNG photo.
  const png = Buffer.from(
    'iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mP8z8BQDwAEhQGAhKmMIQAAAABJRU5ErkJggg==',
    'base64',
  );
  await page.locator('input[type=file]').setInputFiles({ name: 'before.png', mimeType: 'image/png', buffer: png });
  await expect(page.locator('app-secure-image img')).toHaveCount(1);

  await page.getByRole('button', { name: 'Complete' }).click();
  const complete = page.getByRole('dialog');
  await complete.getByLabel('What was done').fill('Serviced and tested the boiler.');
  await complete.getByLabel('Customer name').fill('Sara M.');
  const pad = complete.locator('canvas');
  const box = (await pad.boundingBox())!;
  await page.mouse.move(box.x + 20, box.y + box.height / 2);
  await page.mouse.down();
  await page.mouse.move(box.x + box.width / 2, box.y + 20, { steps: 8 });
  await page.mouse.move(box.x + box.width - 20, box.y + box.height - 20, { steps: 8 });
  await page.mouse.up();
  await complete.getByRole('button', { name: 'Complete job' }).click();
  await expect(page.locator('app-status-chip')).toHaveText('Completed');
  await expect(page.getByText('Signed by Sara M.')).toBeVisible();

  // Office sees the result.
  await office.goto(jobUrl);
  await expect(office.locator('app-status-chip').first()).toHaveText('Completed');
  await expect(office.getByText('Serviced and tested the boiler.')).toBeVisible();

  // Office drafts the invoice and adds a call-out fee.
  await office.getByRole('button', { name: 'Create invoice' }).click();
  await office.waitForURL(/\/office\/invoices\/[0-9a-f-]+$/);
  const invoiceUrl = office.url();
  await expect(office.getByRole('heading', { name: 'Draft invoice' })).toBeVisible();
  await office.getByRole('button', { name: 'Add line' }).click();
  const line = office.getByRole('dialog');
  await line.getByLabel('Description').fill('Call-out fee');
  await line.getByLabel(/Unit price/).fill('150');
  await line.getByRole('button', { name: 'Save' }).click();
  await expect(office.getByRole('cell', { name: 'Call-out fee Other' })).toBeVisible();

  // An admin issues it and downloads the PDF.
  const admin = await desktopPage(browser);
  await logIn(admin, ADMIN);
  await admin.goto(invoiceUrl);
  await admin.getByRole('button', { name: 'Issue' }).click();
  await admin.getByRole('dialog').getByRole('button', { name: 'Issue' }).click();
  await expect(admin.getByRole('heading', { name: /^INV-\d{6}$/ })).toBeVisible();
  const download = admin.waitForEvent('download');
  await admin.getByRole('button', { name: 'PDF' }).click();
  expect((await download).suggestedFilename()).toMatch(/^INV-\d{6}\.pdf$/);

  await office.goto(jobUrl);
  await expect(office.locator('app-status-chip').first()).toHaveText('Invoiced');
});
