// Run against the isolated October 2026 fixture described in README.md.
const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const { chromium } = require(process.env.PLAYWRIGHT_MODULE_PATH || 'playwright');
const { expect } = require((process.env.PLAYWRIGHT_MODULE_PATH || 'playwright') + '/test');
const base = process.env.HUB_TEST_URL;
const output = process.env.HUB_SCREENSHOT_DIR;
assert(base && output && process.env.HUB_TEST_PASSWORD);
fs.mkdirSync(output, { recursive: true });
const results = [], errors = [];
async function navigate(page, route) {
    await page.goto(base + route, { waitUntil: 'networkidle' });
    await page.evaluate(() => document.fonts.ready);
}
async function check(page, width, name, action) {
    try {
        await action();
        await expect(page.locator('#blazor-error-ui')).not.toBeVisible();
        assert((await page.title()).includes('Organization Hub'));
        assert((await page.locator('body').innerText()).length > 40);
        const geometry = await page.evaluate(() => ({ clientWidth: document.documentElement.clientWidth, scrollWidth: document.documentElement.scrollWidth }));
        assert(geometry.scrollWidth <= geometry.clientWidth + 1);
        await page.screenshot({ path: path.join(output, `${width}-${name}.png`) });
        results.push({ width, name, status: 'pass', url: page.url(), geometry });
    } catch (error) {
        results.push({ width, name, status: 'fail', error: error.message });
        await page.screenshot({ path: path.join(output, `${width}-${name}-failed.png`) });
    }
}
(async () => {
    const browser = await chromium.launch({ executablePath: process.env.HUB_BROWSER_EXECUTABLE });
    try {
        for (const [width, height] of [[1440, 1000], [1920, 1080], [480, 1040]]) {
            for (const account of ['ux-admin', 'ux-member']) {
                const context = await browser.newContext({ viewport: { width, height }, isMobile: width === 480, hasTouch: width === 480, deviceScaleFactor: width === 480 ? 3 : 1, locale: 'ko-KR', timezoneId: 'Asia/Seoul' });
                context.setDefaultTimeout(4000);
                const page = await context.newPage();
                page.on('pageerror', error => errors.push(error.message));
                page.on('console', message => { if (message.type() === 'error') errors.push(message.text()); });
                await page.request.post(base + '/fixture/state/normal');
                await navigate(page, '/login');
                await page.getByLabel('아이디', { exact: true }).fill(account);
                await page.getByLabel('비밀번호', { exact: true }).fill(process.env.HUB_TEST_PASSWORD);
                await page.getByRole('button', { name: '로그인', exact: true }).click();
                await page.waitForURL('**/dashboard');
                if (account === 'ux-admin') {
                    await check(page, width, 'calendar-draft', async () => {
                        await navigate(page, '/calendar');
                        await page.getByRole('button', { name: '일정 추가', exact: true }).click();
                        const title = page.getByRole('textbox', { name: '일정 제목 (필수)', exact: true });
                        await title.fill('날짜를 둘러보는 동안 유지할 협업 일정');
                        await title.press('Tab');
                        await page.getByRole('button', { name: /10월 21일 일정/ }).click();
                        await expect(title).toBeVisible();
                        await expect(title).toHaveValue('날짜를 둘러보는 동안 유지할 협업 일정');
                        await page.getByRole('button', { name: '일정 추가', exact: true }).click();
                        await expect(title).toHaveValue('날짜를 둘러보는 동안 유지할 협업 일정');
                        await title.scrollIntoViewIfNeeded();
                        await page.screenshot({ path: path.join(output, `${width}-calendar-draft-retained.png`) });
                        await page.getByRole('button', { name: '취소', exact: true }).click();
                        await expect(page.getByRole('button', { name: '계속 작성', exact: true })).toBeVisible();
                        await page.getByRole('button', { name: '계속 작성', exact: true }).scrollIntoViewIfNeeded();
                        await page.screenshot({ path: path.join(output, `${width}-calendar-discard-confirmation.png`) });
                        await page.getByRole('button', { name: '계속 작성', exact: true }).click();
                        await expect(title).toHaveValue('날짜를 둘러보는 동안 유지할 협업 일정');
                        await page.getByRole('button', { name: '취소', exact: true }).click();
                        await page.getByRole('button', { name: '초안 버리기', exact: true }).click();
                        await expect(title).not.toBeVisible();
                    });
                    await check(page, width, 'approval-summary', async () => {
                        await navigate(page, '/approvals/request');
                        const file = page.getByRole('button', { name: /파일 선택:.*분기운영계획/ });
                        for (const candidate of await file.all()) { if (await candidate.isVisible()) { await candidate.click(); break; } }
                        const title = page.getByRole('textbox', { name: /요청 제목/ });
                        await title.fill('직접 작성한 예산 검토 요청');
                        await title.press('Tab');
                        const summary = page.locator('.hub-approval-submission-summary');
                        await expect(summary).toContainText('2026-10-19_경영지원팀_분기운영계획.pdf');
                        await expect(summary).toContainText('경영지원팀 결재 완료 자료');
                        await page.locator('.hub-approval-line-card').scrollIntoViewIfNeeded();
                        await expect(title).toHaveValue('직접 작성한 예산 검토 요청');
                        // Destination navigation must update the summary rather than retaining the root.
                        await page.locator('.hub-folder-tile').first().click();
                        await expect(summary).toContainText('2026년 4분기 운영 자료');
                        await summary.scrollIntoViewIfNeeded();
                    });
                }
                await check(page, width, account + '-home-density', async () => {
                    await navigate(page, '/dashboard');
                    await expect(page.locator('.hub-dashboard-todo-row')).toHaveCount(12);
                    if (width === 480) {
                        await expect(page.locator('.hub-dashboard-todo-row:visible')).toHaveCount(3);
                        const toggle = page.locator('.hub-dashboard-todo-toggle');
                        await toggle.click();
                        await expect(page.locator('.hub-dashboard-todo-row:visible')).toHaveCount(12);
                        await toggle.click();
                        await expect(page.locator('.hub-dashboard-todo-row:visible')).toHaveCount(3);
                        await page.evaluate(() => scrollTo(0, 0));
                    } else await expect(page.locator('.hub-dashboard-todo-row:visible')).toHaveCount(12);
                });
                await check(page, width, account + '-file-readability', async () => {
                    await navigate(page, '/materials');
                    if (width === 480) {
                        const row = page.locator('.hub-file-compact-row').filter({ hasText: '고객경험개선과제' }).first();
                        const geometry = await row.evaluate(element => {
                            const main = element.querySelector('.hub-file-compact-main').getBoundingClientRect();
                            const actions = element.querySelector('.hub-file-compact-actions').getBoundingClientRect();
                            const outer = element.getBoundingClientRect();
                            return { mainWidth: main.width, rowWidth: outer.width, mainBottom: main.bottom, actionsTop: actions.top };
                        });
                        assert(geometry.mainWidth >= geometry.rowWidth - 36, JSON.stringify(geometry));
                        assert(geometry.actionsTop >= geometry.mainBottom - 1, JSON.stringify(geometry));
                        await expect(row.getByRole('link', { name: '다운로드', exact: true })).toBeVisible();
                        await expect(row.getByRole('button', { name: '삭제', exact: true })).toHaveCount(account === 'ux-admin' ? 1 : 0);
                    }
                });
                await check(page, width, account + '-empty-upload', async () => {
                    await page.request.post(base + '/fixture/state/empty');
                    await navigate(page, '/materials');
                    const upload = page.getByRole('link', { name: '파일 올리기', exact: true });
                    if (account === 'ux-admin') {
                        const action = page.locator('.hub-file-empty-state').getByRole('link', { name: '파일 올리기', exact: true });
                        await expect(action).toBeVisible();
                        await page.screenshot({ path: path.join(output, `${width}-empty-upload-action.png`) });
                        await action.click();
                        await page.waitForURL('**/upload?root=qa-team');
                        await expect(page.locator('.hub-browser-root-select')).toContainText('경영지원팀 자료');
                    } else await expect(page.locator('.hub-file-empty-state').getByRole('link')).toHaveCount(0);
                    await page.request.post(base + '/fixture/state/normal');
                });
                await context.close();
            }
        }
    } finally {
        await browser.close();
        fs.writeFileSync(path.join(output, 'results.json'), JSON.stringify({ results, errors }, null, 2));
    }
    console.log(JSON.stringify({ passed: results.filter(result => result.status === 'pass').length, failed: results.filter(result => result.status === 'fail'), errors }, null, 2));
    assert.equal(results.filter(result => result.status === 'fail').length, 0);
    assert.equal(errors.length, 0);
})();
