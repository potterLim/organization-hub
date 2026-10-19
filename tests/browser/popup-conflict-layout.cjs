// Use an isolated October 2026 fixture with task1 and two same-name files in qa-team.
const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const { chromium } = require(process.env.PLAYWRIGHT_MODULE_PATH || 'playwright');

const baseUrl = process.env.HUB_TEST_URL;
const output = process.env.HUB_SCREENSHOT_DIR;
assert(baseUrl && output && process.env.HUB_TEST_USER && process.env.HUB_TEST_PASSWORD);
fs.mkdirSync(output, { recursive: true });
const results = [];
const errors = [];

async function navigate(page, route) {
    await page.goto(baseUrl + route, { waitUntil: 'networkidle' });
    await page.evaluate(() => document.fonts.ready);
}

async function login(page, username) {
    await navigate(page, '/login');
    await page.getByLabel('아이디', { exact: true }).fill(username);
    await page.getByLabel('비밀번호', { exact: true }).fill(process.env.HUB_TEST_PASSWORD);
    await page.getByRole('button', { name: '로그인', exact: true }).click();
    await page.waitForURL('**/dashboard');
}

async function check(page, width, name, action, inspect) {
    const area = process.env.HUB_LAYOUT_AREA;
    const isConflict = name.includes('conflict');
    if ((area === 'popup' && isConflict) || (area === 'conflict' && !isConflict)) return;
    try {
        await action();
        await page.waitForTimeout(250);
        const geometry = await inspect();
        const screenshot = path.join(output, `${width}-${name}.png`);
        await page.screenshot({ path: screenshot });
        assert.equal(await page.locator('#blazor-error-ui').isVisible(), false);
        assert((await page.title()).includes('Organization Hub'));
        assert((await page.locator('body').innerText()).trim().length > 40);
        assert.equal(geometry.violations.length, 0, JSON.stringify(geometry));
        if (!isConflict) await verifyPickerInteraction(page);
        results.push({ width, name, status: 'pass', url: page.url(), geometry, screenshot });
    } catch (error) {
        results.push({ width, name, status: 'fail', error: error.message });
    }
}

async function verifyPickerInteraction(page) {
    const dialog = page.getByRole('dialog');
    const fieldIndex = await dialog.evaluate(element => [...document.querySelectorAll('.hub-calendar-date-time-field')].indexOf(element.parentElement));
    const field = page.locator('.hub-calendar-date-time-field').nth(fieldIndex);
    const selected = dialog.locator('[data-picker-date][aria-pressed="true"]:not(:disabled)');
    const day = await selected.count() ? selected : dialog.locator('[data-picker-date]:not(:disabled)').first();
    const date = await day.getAttribute('data-picker-date');
    await day.click();
    await dialog.getByRole('button', { name: '완료', exact: true }).click();
    await dialog.waitFor({ state: 'hidden' });
    await page.waitForFunction(([index, expected]) => document.querySelectorAll('.hub-calendar-date-time-field')[index].textContent.includes(expected), [fieldIndex, date]);
    assert.notEqual(await field.evaluate(element => getComputedStyle(element.closest('.hub-card')).backdropFilter), 'none');
    await field.locator('.hub-calendar-date-time-field-button').click();
    await page.waitForFunction(() => document.activeElement?.matches('[data-picker-date]'));
    await page.keyboard.press('Escape');
    await dialog.waitFor({ state: 'hidden' });
    await page.waitForFunction(() => document.activeElement?.matches('.hub-calendar-date-time-field-button'));
}

async function inspectPopup(page) {
    return page.locator('[role="dialog"]').evaluate(panel => {
        const rect = panel.getBoundingClientRect();
        // Mobile innerWidth can expand with overflowing descendants; clientWidth keeps the viewport width.
        const width = document.documentElement.clientWidth;
        const violations = [];
        if (document.documentElement.scrollWidth > width + 1) violations.push('document overflows client viewport');
        if (rect.left < 0 || rect.right > width + 1) violations.push('popup leaves viewport');
        if (rect.top < 0 || rect.bottom > innerHeight + 1) violations.push('popup leaves vertical viewport');
        const headerBottom = document.querySelector('.hub-appbar')?.getBoundingClientRect().bottom || 0;
        if (rect.top < headerBottom) violations.push('popup is covered by fixed header');
        for (const button of panel.querySelectorAll('button')) {
            const bounds = button.getBoundingClientRect();
            if (bounds.left < rect.left - 1 || bounds.right > rect.right + 1) violations.push('popup control leaves panel');
        }
        return { clientWidth: width, innerWidth, scrollWidth: document.documentElement.scrollWidth, left: rect.left, right: rect.right, top: rect.top, bottom: rect.bottom, viewportHeight: innerHeight, violations };
    });
}

async function inspectConflict(panel) {
    return panel.evaluate(element => {
        const bounds = element.getBoundingClientRect();
        const title = element.querySelector('strong').getBoundingClientRect();
        const violations = [];
        if (title.width < Math.min(180, bounds.width - 80)) violations.push('conflict title collapsed');
        const children = [...element.children].map(child => {
            const rect = child.getBoundingClientRect();
            if (rect.left < bounds.left - 1 || rect.right > bounds.right + 1) violations.push('content leaves conflict panel');
            return { left: rect.left, right: rect.right, top: rect.top, bottom: rect.bottom };
        });
        for (let first = 0; first < children.length; first++) {
            for (let second = first + 1; second < children.length; second++) {
                const a = children[first], b = children[second];
                if (Math.min(a.right, b.right) - Math.max(a.left, b.left) > 1 && Math.min(a.bottom, b.bottom) - Math.max(a.top, b.top) > 1) violations.push('conflict sections overlap');
            }
        }
        return { panelWidth: bounds.width, titleWidth: title.width, children, violations };
    });
}

