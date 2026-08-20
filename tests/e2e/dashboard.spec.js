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

  const brandMark = page.locator('.app-brand-mark');
  await expect(brandMark).toBeVisible();
  await expect(page.locator('.mobile-dock .ui-icon')).toHaveCount(4);
  expect(await brandMark.evaluate(image => image.complete && image.naturalWidth === 192)).toBeTruthy();
  if (layoutViewportWidth(page) <= 720) {
    await expect(page.locator('.mobile-dock .ui-icon').first()).toBeVisible();
  }

  const forecastDate = page.getByLabel('起报日期', { exact: true });
  const forecastHour = page.locator('.forecast-time-picker input');
  const datetimeHint = page.locator('#forecast-time-hint');
  await expect(forecastDate).toBeVisible();
  await expect(forecastHour).toBeVisible();
  await expect(datetimeHint).toBeVisible();
  // The first InteractiveServer circuit can take a few seconds to attach on a cold start.
  await page.waitForTimeout(5000);
  await page.getByPlaceholder('输入海岛或码头名称').fill('东极岛');
  await page.getByRole('button', { name: '查找地点' }).click();
  await expect(page.locator('#map-picker-title')).toBeHidden();
  await page.locator('.forecast-date-picker').click();
  const dateCells = page.locator('.ant-picker-dropdown .ant-picker-date-panel .ant-picker-cell-in-view:not(.ant-picker-cell-disabled)');
  await expect(dateCells.first()).toBeVisible();
  await dateCells.first().click();
  await expect(forecastDate).toHaveValue(/^\d{4}-\d{2}-\d{2}$/);
  await page.keyboard.press('Escape');
  await page.locator('.forecast-time-picker').click();
  const hourCells = page.locator('.ant-picker-dropdown .ant-picker-time-panel-cell:not(.ant-picker-time-panel-cell-disabled)');
  await expect(hourCells).toHaveCount(24, { timeout: 15_000 });
  await expect(page.locator('.ant-picker-dropdown .ant-picker-time-panel-column')).toHaveCount(1);
  await page.keyboard.press('Escape');
  const datetimeLayout = await page.evaluate(() => {
    const input = document.querySelector('.datetime-control .forecast-date-picker input');
    const hour = document.querySelector('.datetime-control .forecast-time-picker input');
    const suffix = document.querySelector('.datetime-control .forecast-time-picker .ant-picker-suffix');
    const segment = document.querySelector('.range-controls .segmented-group .segment');
    const queryButton = document.querySelector('.range-controls > .primary-button');
    if (!input || !hour || !suffix) throw new Error('Date time control is missing.');

    const inputBox = input.getBoundingClientRect();
    const controlBox = input.closest('.datetime-control').getBoundingClientRect();
    const datePickerBox = input.closest('.forecast-date-picker.ant-picker')?.getBoundingClientRect();
    const hourBox = hour.getBoundingClientRect();
    const suffixBox = suffix.getBoundingClientRect();
    const segmentBox = segment?.getBoundingClientRect();
    const queryButtonBox = queryButton?.getBoundingClientRect();
    const suffixStyle = getComputedStyle(suffix);
    return {
      inputWidth: inputBox.width,
      controlWidth: controlBox.width,
      inputHeight: inputBox.height,
      datePickerWidth: datePickerBox?.width ?? 0,
      datePickerHeight: datePickerBox?.height ?? 0,
      hourHeight: hourBox.height,
      suffixWidth: suffixBox.width,
      suffixInsideControl: suffixBox.right <= controlBox.right && suffixBox.left >= controlBox.left,
      suffixColor: suffixStyle.color,
      controlTop: controlBox.top,
      segmentTop: segmentBox?.top ?? 0,
      queryButtonTop: queryButtonBox?.top ?? 0,
      language: input.closest('.forecast-date-picker-shell')?.getAttribute('lang'),
      selectedDate: input.value,
      selectedHour: hour.value,
      hint: document.querySelector('#forecast-time-hint')?.textContent?.trim() || ''
    };
  });
  expect(datetimeLayout.inputWidth).toBeGreaterThan(100);
  expect(datetimeLayout.datePickerWidth).toBeGreaterThan(200);
  expect(datetimeLayout.inputHeight).toBeGreaterThanOrEqual(20);
  expect(datetimeLayout.datePickerHeight).toBeGreaterThanOrEqual(46);
  expect(datetimeLayout.hourHeight).toBeGreaterThanOrEqual(20);
  expect(datetimeLayout.suffixWidth).toBeGreaterThanOrEqual(14);
  expect(datetimeLayout.suffixInsideControl).toBeTruthy();
  expect(datetimeLayout.suffixColor).not.toBe('rgba(0, 0, 0, 0)');
  if (layoutViewportWidth(page) > 680) {
    expect(Math.abs(datetimeLayout.controlTop - datetimeLayout.segmentTop)).toBeLessThanOrEqual(1);
    expect(Math.abs(datetimeLayout.controlTop - datetimeLayout.queryButtonTop)).toBeLessThanOrEqual(1);
  }
  expect(datetimeLayout.language).toBe('zh-CN');
  expect(datetimeLayout.selectedHour).toMatch(/^\d{2}:00$/);
  expect(datetimeLayout.hint).toContain('分钟固定为 00');
  expect(datetimeLayout.selectedDate).toMatch(/^\d{4}-\d{2}-\d{2}$/);
  if (layoutViewportWidth(page) <= 680) {
    expect(datetimeLayout.controlWidth).toBeGreaterThanOrEqual(300);
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
