const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const playwrightModule = process.env.PLAYWRIGHT_MODULE_PATH || 'playwright';
const { chromium } = require(playwrightModule);
const { expect } = require(playwrightModule + '/test');

const baseUrl = process.env.HUB_TEST_URL;
const outputDirectory = process.env.HUB_SCREENSHOT_DIR;
const password = process.env.HUB_TEST_PASSWORD;
assert(baseUrl && outputDirectory && password && process.env.HUB_TEST_USER);
fs.mkdirSync(outputDirectory, { recursive: true });

const emptyMessage = '조건에 맞는 사용자가 없습니다. 검색어나 필터를 변경해 주세요.';
const profiles = [
    { width: 1440, height: 1000 },
    { width: 1920, height: 1080 },
    { width: 480, height: 1040, mobile: true },
    { width: 360, height: 800, mobile: true },
    { width: 720, height: 1040, mobile: true },
    { width: 721, height: 1040 }
];
const accounts = [process.env.HUB_TEST_USER, process.env.HUB_MANAGER_USER].filter(Boolean);
const results = [];
const errors = [];

async function navigate(page, route) {
    await page.goto(new URL(route, baseUrl).href, { waitUntil: 'networkidle' });
    await page.evaluate(() => document.fonts.ready);
    if (await page.locator('.hub-main').count()) {
        await page.waitForFunction(() => [...document.querySelectorAll('*')].some(element => element.getAttributeNames().some(name => name.startsWith('_bl_'))));
    }
    await page.waitForTimeout(250);
}

async function login(page, username) {
    await navigate(page, '/login');
    await page.waitForTimeout(1500);
    await page.getByLabel('아이디', { exact: true }).fill(username);
    await page.getByLabel('비밀번호', { exact: true }).fill(password);
    await page.getByRole('button', { name: '로그인', exact: true }).click();
    await page.waitForURL('**/dashboard');
}

async function selectFilter(page, label, option) {
    await page.locator('.mud-select').filter({ has: page.getByText(label, { exact: true }) }).first().click();
    await page.getByRole('option', { name: option, exact: true }).click();
}

async function check(page, profile, username, name, action, isEmpty) {
    try {
        await action();
        await page.waitForTimeout(250);
        await expect(page).toHaveTitle('사용자 관리 · Organization Hub');
        await expect(page.locator('#blazor-error-ui')).not.toBeVisible();
        await expect(page.locator('#components-reconnect-modal')).not.toBeVisible();
        const geometry = await page.evaluate(message => {
            const card = document.querySelector('.hub-user-directory .hub-list-card');
            const text = [...card.querySelectorAll('p')].find(element => element.textContent === message);
            const bounds = card.getBoundingClientRect();
            const tableBounds = card.querySelector('.mud-table-container').getBoundingClientRect();
            const measurement = {
                clientWidth: document.documentElement.clientWidth,
                scrollWidth: document.documentElement.scrollWidth,
                cardLeft: bounds.left,
                cardRight: bounds.right,
                tableLeft: tableBounds.left,
                tableRight: tableBounds.right
            };
            if (text) {
                const cell = text.closest('td, th');
                const textBounds = text.getBoundingClientRect();
                const cellBounds = cell.getBoundingClientRect();
                measurement.empty = {
                    textLeft: textBounds.left,
                    textRight: textBounds.right,
                    cellLeft: cellBounds.left,
                    cellRight: cellBounds.right,
                    textOverflow: text.scrollWidth > text.clientWidth + 1,
                    rowClass: cell.closest('tr').className,
                    cellClass: cell.className
                };
            }
            return measurement;
        }, emptyMessage);
        const screenshot = path.join(outputDirectory, `${profile.width}-${username}-${name}.png`);
        await page.screenshot({ path: screenshot, animations: 'disabled' });
        if (isEmpty) {
            await page.screenshot({ path: screenshot.replace('.png', '-full.png'), fullPage: true, animations: 'disabled' });
        }
        assert(geometry.scrollWidth <= geometry.clientWidth + 1, JSON.stringify(geometry));
        assert(geometry.tableLeft >= geometry.cardLeft - 1, JSON.stringify(geometry));
        assert(geometry.tableRight <= geometry.cardRight + 1, JSON.stringify(geometry));
        if (isEmpty) {
            assert(geometry.empty, 'The full empty-state message must remain present.');
            if (profile.mobile) {
                assert(geometry.empty.cellLeft >= geometry.tableLeft - 1, JSON.stringify(geometry));
                assert(geometry.empty.cellRight <= geometry.tableRight + 1, JSON.stringify(geometry));
            }
            assert(geometry.empty.textLeft >= geometry.empty.cellLeft - 1, JSON.stringify(geometry));
            assert(geometry.empty.textRight <= geometry.empty.cellRight + 1, JSON.stringify(geometry));
            assert.equal(geometry.empty.textOverflow, false, JSON.stringify(geometry));
        }
        results.push({ width: profile.width, username, name, status: 'pass', geometry, screenshot });
    } catch (error) {
        await page.screenshot({ path: path.join(outputDirectory, `${profile.width}-${username}-${name}-failure.png`), fullPage: true });
        fs.writeFileSync(path.join(outputDirectory, `${profile.width}-${username}-${name}-failure.txt`), await page.locator('body').innerText());
        results.push({ width: profile.width, username, name, status: 'fail', error: error.message });
    }
}

