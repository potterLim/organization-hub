// Isolated fixture only: tests/browser/Fixture and README.md.
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
async function go(page, route) {
    await page.goto(base + route, { waitUntil: 'networkidle' });
    await page.waitForTimeout(700);
}
async function open(browser, width, height, account) {
    const context = await browser.newContext({ viewport: { width, height }, deviceScaleFactor: width === 480 ? 3 : 1, isMobile: width === 480, hasTouch: width === 480, locale: 'ko-KR', timezoneId: 'Asia/Seoul' });
    const page = await context.newPage();
    page.setDefaultTimeout(8000);
    page.on('pageerror', error => errors.push(error.message));
    await go(page, '/login');
    await page.getByLabel('아이디', { exact: true }).fill(account);
    await page.getByLabel('비밀번호', { exact: true }).fill(process.env.HUB_TEST_PASSWORD);
    await page.getByRole('button', { name: '로그인', exact: true }).click();
    await page.waitForURL('**/dashboard');
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
    await expect(page.locator('#blazor-error-ui')).not.toBeVisible();
    const geometry = await page.evaluate(() => ({ width: document.documentElement.clientWidth, scroll: document.documentElement.scrollWidth }));
    assert(geometry.scroll <= geometry.width + 1, JSON.stringify(geometry));
    const file = `${width}-${account}-${name}.png`;
    await page.screenshot({ path: path.join(output, file) });
    results.push({ width, account, name, file, geometry, status: 'pass' });
}
const row = page => page.locator('.hub-todo-row[data-task-list-id="qa-tasks"][data-task-id="task1"]');
async function selectOrganizationCalendar(page) {
    await page.locator('.hub-calendar-selector-wrap .hub-control-select-width').click();
    await page.getByRole('option', { name: '조직 공용', exact: true }).click();
}
async function event(page) {
    await selectOrganizationCalendar(page);
    await page.getByRole('button', { name: /10월 19일 일정/ }).click();
    await page.locator('[data-event-id="event1"]').first().click();
}
(async () => {
    const browser = await chromium.launch({ executablePath: process.env.HUB_BROWSER_EXECUTABLE });
    try {
        for (const [width, height] of [[1440, 1000], [1920, 1080], [480, 1040]]) {
            let { context, page } = await open(browser, width, height, 'ux-admin');
            await context.request.post(base + '/fixture/state/normal');
            await context.request.post(base + '/fixture/reset-resources');
            await go(page, '/calendar'); await event(page);
            await page.getByRole('button', { name: '수정', exact: true }).click();
            const title = page.getByRole('textbox', { name: '일정 제목 (필수)', exact: true });
            await title.fill('저장 실패 후에도 유지되는 일정');
            await context.request.post(base + '/fixture/state/write-reject');
            await page.getByRole('button', { name: '저장', exact: true }).click();
            await expect(page.getByText('일정을 저장하지 못했습니다. 작성한 내용은 유지됩니다.', { exact: true })).toBeVisible();
            await expect(title).toHaveValue('저장 실패 후에도 유지되는 일정');
            await expect(page.getByText('월간 일정', { exact: true })).toBeVisible();
            await proof(page, width, 'ux-admin', 'calendar-draft-retained');
            await menu(page, '할 일');
            await page.getByRole('button', { name: '계속 작성', exact: true }).click();
            await expect(title).toBeFocused();
            await context.request.post(base + '/fixture/state/normal');
            await page.getByRole('button', { name: '저장', exact: true }).click();
            await expect(page.getByRole('heading', { name: '저장 실패 후에도 유지되는 일정', exact: true })).toBeVisible();
            await expect(page.getByRole('button', { name: '입력 버리고 이동', exact: true })).not.toBeVisible();
            await go(page, '/todos');
            await row(page).getByRole('button', { name: '수정', exact: true }).click();
            await row(page).getByRole('textbox', { name: '할 일 제목', exact: true }).fill('저장된 할 일');
            await menu(page, '일정');
            await expect(page.getByRole('button', { name: '계속 작성', exact: true })).toBeVisible();
            await row(page).getByRole('button', { name: '저장', exact: true }).click();
            await expect(row(page).locator('.hub-todo-title')).toHaveText('저장된 할 일');
            await expect(page.getByRole('button', { name: '입력 버리고 이동', exact: true })).not.toBeVisible();
            await proof(page, width, 'ux-admin', 'todo-save-clears-warning');
            await go(page, '/admin/users');
            await page.getByRole('button', { name: '이민준 사용자 수정', exact: true }).click();
            const name = page.getByRole('textbox', { name: '표시 이름 (필수)', exact: true });
            await expect(name).toHaveValue('이민준');
            await expect(page.getByRole('button', { name: '계속 작성', exact: true })).not.toBeVisible();
            await name.fill('보호할 관리자 입력');
            await page.getByRole('button', { name: '새 사용자', exact: true }).first().click();
            await page.getByRole('button', { name: '계속 작성', exact: true }).click();
            await expect(page.getByRole('textbox', { name: '아이디 (필수)', exact: true })).toBeFocused();
            await expect(name).toHaveValue('보호할 관리자 입력');
            await menu(page, '홈');
            await expect(page.getByRole('button', { name: '계속 작성', exact: true })).toBeVisible();
            assert.equal(new URL(page.url()).pathname, '/admin/users');
            await proof(page, width, 'ux-admin', 'admin-draft-protected');
            await page.getByRole('button', { name: '계속 작성', exact: true }).click();
            await page.getByRole('button', { name: '새 사용자', exact: true }).first().click();
            await page.getByRole('button', { name: '입력 버리고 이동', exact: true }).click();
            await expect(name).toHaveValue('');
            await context.close();

            if (process.env.HUB_TEST_AREA === 'admin') continue;
            ({ context, page } = await open(browser, width, height, 'p1-create'));
            await context.request.post(base + '/fixture/reset-resources');
            await go(page, '/todos');
            await row(page).getByRole('button', { name: '수정', exact: true }).click();
            await row(page).getByRole('textbox', { name: '할 일 제목', exact: true }).fill('내 업무 변경');
            await row(page).locator('.hub-calendar-date-time-field-button').click();
            const dueDialog = page.getByRole('dialog', { name: '마감 기한', exact: true });
            await dueDialog.locator('[data-picker-date="2026-10-29"]').click();
            await dueDialog.getByRole('button', { name: '완료', exact: true }).click();
            await context.request.post(base + '/fixture/fault/deadline');
            await row(page).getByRole('button', { name: '저장', exact: true }).click();
            await expect(page.getByText('마감 시간을 저장하는 중입니다.', { exact: true })).toBeVisible();
            await expect(page.getByText('복구 작업', { exact: false })).not.toBeVisible();
            await page.getByText('마감 시간을 저장하는 중입니다.', { exact: true }).scrollIntoViewIfNeeded();
            await proof(page, width, 'p1-create', 'deadline-result');
            await context.request.post(base + '/fixture/fault/none');
            await context.request.post(base + '/fixture/recover');
            await expect(page.getByText('마감 시간을 저장하는 중입니다.', { exact: true })).not.toBeVisible({ timeout: 15000 });
            await go(page, '/upload?root=qa-team');
            const fileName = `회귀검사-${width}.pdf`;
            await page.locator('input[type=file]').setInputFiles({ name: fileName, mimeType: 'application/pdf', buffer: fs.readFileSync(path.join(__dirname, 'Fixture/sample.pdf')) });
            await context.request.post(base + '/fixture/fault/upload');
            await page.getByRole('button', { name: '파일 올리기', exact: true }).click();
            await expect(page.getByText('업로드 기록을 남기지 못했으므로', { exact: false })).not.toBeVisible();
            await expect(page.locator('.mud-alert-filled-success, .mud-alert-outlined-success, .mud-alert-text-success')).toBeVisible();
            await page.locator('.mud-alert').last().scrollIntoViewIfNeeded();
            await proof(page, width, 'p1-create', 'upload-success');
            let records = await (await context.request.get(base + '/fixture/records')).json();
            assert.equal(records.pendingUploads, 1);
            await context.request.post(base + '/fixture/fault/none');
            await context.request.post(base + '/fixture/recover');
            records = await (await context.request.get(base + '/fixture/records')).json();
            assert.equal(records.pendingUploads, 0);
            assert.equal(records.uploads.filter(file => file.fileName === fileName).length, 1);
            await context.close();

            ({ context, page } = await open(browser, width, height, 'p1-delete'));
            for (const mode of ['delete-lost', 'delete-audit']) {
                await context.request.post(base + '/fixture/reset-resources');
                await go(page, '/calendar'); await event(page);
                await page.getByRole('button', { name: '삭제', exact: true }).click();
                if (mode === 'delete-lost') await context.request.post(base + '/fixture/state/delete-lost');
                else await context.request.post(base + '/fixture/fault/delete-audit');
                const before = await (await context.request.get(base + '/fixture/records')).json();
                await page.locator('.hub-delete-confirm').getByRole('button', { name: '삭제', exact: true }).click();
                await expect.poll(async () => (await (await context.request.get(base + '/fixture/records')).json()).latestDeletionId).toBeGreaterThan(before.latestDeletionId);
                if (mode === 'delete-lost') await expect.poll(async () => (await (await context.request.get(base + '/fixture/records')).json()).latestDeletion).toBe('Succeeded');
                await expect(page.locator('.hub-delete-confirm')).not.toBeVisible();
                await go(page, '/calendar');
                await selectOrganizationCalendar(page);
                await page.getByRole('button', { name: /10월 19일 일정/ }).click();
                await expect(page.getByText('이 날짜의 일정이 없습니다.', { exact: true })).toBeVisible();
                await expect(page.getByText('월간 일정', { exact: true })).toBeVisible();
                await expect(page.locator('[data-event-id="event1"]')).toHaveCount(0);
                await expect(page.getByText('삭제 기록 상태', { exact: false })).not.toBeVisible();
                await proof(page, width, 'p1-delete', mode);
                if (mode === 'delete-lost') {
                    const state = await (await context.request.get(base + '/fixture/records')).json();
                    assert.equal(state.latestDeletion, 'Succeeded');
                }
                await context.request.post(base + '/fixture/state/normal');
                await context.request.post(base + '/fixture/fault/none');
            }
            await context.close();
            for (const account of ['operations-reader', 'planning-reader', 'delegated-reader']) {
                ({ context, page } = await open(browser, width, height, account));
                for (const route of ['/calendar', '/todos', '/materials']) {
                    await go(page, route);
                    await expect(page.getByText('접근 권한이 없습니다.', { exact: true })).not.toBeVisible();
                    await proof(page, width, account, route.slice(1));
                }
                for (const folder of ['qa-team', 'qa-planning']) {
                    const response = await context.request.get(base + '/files/' + folder + '-file1/download', { maxRedirects: 0 });
                    const allowed = (folder === 'qa-team') === (account === 'operations-reader');
                    assert.equal(response.status(), allowed ? 200 : 302, JSON.stringify({ account, folder, allowed, location: response.headers().location }));
                    if (!allowed) assert.equal(response.headers().location, '/access-denied');
                }
                await context.close();
            }
            ({ context, page } = await open(browser, width, height, 'final-approver'));
            await go(page, '/approvals/inbox');
            await expect(page.getByRole('button', { name: '저장 다시 시도', exact: true })).toHaveCount(0);
            await expect(page.getByText('승인은 반영되었습니다. 승인본을 준비하고 있습니다.', { exact: true }).first()).toBeVisible();
            await page.getByText('승인은 반영되었습니다. 승인본을 준비하고 있습니다.', { exact: true }).first().scrollIntoViewIfNeeded();
            await proof(page, width, 'final-approver', 'publication-status');
            if (width === 480) {
                const publishing = page.getByText('승인은 반영되었습니다. 승인본을 준비하고 있습니다.', { exact: true });
                const before = await publishing.count();
                const publication = await (await context.request.post(base + '/fixture/publish/34')).json();
                assert(publication.isSuccess, JSON.stringify(publication));
                await expect(publishing).toHaveCount(before - 1, { timeout: 12000 });
                results.push({ width, account: 'final-approver', name: 'publication-auto-refresh', status: 'pass' });
            }
            await context.close();
        }
    } catch (error) {
        for (const context of browser.contexts()) for (const page of context.pages()) { await page.screenshot({ path: path.join(output, 'failure.png') }); fs.writeFileSync(path.join(output, 'failure.txt'), await page.locator('body').innerText()); }
        results.push({ status: 'fail', error: error.stack }); console.error(error); process.exitCode = 1;
    } finally {
        fs.writeFileSync(path.join(output, 'results.json'), JSON.stringify({ results, errors }, null, 2));
        await browser.close();
        console.log(JSON.stringify({ checks: results.length, errors: errors.length, failures: results.filter(item => item.status === 'fail').length }));
    }
})();
