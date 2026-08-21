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
  await expect(page.getByText('登录后可查询潮汐')).toBeVisible();
  await expect(page.getByLabel('查询潮汐（消耗 Credits）')).toHaveCount(0);
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

  const loadingLayout = await page.evaluate(() => {
    const query = document.querySelector('section[aria-labelledby="query-title"]');
    const header = document.querySelector('.app-header');
    const result = Array.from(document.querySelectorAll('section'))
      .find(section => section.querySelector('h2')?.textContent?.trim() === '等待查询');
    const scopeAttribute = Array.from(query?.attributes ?? [])
      .find(attribute => attribute.name.startsWith('b-'))?.name;
    if (!query || !header || !result || !scopeAttribute) throw new Error('Dashboard loading test regions are missing.');

    const loading = document.createElement('div');
    loading.className = 'query-loading';
    loading.setAttribute('role', 'status');
    const spinner = document.createElement('span');
    spinner.className = 'spinner';
    const copy = document.createElement('span');
    copy.className = 'query-loading-copy';
    const title = document.createElement('strong');
    title.textContent = '正在查询海况';
    const detail = document.createElement('span');
    detail.textContent = '正在汇总天气、海浪与潮汐数据，完成后将在下方更新结果。';
    const badge = document.createElement('span');
    badge.className = 'query-loading-badge';
    badge.textContent = '数据汇总中';
    for (const element of [loading, spinner, copy, title, detail, badge]) element.setAttribute(scopeAttribute, '');
    copy.append(title, detail);
    loading.append(spinner, copy, badge);
    query.appendChild(loading);

    const loadingBox = loading.getBoundingClientRect();
    const queryBox = query.getBoundingClientRect();
    const headerBox = header.getBoundingClientRect();
    const resultBox = result.getBoundingClientRect();
    const overlaps = (first, second) =>
      first.bottom > second.top && first.top < second.bottom && first.right > second.left && first.left < second.right;
    return {
      position: getComputedStyle(loading).position,
      insideQuery:
        loadingBox.left >= queryBox.left &&
        loadingBox.right <= queryBox.right + 1 &&
        loadingBox.top >= queryBox.top &&
        loadingBox.bottom <= queryBox.bottom + 1,
      overlapsHeader: overlaps(loadingBox, headerBox),
      overlapsResult: overlaps(loadingBox, resultBox),
      badgeDisplay: getComputedStyle(badge).display,
      viewportWidth: document.documentElement.clientWidth,
      documentWidth: document.documentElement.scrollWidth
    };
  });
  expect(loadingLayout.position).not.toBe('fixed');
  expect(loadingLayout.insideQuery).toBeTruthy();
  expect(loadingLayout.overlapsHeader).toBeFalsy();
  expect(loadingLayout.overlapsResult).toBeFalsy();
  expect(loadingLayout.documentWidth).toBeLessThanOrEqual(loadingLayout.viewportWidth + 1);
  expect(loadingLayout.badgeDisplay === 'none').toBe(layoutViewportWidth(page) <= 680);

  await page.goto('/account/login');
  await expect(page.getByRole('heading', { name: '登录' })).toBeVisible();
  expect(await page.evaluate(() => document.documentElement.scrollWidth <= document.documentElement.clientWidth + 1)).toBeTruthy();
  expect(consoleErrors).toEqual([]);
  expect(httpErrors).toEqual([]);
});

test('tide chart renders inside its responsive panel', async ({ page }) => {
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

  const rendered = await page.evaluate(async () => {
    const scopeAttribute = Array.from(document.querySelector('.query-band')?.attributes ?? [])
      .find(attribute => attribute.name.startsWith('b-'))?.name;
    if (!scopeAttribute) throw new Error('Dashboard scoped CSS attribute is missing.');

    const section = document.createElement('section');
    section.className = 'tide-section';
    const heading = document.createElement('h2');
    heading.textContent = '潮汐参考';
    const shell = document.createElement('div');
    shell.className = 'tide-chart-shell';
    const chart = document.createElement('div');
    chart.id = 'dashboard-tide-chart-test';
    chart.className = 'tide-chart';
    for (const element of [section, heading, shell, chart]) element.setAttribute(scopeAttribute, '');
    shell.appendChild(chart);
    section.append(heading, shell);
    document.body.appendChild(section);

    const points = Array.from({ length: 25 }, (_, index) => ({
      label: `07-16 ${String(index).padStart(2, '0')}:00`,
      fullLabel: `2026-07-16 ${String(index).padStart(2, '0')}:00`,
      height: 1.2 + Math.sin((index - 3) * Math.PI / 6) * 0.8,
      type: index % 12 === 0 ? 'low' : index % 12 === 6 ? 'high' : 'normal',
      trendText: index < 6 || index >= 18 ? '涨潮' : '退潮'
    }));
    const module = await import('/js/tide-chart.js');
    return module.render(chart.id, {
      accessibleDescription: '固定样本未来二十四小时潮位变化曲线。',
      points
    });
  });

  expect(rendered).toBeTruthy();
  const chart = page.locator('#dashboard-tide-chart-test');
  await expect(chart.locator('canvas')).toBeVisible();
  const layout = await chart.evaluate(element => {
    const chartBox = element.getBoundingClientRect();
    const shellBox = element.parentElement.getBoundingClientRect();
    return {
      chartWidth: chartBox.width,
      chartHeight: chartBox.height,
      insideShell: chartBox.left >= shellBox.left && chartBox.right <= shellBox.right + 1,
      viewportWidth: document.documentElement.clientWidth,
      documentWidth: document.documentElement.scrollWidth
    };
  });
  expect(layout.chartWidth).toBeGreaterThan(250);
  expect(layout.chartHeight).toBeGreaterThanOrEqual(layoutViewportWidth(page) <= 680 ? 280 : 320);
  expect(layout.insideShell).toBeTruthy();
  expect(layout.documentWidth).toBeLessThanOrEqual(layout.viewportWidth + 1);
  expect(consoleErrors).toEqual([]);
  expect(httpErrors).toEqual([]);
});

function layoutViewportWidth(page) {
  return page.viewportSize()?.width ?? 0;
}
