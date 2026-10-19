// Run against an isolated app seeded with 1, 12, 99, 100 and 999 events on October 19-23, 2026.
// HUB_TEST_URL, HUB_TEST_USER, HUB_TEST_PASSWORD and HUB_SCREENSHOT_DIR must be supplied by the test host.
const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const { chromium } = require(process.env.PLAYWRIGHT_MODULE_PATH || 'playwright');

const baseUrl = process.env.HUB_TEST_URL;
const user = process.env.HUB_TEST_USER;
const password = process.env.HUB_TEST_PASSWORD;
const output = process.env.HUB_SCREENSHOT_DIR;
assert(baseUrl && user && password && output, 'Supply the isolated test URL, account and screenshot directory.');
fs.mkdirSync(output, { recursive: true });

async function inspectCells(page) {
    return page.locator('.hub-calendar-day').evaluateAll(cells => cells.map(cell => {
        const boundary = cell.getBoundingClientRect();
        const violations = [];
        const tolerance = 0.75;
        for (const element of cell.querySelectorAll('.hub-calendar-day-date, .hub-calendar-day-number, .hub-calendar-day-badges, .hub-calendar-day-count, .hub-calendar-day-time-list, .hub-calendar-day-time-line')) {
            const rect = element.getBoundingClientRect();
            if (!rect.width || !rect.height) continue;
            if (rect.left < boundary.left - tolerance || rect.right > boundary.right + tolerance || rect.top < boundary.top - tolerance || rect.bottom > boundary.bottom + tolerance) {
                violations.push({ element: element.className, left: rect.left - boundary.left, right: rect.right - boundary.right });
            }
            if (element.matches('.hub-calendar-day-count')) {
                const range = document.createRange();
                range.selectNodeContents(element);
                for (const text of range.getClientRects()) {
                    if (text.left < rect.left - tolerance || text.right > rect.right + tolerance) violations.push({ element: 'badge text', text: element.textContent });
                }
            }
        }
        return {
            label: cell.getAttribute('aria-label'),
            count: cell.querySelector('.hub-calendar-day-count')?.textContent ?? '',
            previews: cell.querySelectorAll('.hub-calendar-day-time-line').length,
            violations
        };
    }));
}

(async () => {
    const results = [];
    const errors = [];
    const browser = await chromium.launch(process.env.HUB_BROWSER_EXECUTABLE ? { executablePath: process.env.HUB_BROWSER_EXECUTABLE } : {});
    try {
        for (const [width, height] of [[1440, 1000], [1920, 1080], [480, 1040]]) {
            const context = await browser.newContext({ viewport: { width, height }, deviceScaleFactor: width === 480 ? 3 : 1, isMobile: width === 480, hasTouch: width === 480, locale: 'ko-KR', timezoneId: 'Asia/Seoul' });
            try {
                const page = await context.newPage();
                page.on('pageerror', error => errors.push({ width, message: error.message }));
                page.on('console', message => { if (message.type() === 'error') errors.push({ width, message: message.text() }); });
                await page.goto(baseUrl + '/login', { waitUntil: 'networkidle' });
                await page.getByLabel('아이디', { exact: true }).fill(user);
                await page.getByLabel('비밀번호', { exact: true }).fill(password);
                await page.getByRole('button', { name: '로그인', exact: true }).click();
                await page.waitForURL('**/dashboard');
                await page.goto(baseUrl + '/calendar', { waitUntil: 'networkidle' });
                await page.getByRole('button', { name: '10월 23일 일정 999건 보기', exact: true }).waitFor();
                await page.evaluate(() => document.fonts.ready);
                const cells = await inspectCells(page);
                const cases = [1, 12, 99, 100, 999].map((count, index) => {
                    const cell = cells.find(item => item.label === `10월 ${19 + index}일 일정 ${count}건 보기`);
                    assert(cell, `Missing seeded count ${count} at width ${width}`);
                    return cell;
                });
                const grid = page.locator('.hub-calendar-month-grid');
                await grid.scrollIntoViewIfNeeded();
                await page.screenshot({ path: path.join(output, `calendar-${width}.png`) });
                const runtimeError = await page.locator('#blazor-error-ui').isVisible();
                const pageOverflow = await page.evaluate(() => document.documentElement.scrollWidth > innerWidth);
                results.push({ width, height, title: await page.title(), url: page.url(), runtimeError, pageOverflow, cases, cellViolations: cells.filter(cell => cell.violations.length) });
            } finally {
                await context.close();
            }
        }
    } finally {
        await browser.close();
        fs.writeFileSync(path.join(output, 'calendar-layout.json'), JSON.stringify({ results, errors }, null, 2));
    }
    assert.equal(errors.length, 0, 'Browser errors');
    for (const result of results) {
        assert.equal(result.runtimeError, false);
        assert.equal(result.pageOverflow, false);
        assert.equal(result.cellViolations.length, 0, `${result.width}: ${JSON.stringify(result.cellViolations.slice(0, 5))}`);
        for (const cell of result.cases) assert(cell.previews <= 2, `${result.width}: excessive previews for ${cell.count}`);
    }
    console.log(`PASS: ${results.length} viewports; 1, 12, 99, 100, 999 events; all cell descendants contained.`);
})().catch(error => { console.error(error); process.exitCode = 1; });
