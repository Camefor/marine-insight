const { test, expect } = require('@playwright/test');

test('dashboard and account shell remain usable without layout overflow', async ({ page }) => {
  const consoleErrors = [];
  const httpErrors = [];
  page.on('console', message => {
    if (message.type() === 'error') consoleErrors.push(`${message.text()} (${message.location().url || 'inline'})`);
  });
  page.on('response', response => {
    if (response.status() >= 400) httpErrors.push(`${response.status()} ${response.url()}`);
  });

  await page.goto('/');
  await expect(page.getByRole('heading', { name: '海岛海况智能决策平台' })).toBeVisible();
  await expect(page.getByRole('button', { name: '查询海况' })).toBeVisible();
  await expect(page.getByText('地图选点', { exact: true })).toBeVisible();
  await expect(page.getByLabel('纬度')).toBeHidden();
  await expect(page.getByLabel('经度')).toBeHidden();

  const forecastStart = page.getByLabel('起报时间', { exact: true });
  const datetimeAction = page.locator('.datetime-action');
  await expect(forecastStart).toBeVisible();
  await expect(datetimeAction).toBeVisible();
  const datetimeLayout = await page.evaluate(() => {
    const input = document.querySelector('.datetime-control input');
    const action = document.querySelector('.datetime-action');
    const display = document.querySelector('.datetime-display');
    if (!input || !action) throw new Error('Date time control is missing.');

    const inputBox = input.getBoundingClientRect();
    const actionBox = action.getBoundingClientRect();
    const actionStyle = getComputedStyle(action);
    return {
      inputWidth: inputBox.width,
      inputHeight: inputBox.height,
      actionWidth: actionBox.width,
      actionHeight: actionBox.height,
      actionInsideInput: actionBox.right <= inputBox.right && actionBox.left >= inputBox.left,
      actionColor: actionStyle.color,
      actionBorder: actionStyle.borderColor,
      displayText: display?.textContent?.trim() || '',
      displayVisible: !!display && getComputedStyle(display).display !== 'none',
      language: input.getAttribute('lang')
    };
  });
  expect(datetimeLayout.inputWidth).toBeGreaterThan(200);
  expect(datetimeLayout.inputHeight).toBeGreaterThanOrEqual(46);
  expect(datetimeLayout.actionWidth).toBeGreaterThanOrEqual(42);
  expect(datetimeLayout.actionHeight).toBeGreaterThanOrEqual(36);
  expect(datetimeLayout.actionInsideInput).toBeTruthy();
  expect(datetimeLayout.actionColor).not.toBe('rgba(0, 0, 0, 0)');
  expect(datetimeLayout.actionBorder).not.toBe('rgba(0, 0, 0, 0)');
  expect(datetimeLayout.language).toBe('zh-CN');
  if (layoutViewportWidth(page) <= 680) {
    expect(datetimeLayout.inputWidth).toBeGreaterThanOrEqual(300);
    expect(datetimeLayout.displayVisible).toBeTruthy();
    expect(datetimeLayout.displayText).toMatch(/\d{4}-\d{2}-\d{2} \d{2}:\d{2}$/);
  }

  const queryBand = page.locator('section[aria-labelledby="query-title"]');
  const initialState = page.getByRole('heading', { name: '等待查询' }).locator('..');
  await expect(queryBand).toBeVisible();
  await expect(initialState).toBeVisible();
  // Leaflet can finish sizing between separate DOM reads, so sample both regions in one browser frame.
  const layout = await page.evaluate(() => {
    const query = document.querySelector('section[aria-labelledby="query-title"]');
    const result = Array.from(document.querySelectorAll('section'))
      .find(section => section.querySelector('h2')?.textContent?.trim() === '等待查询');
    if (!query || !result) throw new Error('Dashboard layout regions are missing.');

    const queryBox = query.getBoundingClientRect();
    const resultBox = result.getBoundingClientRect();
    return {
      viewportWidth: document.documentElement.clientWidth,
      documentWidth: document.documentElement.scrollWidth,
      regionsOverlap:
        queryBox.bottom > resultBox.top &&
        queryBox.top < resultBox.bottom &&
        queryBox.right > resultBox.left &&
        queryBox.left < resultBox.right
    };
  });
  expect(layout.documentWidth).toBeLessThanOrEqual(layout.viewportWidth + 1);
  expect(layout.regionsOverlap).toBeFalsy();

  await page.goto('/account/login');
  await expect(page.getByRole('heading', { name: '登录' })).toBeVisible();
  expect(await page.evaluate(() => document.documentElement.scrollWidth <= document.documentElement.clientWidth + 1)).toBeTruthy();
  expect(consoleErrors).toEqual([]);
  expect(httpErrors).toEqual([]);
});

function layoutViewportWidth(page) {
  return page.viewportSize()?.width ?? 0;
}
