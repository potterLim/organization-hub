// Synthetic local fixture only; no live Google account is used.
const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const modulePath = process.env.PLAYWRIGHT_MODULE_PATH || 'playwright';
const { chromium } = require(modulePath);
const { expect } = require(modulePath + '/test');
const base = process.env.HUB_TEST_URL, output = process.env.HUB_SCREENSHOT_DIR;
const area = process.env.HUB_TEST_AREA || 'all';
assert(base && output && process.env.HUB_TEST_PASSWORD);
fs.mkdirSync(output, { recursive: true });
const results = [], errors = [];
async function go(page, route) {
    await page.goto(base + route, { waitUntil: 'networkidle' });
    await page.waitForTimeout(600);
}
async function login(browser, width, height, account) {
    const context = await browser.newContext({ viewport: { width, height }, deviceScaleFactor: width === 480 ? 3 : 1, isMobile: width === 480, hasTouch: width === 480, locale: 'ko-KR', timezoneId: 'Asia/Seoul' });
    const page = await context.newPage();
    page.on('pageerror', error => errors.push(error.message));
    await go(page, '/login');
    await page.getByLabel('아이디', { exact: true }).fill(account);
    await page.getByLabel('비밀번호', { exact: true }).fill(process.env.HUB_TEST_PASSWORD);
    await page.getByRole('button', { name: '로그인', exact: true }).click();
    await page.waitForURL('**/dashboard');
    await page.waitForTimeout(600);
    return { context, page };
}
async function menu(page, name) {
    const link = page.getByRole('link', { name, exact: true });
    if (!await link.evaluate(element => element.getBoundingClientRect().left >= 0)) {
        await page.getByRole('button', { name: '좌측 메뉴 열기', exact: true }).click();
    }
    await link.click();
}
async function proof(page, width, account, name) {
    const geometry = await page.evaluate(() => ({ width: document.documentElement.clientWidth, scroll: document.documentElement.scrollWidth }));
    assert(geometry.scroll <= geometry.width + 1);
    await expect(page.locator('#blazor-error-ui')).not.toBeVisible();
    const file = `${width}-${account}-${name}.png`;
    await page.screenshot({ path: path.join(output, file) });
    results.push({ width, account, name, file, geometry, status: 'pass' });
}
async function calendar(browser, width, height) {
    const { context, page } = await login(browser, width, height, 'ux-admin');
    try {
        await context.request.post(base + '/fixture/state/normal');
        await context.request.post(base + '/fixture/reset-resources');
        await go(page, '/calendar');
        await page.locator('.hub-calendar-selector-wrap .hub-control-select-width').click();
        await page.getByRole('option', { name: '조직 공용', exact: true }).click();
        await page.getByRole('button', { name: /10월 19일 일정/ }).click();
        await page.locator('[data-event-id="event1"]').first().click();
        await page.getByRole('button', { name: '수정', exact: true }).click();
        const title = page.getByRole('textbox', { name: '일정 제목 (필수)', exact: true });
        await title.fill('저장 중 안전하게 보호되는 일정');
        await context.request.post(base + '/fixture/state/loading');
        await page.getByRole('button', { name: '저장', exact: true }).click();
        await expect(title).toBeDisabled();
        await expect(page.getByRole('textbox', { name: '위치 (선택)', exact: true })).toBeDisabled();
        await expect(page.getByRole('textbox', { name: '설명 (선택)', exact: true })).toBeDisabled();
        await expect(page.locator('.hub-calendar-date-time-field button').first()).toBeDisabled();
        await proof(page, width, 'ux-admin', 'calendar-saving-protected');
        await expect(page.getByRole('heading', { name: '저장 중 안전하게 보호되는 일정', exact: true })).toBeVisible({ timeout: 20000 });
        await proof(page, width, 'ux-admin', 'calendar-saved');
    } finally {
        await context.request.post(base + '/fixture/state/normal');
        await context.close();
    }
}
async function approval(browser, width, height, account) {
    const { context, page } = await login(browser, width, height, account);
    try {
        await go(page, '/approvals/request');
        // Opening an untouched form must not produce a warning.
        await menu(page, '홈');
        await page.waitForURL('**/dashboard');
        await go(page, '/approvals/request');
        const title = page.getByRole('textbox', { name: '요청 제목 (선택 · 미입력 시 파일명)', exact: true });
        await title.fill('이어서 작성할 결재 요청');
        await menu(page, '홈');
        await expect(page.getByRole('button', { name: '계속 작성', exact: true })).toBeVisible();
        await proof(page, width, account, 'approval-navigation-protected');
        await page.getByRole('button', { name: '계속 작성', exact: true }).click();
        await expect(title).toHaveValue('이어서 작성할 결재 요청');
        await expect(title).toBeFocused();
        await menu(page, '홈');
        await page.getByRole('button', { name: '입력 버리고 이동', exact: true }).click();
        await page.waitForURL('**/dashboard');
        await go(page, '/approvals/request');
        await expect(title).toHaveValue('');
        await proof(page, width, account, 'approval-explicit-discard');
    } finally {
        await context.close();
    }
}
(async () => {
    const browser = await chromium.launch({ executablePath: process.env.HUB_BROWSER_EXECUTABLE });
    try {
        for (const [width, height] of [[1440, 1000], [1920, 1080], [480, 1040]]) {
            if (area === 'all' || area === 'calendar') await calendar(browser, width, height);
            if (area === 'all' || area === 'approval') {
                for (const account of ['ux-member', 'p1-create']) await approval(browser, width, height, account);
            }
        }
        assert.deepEqual(errors, []);
    } finally {
        fs.writeFileSync(path.join(output, 'editing-boundaries.json'), JSON.stringify({ results, errors }, null, 2));
        await browser.close();
    }
})().catch(error => { console.error(error); process.exitCode = 1; });
