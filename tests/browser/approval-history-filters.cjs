const assert = require('node:assert/strict');
const fs = require('node:fs/promises');
const os = require('node:os');
const path = require('node:path');
const playwrightModule = process.env.PLAYWRIGHT_MODULE || 'playwright';
const { chromium } = require(playwrightModule);
const { expect } = require(playwrightModule + '/test');

async function main() {
    const baseUrl = process.env.HUB_TEST_URL;
    const username = process.env.HUB_TEST_USERNAME;
    const password = process.env.HUB_TEST_PASSWORD;
    assert.ok(baseUrl && username && password, 'Set HUB_TEST_URL, HUB_TEST_USERNAME and HUB_TEST_PASSWORD for an isolated test account.');
    const launchOptions = { headless: true };
    if (process.env.PLAYWRIGHT_EXECUTABLE) {
        launchOptions.executablePath = process.env.PLAYWRIGHT_EXECUTABLE;
    }

    const browser = await chromium.launch(launchOptions);
    try {
        const context = await browser.newContext({ viewport: { width: 1440, height: 1000 } });
        const page = await context.newPage();
        const errors = [];
        page.on('pageerror', error => errors.push(error.message));
        page.on('console', message => {
            if (message.type() === 'error') {
                errors.push(message.text());
            }
        });
        await page.goto(new URL('/login', baseUrl).href);
        await page.waitForLoadState('networkidle');
        await page.getByRole('textbox', { name: '아이디', exact: true }).fill(username);
        await page.getByLabel('비밀번호', { exact: true }).fill(password);
        await page.getByRole('button', { name: '로그인', exact: true }).click();
        await page.waitForURL('**/dashboard');
        await page.getByRole('link', { name: '결재 기록', exact: true }).click();
        await page.waitForLoadState('networkidle');
        await expect(page).toHaveTitle('결재 기록 · Organization Hub');
        await expect(page.getByRole('heading', { name: '결재 기록', exact: true })).toBeAttached();
        await page.getByRole('button', { name: '상태·기간 상세 검색', exact: true }).click();
        await page.getByLabel('상태', { exact: true }).locator('..').click();
        await page.getByRole('option', { name: '반려', exact: true }).click();
        await expect(page.getByLabel('상태', { exact: true })).toHaveValue('Rejected');
        const allHistoryButton = page.locator('a,button').filter({ hasText: /^전체 기록$/ });
        await allHistoryButton.click();
        await expect(page.getByLabel('상태', { exact: true })).toHaveValue('AllRelated');
        await page.locator('a,button').filter({ hasText: /^내 진행 중 요청/ }).click();
        await expect(page).toHaveURL(/view=requested/);
        await expect(page.getByLabel('상태', { exact: true })).toHaveValue('MyOngoing');
        await allHistoryButton.click();
        await expect(page.getByLabel('상태', { exact: true })).toHaveValue('AllRelated');
        assert.equal(new URL(page.url()).searchParams.has('view'), false);

        await page.setViewportSize({ width: 390, height: 844 });
        await page.waitForFunction(() => document.querySelector('.hub-container').getBoundingClientRect().width > innerWidth - 48);
        assert.equal(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth), true);
        assert.equal(await page.locator('#blazor-error-ui').isVisible(), false);
        const outputDirectory = await fs.mkdtemp(path.join(os.tmpdir(), 'organization-hub-history-'));
        await page.screenshot({ path: path.join(outputDirectory, 'history-mobile.png') });
        assert.deepEqual(errors, []);
        console.log('PASS history status reset, quick filters, URL, title, mobile layout and browser errors.');
        console.log('Screenshot: ' + outputDirectory);
    } finally {
        await browser.close();
    }
}

main().catch(error => {
    console.error(error);
    process.exitCode = 1;
});
