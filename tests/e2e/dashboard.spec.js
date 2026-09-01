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

  await page.addInitScript(() => localStorage.setItem('marine-insight-theme', 'light'));
  await page.goto('/');
  await expect(page.getByRole('heading', { name: '海岛海况智能决策平台' })).toBeVisible();
  await expect(page.getByRole('button', { name: '查询海况' })).toBeVisible();
  await expect(page.getByText('登录后可查询潮汐')).toBeVisible();
  await expect(page.getByLabel('查询潮汐（消耗 Credits）')).toHaveCount(0);
  await expect(page.getByText('地图选点', { exact: true })).toBeVisible();
  await expect(page.getByLabel('纬度')).toBeHidden();
  await expect(page.getByLabel('经度')).toBeHidden();

  const brandLogo = page.locator('.app-brand-logo');
  await expect(brandLogo).toBeVisible();
  await expect(page.locator('.mobile-dock .ui-icon')).toHaveCount(4);
  expect(await brandLogo.evaluate(image => image.complete && image.naturalWidth === 220 && image.naturalHeight === 48)).toBeTruthy();
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
  const dateTheme = await readPickerTheme(page, '.forecast-date-dropdown', '.ant-picker-date-panel');
  expect(dateTheme.theme).toBe('light');
  expect(dateTheme.containerBackground).toBe(dateTheme.popover);
  expect(dateTheme.panelBackground).toBe(dateTheme.popover);
  expect(dateTheme.normalText).toBe(dateTheme.text);
  expect(dateTheme.selectedBackground).toMatch(/8,\s*127,\s*114/);
  expect(dateTheme.selectedText).toBe(dateTheme.accentContrast);
  await dateCells.first().click();
  await expect(forecastDate).toHaveValue(/^\d{4}-\d{2}-\d{2}$/);
  await page.keyboard.press('Escape');
  await page.locator('.forecast-time-picker').click();
  const hourCells = page.locator('.ant-picker-dropdown .ant-picker-time-panel-cell:not(.ant-picker-time-panel-cell-disabled)');
  await expect(hourCells).toHaveCount(24, { timeout: 15_000 });
  await expect(page.locator('.ant-picker-dropdown .ant-picker-time-panel-column')).toHaveCount(1);
  const timeTheme = await readPickerTheme(page, '.forecast-time-dropdown', '.ant-picker-time-panel');
  expect(timeTheme.theme).toBe('light');
  expect(timeTheme.containerBackground).toBe(timeTheme.popover);
  expect(timeTheme.panelBackground).toBe(timeTheme.popover);
  expect(timeTheme.normalText).toBe(timeTheme.text);
  expect(timeTheme.selectedBackground).toMatch(/8,\s*127,\s*114/);
  expect(timeTheme.selectedText).toBe(timeTheme.accentContrast);
  await page.keyboard.press('Escape');
  const datetimeLayout = await page.evaluate(() => {
    const input = document.querySelector('.datetime-control .forecast-date-picker input');
    const hour = document.querySelector('.datetime-control .forecast-time-picker input');
    const suffix = document.querySelector('.datetime-control .forecast-time-picker .ant-picker-suffix');
    const segment = document.querySelector('.range-controls .segmented-group .segment');
    const paidProvider = document.querySelector('.range-controls > .paid-provider-option');
    const queryButton = document.querySelector('.range-controls > .primary-button');
    if (!input || !hour || !suffix) throw new Error('Date time control is missing.');

    const inputBox = input.getBoundingClientRect();
    const controlBox = input.closest('.datetime-control').getBoundingClientRect();
    const datePickerBox = input.closest('.forecast-date-picker.ant-picker')?.getBoundingClientRect();
    const hourBox = hour.getBoundingClientRect();
    const suffixBox = suffix.getBoundingClientRect();
    const segmentBox = segment?.getBoundingClientRect();
    const paidProviderBox = paidProvider?.getBoundingClientRect();
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
      paidProviderTop: paidProviderBox?.top ?? 0,
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
  if (layoutViewportWidth(page) > 1040) {
    expect(Math.abs(datetimeLayout.controlTop - datetimeLayout.paidProviderTop)).toBeLessThanOrEqual(1);
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

  const anchorLayout = await page.evaluate(async () => {
    const query = document.querySelector('.query-band');
    const target = document.querySelector('.empty-state h2');
    if (!query || !target || typeof window.scrollToAnchor !== 'function') {
      throw new Error('Dashboard anchor test regions are missing.');
    }

    target.id = 'summary-title';
    window.scrollToAnchor('summary-title');
    await new Promise(resolve => setTimeout(resolve, 700));
    const queryBox = query.getBoundingClientRect();
    return {
      hash: window.location.hash,
      queryStillVisible: queryBox.bottom > 0 && queryBox.top < window.innerHeight
    };
  });
  expect(anchorLayout.hash).toBe('#summary-title');
  expect(anchorLayout.queryStillVisible).toBeTruthy();

  const hourlyHeaderThemes = await page.evaluate(() => {
    const query = document.querySelector('.query-band');
    const scopeAttribute = Array.from(query?.attributes ?? [])
      .find(attribute => attribute.name.startsWith('b-'))?.name;
    if (!scopeAttribute) throw new Error('Dashboard scoped CSS attribute is missing.');

    const wrap = document.createElement('div');
    wrap.className = 'hourly-table-wrap';
    const table = document.createElement('table');
    const head = document.createElement('thead');
    const row = document.createElement('tr');
    const cell = document.createElement('th');
    cell.textContent = '时间';
    for (const element of [wrap, table, head, row, cell]) element.setAttribute(scopeAttribute, '');
    row.appendChild(cell);
    head.appendChild(row);
    table.appendChild(head);
    wrap.appendChild(table);
    document.body.appendChild(wrap);

    const originalTheme = document.documentElement.dataset.theme;
    document.documentElement.dataset.theme = 'light';
    const light = { background: getComputedStyle(cell).backgroundColor, color: getComputedStyle(cell).color };
    document.documentElement.dataset.theme = 'dark';
    const dark = { background: getComputedStyle(cell).backgroundColor, color: getComputedStyle(cell).color };
    document.documentElement.dataset.theme = originalTheme || 'light';
    wrap.remove();
    return { light, dark };
  });
  expect(hourlyHeaderThemes.light.background).not.toBe('rgba(0, 0, 0, 0)');
  expect(hourlyHeaderThemes.dark.background).not.toBe('rgba(0, 0, 0, 0)');
  expect(hourlyHeaderThemes.light.background).not.toBe(hourlyHeaderThemes.dark.background);
  expect(hourlyHeaderThemes.light.color).not.toBe(hourlyHeaderThemes.dark.color);

  const weatherSummaryLayout = await page.evaluate(() => {
    const scopeAttribute = Array.from(document.querySelector('.query-band')?.attributes ?? [])
      .find(attribute => attribute.name.startsWith('b-'))?.name;
    if (!scopeAttribute) throw new Error('Dashboard scoped CSS attribute is missing.');

    const section = document.createElement('section');
    section.className = 'weather-summary-section';
    const heading = document.createElement('div');
    heading.className = 'section-heading';
    heading.innerHTML = '<h2>天气速览</h2><span class="weather-summary-status status-raining">当前下雨</span>';
    const grid = document.createElement('dl');
    grid.className = 'weather-summary-grid';
    for (const [label, value] of [
      ['当前雨量', '1.3 mm/h'], ['风力', '5级（清风）'], ['平均风', '8.2 m/s'],
      ['阵风', '12.4 m/s'], ['降雨开始', '2026-09-01 08:00'], ['降雨结束', '2026-09-01 11:00']
    ]) {
      const item = document.createElement('div');
      item.innerHTML = `<dt>${label}</dt><dd>${value}</dd>`;
      grid.appendChild(item);
    }
    for (const element of [section, heading, ...heading.children, grid, ...grid.children]) {
      element.setAttribute(scopeAttribute, '');
      for (const child of element.children) child.setAttribute(scopeAttribute, '');
    }
    section.append(heading, grid);
    document.querySelector('.dashboard-shell')?.appendChild(section);

    const sectionBox = section.getBoundingClientRect();
    const gridStyle = getComputedStyle(grid);
    return {
      columns: gridStyle.gridTemplateColumns.split(' ').filter(Boolean).length,
      insideViewport: sectionBox.left >= 0 && sectionBox.right <= document.documentElement.clientWidth + 1,
      viewportWidth: document.documentElement.clientWidth,
      documentWidth: document.documentElement.scrollWidth
    };
  });
  expect(weatherSummaryLayout.columns).toBe(layoutViewportWidth(page) <= 420 ? 1 : layoutViewportWidth(page) <= 680 ? 2 : 3);
  expect(weatherSummaryLayout.insideViewport).toBeTruthy();
  expect(weatherSummaryLayout.documentWidth).toBeLessThanOrEqual(weatherSummaryLayout.viewportWidth + 1);

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

async function readPickerTheme(page, _dropdownSelector, panelSelector) {
  const dropdown = page.locator(`.ant-picker-dropdown:visible`).last();
  const panel = dropdown.locator(panelSelector);
  const normalCell = panel.locator(
    '.ant-picker-cell-in-view:not(.ant-picker-cell-selected):not(.ant-picker-cell-disabled) .ant-picker-cell-inner, ' +
    '.ant-picker-time-panel-cell:not(.ant-picker-time-panel-cell-selected):not(.ant-picker-time-panel-cell-disabled) .ant-picker-time-panel-cell-inner'
  ).first();
  const selectedCell = panel.locator(
    '.ant-picker-cell-selected .ant-picker-cell-inner, ' +
    '.ant-picker-time-panel-cell-selected .ant-picker-time-panel-cell-inner'
  ).first();

  await expect(dropdown).toBeVisible();
  await expect(panel).toBeVisible();
  await expect(normalCell).toBeVisible();
  await expect(selectedCell).toBeVisible();
  return dropdown.evaluate((element, activePanelSelector) => {
    const rootStyle = getComputedStyle(document.documentElement);
    const panelElement = element.querySelector(activePanelSelector);
    const normalCellElement = panelElement?.querySelector(
      '.ant-picker-cell-in-view:not(.ant-picker-cell-selected):not(.ant-picker-cell-disabled) .ant-picker-cell-inner, ' +
      '.ant-picker-time-panel-cell:not(.ant-picker-time-panel-cell-selected):not(.ant-picker-time-panel-cell-disabled) .ant-picker-time-panel-cell-inner'
    );
    const selectedCellElement = panelElement?.querySelector(
      '.ant-picker-cell-selected .ant-picker-cell-inner, ' +
      '.ant-picker-time-panel-cell-selected .ant-picker-time-panel-cell-inner'
    );
    const resolveColor = variableName => {
      const sample = document.createElement('span');
      sample.style.color = `var(${variableName})`;
      element.appendChild(sample);
      const color = getComputedStyle(sample).color;
      sample.remove();
      return color;
    };

    return {
      theme: document.documentElement.dataset.theme,
      popover: resolveColor('--marine-popover'),
      text: resolveColor('--marine-text'),
      accent: resolveColor('--marine-accent'),
      accentContrast: resolveColor('--marine-accent-contrast'),
      containerBackground: getComputedStyle(element.querySelector('.ant-picker-panel-container')).backgroundColor,
      panelBackground: getComputedStyle(panelElement).backgroundColor,
      normalText: getComputedStyle(normalCellElement).color,
      selectedBackground: getComputedStyle(selectedCellElement).backgroundColor,
      selectedText: getComputedStyle(selectedCellElement).color,
      colorScheme: rootStyle.colorScheme
    };
  }, panelSelector);
}

test('light and dark themes persist across about and dashboard via manual toggle', async ({ page }) => {
  await page.emulateMedia({ colorScheme: 'light' });
  await page.goto('/about');
  await page.evaluate(() => localStorage.removeItem('marine-insight-theme'));
  await page.reload();

  const themeToggle = page.locator('[data-theme-toggle]');
  const repositoryLink = page.getByRole('link', { name: 'github.com/Camefor/marine-insight' });
  await expect(page.locator('html')).toHaveAttribute('data-theme', 'light');
  await expect(themeToggle).toHaveAttribute('aria-label', '切换至夜间模式');
  await expect(repositoryLink).toHaveAttribute('href', 'https://github.com/Camefor/marine-insight');
  await expect(repositoryLink).toHaveAttribute('target', '_blank');
  await expectAboutContrast(page);

  await themeToggle.click();
  await expect(page.locator('html')).toHaveAttribute('data-theme', 'dark');
  await expect(themeToggle).toHaveAttribute('aria-label', '切换至日间模式');
  expect(await page.evaluate(() => localStorage.getItem('marine-insight-theme'))).toBe('dark');

  await page.reload();
  await expect(page.locator('html')).toHaveAttribute('data-theme', 'dark');
  await expectAboutContrast(page);

  await page.goto('/');
  await expect(page.locator('html')).toHaveAttribute('data-theme', 'dark');
  const darkDashboardTheme = await readDashboardTheme(page);
  await page.locator('[data-theme-toggle]').click();
  await expect(page.locator('html')).toHaveAttribute('data-theme', 'light');
  const lightDashboardTheme = await readDashboardTheme(page);
  expect(lightDashboardTheme.bodyBackground).not.toBe(darkDashboardTheme.bodyBackground);
  expect(lightDashboardTheme.textColor).not.toBe(darkDashboardTheme.textColor);
  expect(lightDashboardTheme.panelBackground).not.toBe(darkDashboardTheme.panelBackground);
  expect(await page.evaluate(() => localStorage.getItem('marine-insight-theme'))).toBe('light');

  const dimensions = await page.evaluate(() => ({
    viewportWidth: document.documentElement.clientWidth,
    documentWidth: document.documentElement.scrollWidth
  }));
  expect(dimensions.documentWidth).toBeLessThanOrEqual(dimensions.viewportWidth + 1);
});

async function expectAboutContrast(page) {
  const contrastRatios = await page.evaluate(() => {
    const parseColor = value => {
      const channels = value.match(/[\d.]+/g)?.map(Number) ?? [];
      return {
        red: channels[0] ?? 0,
        green: channels[1] ?? 0,
        blue: channels[2] ?? 0,
        alpha: channels[3] ?? 1
      };
    };
    const blend = (foreground, background) => {
      const alpha = foreground.alpha + background.alpha * (1 - foreground.alpha);
      if (alpha === 0) return { red: 255, green: 255, blue: 255, alpha: 1 };
      return {
        red: (foreground.red * foreground.alpha + background.red * background.alpha * (1 - foreground.alpha)) / alpha,
        green: (foreground.green * foreground.alpha + background.green * background.alpha * (1 - foreground.alpha)) / alpha,
        blue: (foreground.blue * foreground.alpha + background.blue * background.alpha * (1 - foreground.alpha)) / alpha,
        alpha
      };
    };
    const luminance = color => {
      const channel = value => {
        const normalized = value / 255;
        return normalized <= 0.03928 ? normalized / 12.92 : ((normalized + 0.055) / 1.055) ** 2.4;
      };
      return 0.2126 * channel(color.red) + 0.7152 * channel(color.green) + 0.0722 * channel(color.blue);
    };
    const contrast = selector => {
      const element = document.querySelector(selector);
      const ancestry = [];
      for (let current = element; current; current = current.parentElement) ancestry.unshift(current);
      let background = { red: 255, green: 255, blue: 255, alpha: 1 };
      for (const current of ancestry) {
        background = blend(parseColor(getComputedStyle(current).backgroundColor), background);
      }
      const foreground = parseColor(getComputedStyle(element).color);
      const lighter = Math.max(luminance(foreground), luminance(background));
      const darker = Math.min(luminance(foreground), luminance(background));
      return (lighter + 0.05) / (darker + 0.05);
    };

    return [
      '.about-hero h1',
      '.about-lead',
      '.about-card p',
      '.about-trust-list li',
      '.about-source a',
      '.about-disclaimer'
    ].map(selector => ({ selector, ratio: contrast(selector) }));
  });

  for (const result of contrastRatios) {
    expect(result.ratio, `${result.selector} contrast ratio`).toBeGreaterThanOrEqual(4.5);
  }
}

async function readDashboardTheme(page) {
  await expect(page.locator('.dashboard-shell')).toBeVisible();
  return page.evaluate(() => ({
    bodyBackground: getComputedStyle(document.body).backgroundImage,
    textColor: getComputedStyle(document.querySelector('.dashboard-shell')).color,
    panelBackground: getComputedStyle(document.querySelector('.query-band')).backgroundImage
  }));
}
