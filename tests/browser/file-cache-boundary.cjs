// Run only against the isolated browser fixture described in README.md.
const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const { chromium } = require(process.env.PLAYWRIGHT_MODULE_PATH || 'playwright');
const base = process.env.HUB_TEST_URL;
const output = process.env.HUB_SCREENSHOT_DIR;
assert(base && output && process.env.HUB_TEST_PASSWORD);
fs.mkdirSync(output, { recursive: true });

async function login(page, account) {
    await page.goto(base + '/login', { waitUntil: 'networkidle' });
    await page.getByLabel('아이디', { exact: true }).fill(account);
    await page.getByLabel('비밀번호', { exact: true }).fill(process.env.HUB_TEST_PASSWORD);
    await page.getByRole('button', { name: '로그인', exact: true }).click();
    await page.waitForURL('**/dashboard');
    await page.waitForTimeout(700);
}

async function read(page, endpoint) {
    return page.evaluate(async endpoint => {
        const response = await fetch('/files/qa-team-file1/' + endpoint);
        const bytes = await response.arrayBuffer();
        return { url: response.url, cacheControl: response.headers.get('cache-control'), prefix: new TextDecoder().decode(bytes.slice(0, 4)) };
    }, endpoint);
}

(async () => {
    const browser = await chromium.launch({ executablePath: process.env.HUB_BROWSER_EXECUTABLE });
    const results = [];
    try {
        for (const [width, height] of [[1440, 1000], [1920, 1080], [480, 1040]]) {
            const context = await browser.newContext({ viewport: { width, height }, isMobile: width === 480, hasTouch: width === 480 });
            try {
                const page = await context.newPage();
                await login(page, 'operations-reader');
                for (const endpoint of ['stream', 'download']) {
                    const authorized = await read(page, endpoint);
                    assert.equal(authorized.prefix, '%PDF');
                }
                await page.getByRole('button', { name: '로그아웃', exact: true }).click();
                await page.waitForURL('**/login**');
                await login(page, 'planning-reader');
                for (const endpoint of ['stream', 'download']) {
                    const denied = await read(page, endpoint);
                    assert.notEqual(denied.prefix, '%PDF', 'Other account received a cached PDF: ' + endpoint);
                    assert(denied.url.endsWith('/access-denied'));
                    results.push({ width, endpoint, denied, status: 'pass' });
                }
            } finally {
                await context.close();
            }
        }
    } finally {
        fs.writeFileSync(path.join(output, 'file-cache-boundary.json'), JSON.stringify(results, null, 2));
        await browser.close();
    }
})().catch(error => { console.error(error); process.exitCode = 1; });
