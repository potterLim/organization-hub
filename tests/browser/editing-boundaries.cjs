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
    if (!await link.count() || !await link.evaluate(element => element.getBoundingClientRect().left >= 0)) {
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
async function settings(browser, width, height) {
    const { context, page } = await login(browser, width, height, 'ux-admin');
    try {
        await context.request.post(base + '/fixture/state/normal');
        await go(page, '/admin/settings');
        const toggle = page.getByLabel('일정 작성자 표시', { exact: true });
        const initial = await toggle.isChecked();
        await toggle.click();
        await context.request.post(base + '/fixture/state/error');
        await page.getByRole('button', { name: '새로고침', exact: true }).click();
        await expect(page.getByText('일부 목록을 불러오지 못했습니다. 다시 시도해 주세요.', { exact: true })).toBeVisible();
        assert.equal(await toggle.isChecked(), !initial);
        await expect(page.getByText('설정을 다시 불러왔습니다.', { exact: true })).toHaveCount(0);
        await proof(page, width, 'ux-admin', 'settings-failure-retains-draft');
        await context.request.post(base + '/fixture/state/normal');
        await page.getByRole('button', { name: '새로고침', exact: true }).click();
        await expect(page.getByText('연결된 자료 목록을 새로고침했습니다.', { exact: true })).toBeVisible();
        assert.equal(await toggle.isChecked(), !initial);
        await page.getByRole('button', { name: '표시 옵션 저장', exact: true }).click();
        await go(page, '/admin/settings');
        assert.equal(await toggle.isChecked(), !initial);
        await proof(page, width, 'ux-admin', 'settings-display-saved');
        await toggle.click();
        await page.getByRole('button', { name: '표시 옵션 저장', exact: true }).click();
    } finally {
        await context.request.post(base + '/fixture/state/normal');
        await context.close();
    }
}

async function drive(browser, width, height) {
    const { context, page } = await login(browser, width, height, 'ux-admin');
    try {
        await context.request.post(base + '/fixture/state/normal');
        await context.request.post(base + '/fixture/reset-resources');
        await go(page, '/materials');
        const file = page.locator('tr,.hub-file-compact-row').filter({ has: page.locator('a[href="/files/qa-team-file1/download"]') }).filter({ visible: true }).first();
        await file.getByRole('button', { name: '삭제', exact: true }).click();
        await context.request.post(base + '/fixture/state/drive-delete-uncertain');
        await page.locator('.hub-delete-confirm').getByRole('button', { name: '삭제', exact: true }).click();
        await expect(page.getByText('삭제 결과를 확인하지 못했습니다. 목록을 새로고침해 확인해 주세요.', { exact: true })).toBeVisible();
        await expect(file).toBeVisible();
        await proof(page, width, 'ux-admin', 'drive-uncertain-list-retained');
        await context.request.post(base + '/fixture/state/normal');
        await page.getByRole('button', { name: '새로고침', exact: true }).click();
        await expect(page.getByText('삭제 결과를 확인하지 못했습니다.', { exact: false })).toHaveCount(0);
        await file.getByRole('button', { name: '삭제', exact: true }).click();
        await page.locator('.hub-delete-confirm').getByRole('button', { name: '삭제', exact: true }).click();
        await expect(page.locator('a[href="/files/qa-team-file1/download"]')).toHaveCount(0);
        await proof(page, width, 'ux-admin', 'drive-deleted');
    } finally {
        await context.request.post(base + '/fixture/state/normal');
        await context.close();
    }
}
async function upload(browser, width, height) {
    const { context, page } = await login(browser, width, height, 'p1-create');
    try {
        await context.request.post(base + '/fixture/state/normal');
        await go(page, '/upload?root=qa-team');
        const fileName = `초기-기록-검사-${width}.pdf`;
        await page.locator('input[type=file]').setInputFiles({ name: fileName, mimeType: 'application/pdf', buffer: fs.readFileSync(path.join(__dirname, 'Fixture/sample.pdf')) });
        await context.request.post(base + '/fixture/fault/upload-intent');
        await page.getByRole('button', { name: '파일 올리기', exact: true }).click();
        await expect(page.getByText('파일을 올리지 못했습니다. 잠시 후 다시 시도해 주세요.', { exact: true })).toBeVisible();
        let records = await (await context.request.get(base + '/fixture/records')).json();
        assert(!records.teamFiles.some(file => file.fileName === fileName));
        assert(!records.uploads.some(file => file.fileName === fileName));
        await proof(page, width, 'p1-create', 'upload-preparation-failure');
        await context.request.post(base + '/fixture/fault/none');
        await page.getByRole('button', { name: '파일 올리기', exact: true }).click();
        await expect(page.getByText('파일 올리기 완료: ' + fileName, { exact: true })).toBeVisible();
        records = await (await context.request.get(base + '/fixture/records')).json();
        assert.equal(records.teamFiles.filter(file => file.fileName === fileName).length, 1);
        assert.equal(records.uploads.filter(file => file.fileName === fileName).length, 1);
        await proof(page, width, 'p1-create', 'upload-retry-saved-once');
    } finally {
        await context.request.post(base + '/fixture/fault/none');
        await context.close();
    }
}
async function copy(browser, width, height) {
    for (const account of ['ux-member', 'new-member']) {
        const { context, page } = await login(browser, width, height, account);
        try {
            await context.request.post(base + '/fixture/state/root-unconfigured');
            await go(page, '/dashboard');
            await expect(page.locator('.hub-setup-guide')).toHaveCount(0);
            await expect(page.getByText('조직의 기본 연결을 준비하고 있습니다.', { exact: false })).toHaveCount(0);
            await proof(page, width, account, 'home-contextual-guidance');
        } finally {
            await context.request.post(base + '/fixture/state/normal');
            await context.close();
        }
    }
    const { context, page } = await login(browser, width, height, 'ux-admin');
    try {
        await context.request.post(base + '/fixture/state/unconfigured');
        await go(page, '/dashboard');
        await expect(page.getByText('자료를 함께 보관하려면 Google 계정과 기본 폴더를 연결해 주세요.', { exact: true })).toBeVisible();
        await proof(page, width, 'ux-admin', 'home-setup-actions');
        await context.request.post(base + '/fixture/state/normal');
        await go(page, '/admin/settings');
        const help = page.getByText('자료를 보관할 기본 폴더를 선택해 주세요. 일정과 할 일은 사용하는 업무에 맞춰 따로 연결할 수 있습니다.', { exact: true });
        await expect(help).not.toBeVisible();
        await proof(page, width, 'ux-admin', 'settings-help-collapsed');
        await page.locator('summary').filter({ hasText: '연결 설정 도움말' }).click();
        await expect(help).toBeVisible();
        await proof(page, width, 'ux-admin', 'settings-help-expanded');
        await go(page, '/admin/users');
        assert(await page.locator('td[data-label]').evaluateAll(cells => cells.every(cell => !cell.getAttribute('data-label').includes('필수'))));
        await proof(page, width, 'ux-admin', 'user-list-labels');
        await page.getByRole('button', { name: '로그아웃', exact: true }).click();
        await page.waitForURL('**/login**');
        await go(page, '/login?reason=session-ended');
        await expect(page.getByText('로그인이 종료되었습니다. 다시 로그인해 주세요.', { exact: true })).toBeVisible();
        await proof(page, width, 'login', 'session-ended');
    } finally {
        await context.request.post(base + '/fixture/state/normal');
        await context.close();
    }
}
(async () => {
    const browser = await chromium.launch({ executablePath: process.env.HUB_BROWSER_EXECUTABLE });
    try {
        for (const [width, height] of [[1440, 1000], [1920, 1080], [480, 1040]]) {
            if (area === 'all' || area === 'calendar') await calendar(browser, width, height);
            if (area === 'all' || area === 'settings') await settings(browser, width, height);
            if (area === 'all' || area === 'drive') await drive(browser, width, height);
            if (area === 'all' || area === 'upload') await upload(browser, width, height);
            if (area === 'all' || area === 'copy') await copy(browser, width, height);
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