(async () => {
    const browser = await chromium.launch({ executablePath: process.env.HUB_BROWSER_EXECUTABLE });
    try {
        for (const [width, height] of [[1440, 1000], [1920, 1080], [480, 1040], [360, 800]]) {
            const contextOptions = { viewport: { width, height }, deviceScaleFactor: width <= 480 ? 3 : 1, isMobile: width <= 480, hasTouch: width <= 480, locale: 'ko-KR', timezoneId: 'Asia/Seoul' };
            const context = await browser.newContext(contextOptions);
            try {
                const page = await context.newPage();
                page.setDefaultTimeout(10000);
                page.on('pageerror', error => errors.push({ width, message: error.message }));
                page.on('console', message => { if (message.type() === 'error') errors.push({ width, message: message.text() }); });
                await login(page, process.env.HUB_TEST_USER);

                for (const label of ['시작', '종료']) {
                    await check(page, width, `calendar-${label}`, async () => {
                        await navigate(page, '/calendar');
                        await page.getByRole('button', { name: '일정 추가', exact: true }).click();
                        await page.getByRole('button', { name: new RegExp(`^${label} `) }).click();
                        await page.getByRole('dialog', { name: label, exact: true }).waitFor();
                    }, () => inspectPopup(page));
                }
                for (const isCreating of [false, true]) {
                    await check(page, width, isCreating ? 'todo-create-due' : 'todo-edit-due', async () => {
                        await navigate(page, '/todos');
                        if (isCreating) {
                            await page.getByRole('button', { name: /에 추가$/ }).first().click();
                            await page.locator('.hub-todo-create .hub-calendar-date-time-field-button').click();
                        } else {
                            const row = page.locator('[data-task-id="task1"]').first();
                            await row.getByRole('button', { name: '수정', exact: true }).click();
                            await row.locator('.hub-calendar-date-time-field-button').click();
                        }
                        await page.getByRole('dialog', { name: '마감 기한', exact: true }).waitFor();
                    }, () => inspectPopup(page));
                }
                for (const [name, filename, canReplace] of [
                    ['single-conflict', '2026-10-18_기획팀_서비스개선제안.pdf', true],
                    ['multiple-conflict', '2026-10-19_경영지원팀_분기운영계획.pdf', false]
                ]) {
                    await check(page, width, name, async () => {
                        await navigate(page, '/upload');
                        await page.locator('.hub-browser-root-select').click();
                        await page.getByRole('option', { name: '경영지원팀 자료', exact: true }).click();
                        await page.locator('input[type=file]').setInputFiles({ name: filename, mimeType: 'application/pdf', buffer: Buffer.from('%PDF-1.4\nlayout regression fixture') });
                        await page.getByRole('button', { name: '파일 올리기', exact: true }).click();
                        const panel = page.locator('.hub-file-conflict-panel');
                        await panel.waitFor();
                        assert.equal(await panel.getByRole('button', { name: '기존 파일 교체', exact: true }).isEnabled(), canReplace);
                        assert(await panel.getByRole('button', { name: '새 파일로 추가', exact: true }).isEnabled());
                        await panel.evaluate(element => scrollBy(0, element.getBoundingClientRect().top - 100));
                    }, () => inspectConflict(page.locator('.hub-file-conflict-panel')));
                }
            } finally {
                await context.close();
            }
            if (process.env.HUB_APPROVER_USER && process.env.HUB_LAYOUT_AREA !== 'popup') {
                const approvalContext = await browser.newContext(contextOptions);
                try {
                    const page = await approvalContext.newPage();
                    page.setDefaultTimeout(10000);
                    page.on('pageerror', error => errors.push({ width, message: error.message }));
                    page.on('console', message => { if (message.type() === 'error') errors.push({ width, message: message.text() }); });
                    await login(page, process.env.HUB_APPROVER_USER);
                    for (const mode of ['normal', 'approval-multiple']) {
                        const panel = page.locator('.hub-approval-card').filter({ hasText: '신규 고객 온보딩 프로세스 개선안' }).locator('.hub-file-conflict-panel');
                        await check(page, width, `approval-${mode}-conflict`, async () => {
                            assert((await page.request.post(baseUrl + '/fixture/state/' + mode)).ok());
                            await navigate(page, '/approvals/inbox');
                            const card = page.locator('.hub-approval-card').filter({ hasText: '신규 고객 온보딩 프로세스 개선안' });
                            await card.getByRole('button', { name: '승인', exact: true }).click();
                            await panel.waitFor();
                            assert.equal(await panel.getByRole('button', { name: '기존 파일 교체', exact: true }).isEnabled(), mode === 'normal');
                            assert(await panel.getByRole('button', { name: '새 파일로 추가', exact: true }).isEnabled());
                            await panel.evaluate(element => scrollBy(0, element.getBoundingClientRect().top - 100));
                        }, () => inspectConflict(panel));
                    }
                    assert((await page.request.post(baseUrl + '/fixture/state/normal')).ok());
                } finally {
                    await approvalContext.close();
                }
            }
        }
    } finally {
        await browser.close();
        fs.writeFileSync(path.join(output, 'results.json'), JSON.stringify({ results, errors }, null, 2));
    }
    console.log(JSON.stringify({ passed: results.filter(result => result.status === 'pass').length, failed: results.filter(result => result.status === 'fail'), errors }, null, 2));
    assert.equal(results.filter(result => result.status === 'fail').length + errors.length, 0);
})();
