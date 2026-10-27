const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const modulePath = process.env.PLAYWRIGHT_MODULE_PATH || 'playwright';
const { chromium } = require(modulePath);
const { expect } = require(modulePath + '/test');
const base = process.env.HUB_TEST_URL, output = process.env.HUB_SCREENSHOT_DIR;
assert(base && output && process.env.HUB_TEST_PASSWORD);
fs.mkdirSync(output, { recursive: true });
const results = [], errors = [];
async function open(browser, width, height, account, route) {
    const context = await browser.newContext({ viewport: { width, height }, isMobile: width === 480, hasTouch: width === 480, deviceScaleFactor: width === 480 ? 3 : 1, locale: 'ko-KR', timezoneId: 'Asia/Seoul' });
    const page = await context.newPage();
    page.on('pageerror', error => errors.push(error.message));
    await page.goto(base + '/login', { waitUntil: 'networkidle' });
    await page.waitForTimeout(700);
    await page.getByLabel('아이디', { exact: true }).fill(account);
    await page.getByLabel('비밀번호', { exact: true }).fill(process.env.HUB_TEST_PASSWORD);
    await page.getByRole('button', { name: '로그인', exact: true }).click();
    await page.waitForURL('**/dashboard');
    await page.goto(base + route, { waitUntil: 'networkidle' });
    return { page, context };
}
async function selectEvent(page) {
    await page.locator('.hub-calendar-selector-wrap .hub-control-select-width').click();
    await page.getByRole('option', { name: '조직 공용', exact: true }).click();
    await page.getByRole('button', { name: /10월 19일 일정/ }).click();
    await page.locator('[data-event-id="event1"]').first().click();
}
(async () => {
    const browser = await chromium.launch({ executablePath: process.env.HUB_BROWSER_EXECUTABLE });
    try {
        for (const [width, height] of [[1440, 1000], [1920, 1080], [480, 1040]]) {
            for (const calendar of [true, false]) {
                const setup = await browser.newContext();
                await setup.request.post(base + '/fixture/reset-resources');
                await setup.close();
                const route = calendar ? '/calendar' : '/todos';
                const a = await open(browser, width, height, 'p1-delete', route);
                const b = await open(browser, width, height, 'p1-update', route);
                const title = '다른 담당자가 확정한 최신 내용';
                if (calendar) {
                    await selectEvent(a.page);
                    await a.page.getByRole('button', { name: '삭제', exact: true }).click();
                    await selectEvent(b.page);
                    await b.page.getByRole('button', { name: '수정', exact: true }).click();
                    await b.page.getByRole('textbox', { name: '일정 제목 (필수)', exact: true }).fill(title);
                    await b.page.getByRole('button', { name: '저장', exact: true }).click();
                    await expect(b.page.getByRole('heading', { name: title, exact: true })).toBeVisible();
                    await a.page.locator('.hub-delete-confirm').getByRole('button', { name: '삭제', exact: true }).click();
                    await expect(a.page.getByText('다른 사용자가 일정을 변경하여 삭제하지 않았습니다.', { exact: false })).toBeVisible();
                    await expect(a.page.locator('[data-event-id="event1"]').filter({ hasText: title }).first()).toBeVisible();
                } else {
                    const row = page => page.locator('.hub-todo-row[data-task-list-id="qa-tasks"][data-task-id="task1"]');
                    await row(a.page).getByRole('button', { name: '삭제', exact: true }).click();
                    await row(b.page).getByRole('button', { name: '수정', exact: true }).click();
                    await row(b.page).getByRole('textbox', { name: '할 일 제목', exact: true }).fill(title);
                    await row(b.page).getByRole('button', { name: '저장', exact: true }).click();
                    await expect(row(b.page).locator('.hub-todo-title')).toHaveText(title);
                    await row(a.page).getByRole('button', { name: '삭제', exact: true }).click();
                    await expect(a.page.getByText('다른 사용자가 할 일을 변경하여 삭제하지 않았습니다.', { exact: false })).toBeVisible();
                    await expect(row(a.page).locator('.hub-todo-title')).toHaveText(title);
                }
                await expect(a.page.locator('#blazor-error-ui')).not.toBeVisible();
                const file = `${width}-${calendar ? 'calendar' : 'todo'}-stale-delete-blocked.png`;
                await a.page.screenshot({ path: path.join(output, file) });
                results.push({ width, route, file, status: 'pass' });
                await a.context.close(); await b.context.close();
            }
        }
    } catch (error) {
        results.push({ status: 'fail', error: error.stack }); console.error(error); process.exitCode = 1;
    } finally {
        fs.writeFileSync(path.join(output, 'results.json'), JSON.stringify({ results, errors }, null, 2));
        console.log(JSON.stringify({ checks: results.length, errors: errors.length, failures: results.filter(item => item.status === 'fail').length }));
        await browser.close();
    }
})();