async function verifyAccount(browser, profile, username) {
    const context = await browser.newContext({
        viewport: { width: profile.width, height: profile.height },
        deviceScaleFactor: profile.mobile ? 3 : 1,
        isMobile: Boolean(profile.mobile),
        hasTouch: Boolean(profile.mobile),
        locale: 'ko-KR',
        timezoneId: 'Asia/Seoul'
    });
    const page = await context.newPage();
    page.on('pageerror', error => errors.push(error.message));
    page.on('console', message => {
        if (message.type() === 'error') errors.push(message.text());
    });
    try {
        await login(page, username);
        await navigate(page, '/admin/users');
        const rows = page.locator('.hub-admin-table tbody tr').filter({ has: page.locator('[data-label]') });
        const initialCount = await rows.count();
        assert(initialCount > 1, 'Use the isolated multi-account fixture.');
        const search = page.getByRole('textbox', { name: '사용자 검색', exact: true });
        await check(page, profile, username, 'normal-list', async () => {
            await expect(page.getByText('ux-member', { exact: true })).toBeVisible();
        }, false);
        await check(page, profile, username, 'empty-search', async () => {
            await search.fill('등록되지않은사용자');
            await expect(page.getByText(emptyMessage, { exact: true })).toBeVisible();
            await expect(rows).toHaveCount(0);
        }, true);
        await check(page, profile, username, 'search-cleared', async () => {
            await search.fill('');
            await expect(rows).toHaveCount(initialCount);
            await expect(page.getByText(emptyMessage, { exact: true })).toHaveCount(0);
        }, false);
        await check(page, profile, username, 'username-search', async () => {
            await search.fill('UX-MEMBER');
            await expect(rows).toHaveCount(1);
            await expect(page.getByText('ux-member', { exact: true })).toBeVisible();
        }, false);
        await check(page, profile, username, 'empty-status-filter', async () => {
            await selectFilter(page, '활성 상태 필터', '비활성');
            await expect(page.getByText(emptyMessage, { exact: true })).toBeVisible();
            await expect(rows).toHaveCount(0);
        }, true);
        await check(page, profile, username, 'active-filter-restored', async () => {
            await selectFilter(page, '활성 상태 필터', '활성');
            await expect(rows).toHaveCount(1);
        }, false);
        await check(page, profile, username, 'empty-team-filter', async () => {
            await selectFilter(page, '소속 팀 필터', '서비스기획팀');
            await expect(page.getByText(emptyMessage, { exact: true })).toBeVisible();
            await expect(rows).toHaveCount(0);
        }, true);
        await check(page, profile, username, 'display-name-and-edit', async () => {
            await selectFilter(page, '소속 팀 필터', '경영지원팀');
            await search.fill('이민준');
            await expect(rows).toHaveCount(1);
            await rows.getByRole('button', { name: /사용자 수정$/ }).click();
            await expect(page.getByRole('textbox', { name: '표시 이름 (필수)', exact: true })).toHaveValue('이민준');
            await expect(page.getByRole('textbox', { name: '새 비밀번호', exact: true })).toBeVisible();
        }, false);
    } finally {
        await context.close();
    }
}

async function main() {
    const launchOptions = { headless: true };
    if (process.env.HUB_BROWSER_EXECUTABLE) launchOptions.executablePath = process.env.HUB_BROWSER_EXECUTABLE;
    const browser = await chromium.launch(launchOptions);
    try {
        for (const profile of profiles) {
            if (process.env.HUB_TEST_WIDTHS && !process.env.HUB_TEST_WIDTHS.split(',').includes(String(profile.width))) continue;
            for (const username of accounts) await verifyAccount(browser, profile, username);
        }
    } finally {
        await browser.close();
        fs.writeFileSync(path.join(outputDirectory, 'results.json'), JSON.stringify({ results, errors }, null, 2));
    }
    assert(results.length > 0);
    assert.deepEqual(results.filter(result => result.status === 'fail'), []);
    assert.deepEqual(errors, []);
    console.log(`PASS ${results.length} user directory search, filter, editor and layout checks.`);
}

main().catch(error => {
    console.error(error);
    process.exitCode = 1;
});
