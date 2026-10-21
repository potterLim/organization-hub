// Run only against the isolated October 2026 Google SDK fixture; see README.md.
const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const modulePath = process.env.PLAYWRIGHT_MODULE_PATH || 'playwright';
const { chromium } = require(modulePath);
const { expect } = require(modulePath + '/test');
const base = process.env.HUB_TEST_URL;
const output = process.env.HUB_SCREENSHOT_DIR;
assert(base && output && process.env.HUB_TEST_PASSWORD);
fs.mkdirSync(output, { recursive: true });
const results = [], errors = [];
const longTitle = '장기 프로젝트 고객 온보딩 절차 개편 및 담당 부서별 운영 준비 사항 검토와 최종 승인 요청 자료 작성';
const profiles = [[1440, 1000], [1920, 1080], [480, 1040]].filter(([width]) => !process.env.HUB_TEST_WIDTHS || process.env.HUB_TEST_WIDTHS.split(',').includes(String(width)));
const accounts = ['ux-admin', 'ux-member', 'p1-create', 'p1-update', 'p1-delete', 'team-lead', 'final-approver', 'no-access', 'org-manager', 'new-member'];
async function go(page, route) {
    await page.goto(base + route, { waitUntil: 'networkidle' });
    await page.waitForTimeout(350);
}
async function menu(page, name) {
    const link = page.getByRole('link', { name, exact: true });
    const isOnScreen = await link.count() > 0 && await link.evaluate(element => {
        const bounds = element.getBoundingClientRect();
        return bounds.left >= 0 && bounds.right <= innerWidth && bounds.width > 0;
    });
    if (!isOnScreen) await page.getByRole('button', { name: '좌측 메뉴 열기', exact: true }).click();
    await link.click();
}
async function login(page, account, route = '/login') {
    await go(page, route);
    await page.getByLabel('아이디', { exact: true }).fill(account);
    await page.getByLabel('비밀번호', { exact: true }).fill(process.env.HUB_TEST_PASSWORD);
    await page.getByRole('button', { name: '로그인', exact: true }).click();
    await page.waitForURL(url => !url.pathname.includes('login'));
    await page.waitForTimeout(350);
}
async function proof(page, width, account, name, action) {
    await action();
    await expect(page.locator('#blazor-error-ui')).not.toBeVisible();
    await expect(page.locator('#components-reconnect-modal')).not.toBeVisible();
    await expect(page).toHaveTitle(/Organization Hub/);
    assert((await page.locator('body').innerText()).length > 40);
    const geometry = await page.evaluate(() => ({ clientWidth: document.documentElement.clientWidth, scrollWidth: document.documentElement.scrollWidth }));
    assert(geometry.scrollWidth <= geometry.clientWidth + 1, JSON.stringify(geometry));
    const file = `${width}-${account}-${name}.png`;
    await page.screenshot({ path: path.join(output, file) });
    results.push({ width, account, name, geometry, file, url: page.url(), status: 'pass' });
}
(async () => {
    const browser = await chromium.launch({ executablePath: process.env.HUB_BROWSER_EXECUTABLE });
    try {
        for (const [width, height] of profiles) {
            await (await browser.newContext()).request.post(base + '/fixture/reset-resources');
            for (const account of accounts) {
                const context = await browser.newContext({ viewport: { width, height }, deviceScaleFactor: width === 480 ? 3 : 1, isMobile: width === 480, hasTouch: width === 480, locale: 'ko-KR', timezoneId: 'Asia/Seoul' });
                const page = await context.newPage();
                page.setDefaultTimeout(7000);
                page.on('pageerror', error => errors.push(error.message));
                page.on('console', message => { if (message.type() === 'error') errors.push(message.text()); });
                await login(page, account);
                await proof(page, width, account, 'home', async () => {});
                if (account === 'ux-admin' || account === 'org-manager') {
                    await proof(page, width, account, 'user-keyboard-order', async () => {
                        await go(page, '/admin/users');
                        await page.locator('h1').press('Tab');
                        const focused = await page.evaluate(() => ({ y: document.activeElement.getBoundingClientRect().top, scrollY }));
                        assert(focused.scrollY < 100 && focused.y < height, JSON.stringify(focused));
                    });
                }
                if (account === 'ux-admin') {
                    await proof(page, width, account, 'calendar-navigation-protected', async () => {
                        await go(page, '/calendar');
                        await page.getByRole('button', { name: '일정 추가', exact: true }).click();
                        const title = page.getByRole('textbox', { name: '일정 제목 (필수)', exact: true });
                        await title.fill('보존해야 하는 일정 초안');
                        await title.press('Tab');
                        await menu(page, '할 일');
                        await expect(page.getByRole('button', { name: '계속 작성', exact: true })).toBeVisible();
                        assert(new URL(page.url()).pathname === '/calendar');
                        await page.getByRole('button', { name: '계속 작성', exact: true }).click();
                        await expect(title).toHaveValue('보존해야 하는 일정 초안');
                        await menu(page, '할 일');
                        await expect(page.getByRole('button', { name: '계속 작성', exact: true })).toBeVisible();
                    });
                    await page.getByRole('button', { name: '입력 버리고 이동', exact: true }).click();
                    await page.waitForURL('**/todos');
                    await proof(page, width, account, 'todo-navigation-protected', async () => {
                        await page.getByRole('button', { name: '조직 공용에 추가', exact: true }).click();
                        const input = page.locator('.hub-todo-create-input input');
                        await input.fill('보존해야 하는 할 일 초안');
                        await input.press('Tab');
                        await menu(page, '일정');
                        await expect(page.getByRole('button', { name: '계속 작성', exact: true })).toBeVisible();
                        await page.getByRole('button', { name: '계속 작성', exact: true }).click();
                        await expect(input).toHaveValue('보존해야 하는 할 일 초안');
                        await input.fill('');
                    });
                    const row = page.locator('.hub-todo-row[data-task-list-id="qa-tasks"][data-task-id="task1"]');
                    await row.getByRole('button', { name: '수정', exact: true }).click();
                    await row.getByRole('textbox', { name: '할 일 제목', exact: true }).fill(longTitle);
                    await row.getByRole('button', { name: '저장', exact: true }).click();
                    await expect(row.locator('.hub-todo-title')).toHaveText(longTitle);
                    await proof(page, width, account, 'keyboard-manual-order', async () => {
                        await page.locator('.hub-todo-sort-select').click();
                        await page.getByRole('option', { name: '직접 정렬', exact: true }).click();
                        const handle = row.locator('.hub-todo-drag-handle');
                        await expect(handle).toBeVisible();
                        assert.equal(await handle.evaluate(element => element.tabIndex), 0);
                        await handle.press('ArrowDown');
                        await expect(page.locator('.hub-todo-column').first().locator('.hub-todo-row').nth(1)).toHaveAttribute('data-task-id', 'task1');
                        await expect(handle).toBeFocused();
                    });
                }
                for (const [route, name] of [['/calendar', 'calendar'], ['/todos', 'todos'], ['/materials', 'files'], ['/approvals/inbox', 'approvals']]) {
                    await proof(page, width, account, name, async () => {
                        await go(page, route);
                        if (route === '/todos' && account === 'ux-member') {
                            const title = page.locator('.hub-todo-title').filter({ hasText: longTitle });
                            await expect(title).toBeVisible();
                            assert(await title.evaluate(element => element.scrollWidth <= element.clientWidth + 1 && getComputedStyle(element).whiteSpace !== 'nowrap'));
                            await title.scrollIntoViewIfNeeded();
                        }
                        if (route === '/todos' && ['ux-member', 'p1-create', 'p1-update', 'p1-delete'].includes(account)) {
                            const row = page.locator('.hub-todo-row[data-task-list-id="qa-tasks"][data-task-id="task1"]');
                            assert.equal(await row.getByRole('button', { name: '수정', exact: true }).count(), ['p1-create', 'p1-update'].includes(account) ? 1 : 0);
                            assert.equal(await row.getByRole('button', { name: '삭제', exact: true }).count(), ['p1-create', 'p1-delete'].includes(account) ? 1 : 0);
                        }
                    });
                }
                await context.close();
            }
            for (const [query, expected] of [['/approvals/history?view=requested', '/approvals/history?view=requested'], ['/login?returnUrl=https%3A%2F%2Fexample.invalid', '/dashboard']]) {
                const context = await browser.newContext({ viewport: { width, height } });
                const page = await context.newPage();
                await proof(page, width, 'login', expected.includes('history') ? 'return-to-requested' : 'reject-external-return', async () => {
                    await login(page, 'ux-member', query);
                    assert.equal(new URL(page.url()).origin, new URL(base).origin);
                    assert.equal(new URL(page.url()).pathname + new URL(page.url()).search, expected);
                });
                await context.close();
            }
            fs.writeFileSync(path.join(output, `${width}-results.json`), JSON.stringify({ results: results.filter(item => item.width === width), errors }, null, 2));
        }
    } catch (error) {
        results.push({ status: 'fail', error: error.stack });
        console.error(error);
        process.exitCode = 1;
    } finally {
        fs.writeFileSync(path.join(output, 'results.json'), JSON.stringify({ results, errors }, null, 2));
        console.log(JSON.stringify({ checks: results.length, failures: results.filter(item => item.status === 'fail').length, errors: errors.length }));
        await browser.close();
    }
})();
