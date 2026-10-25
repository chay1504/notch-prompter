import {test, expect, chromium} from '@playwright/test';
import {mkdtemp, cp, readFile, writeFile, rm} from 'node:fs/promises';
import os from 'node:os';
import path from 'node:path';
import {fileURLToPath} from 'node:url';

test('Packaged extension: editing, injection, safe formatting, countdown, playback, resize, pause and reload', async ({}, testInfo) => {
  const temporary = await mkdtemp(path.join(os.tmpdir(), 'notchprompter-test-'));
  const extension = path.join(temporary,'extension');
  const source = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..');
  // Host access only for the hermetic test page substitutes for clicking the real toolbar.
  // The shipping manifest is separately checked to contain no host permissions.
  await cp(source, extension, {recursive:true, filter:file => !file.includes(path.sep+'tests')});
  const manifest = JSON.parse(await readFile(path.join(extension,'manifest.json'),'utf8'));
  manifest.host_permissions = ['http://127.0.0.1/*'];
  await writeFile(path.join(extension,'manifest.json'), JSON.stringify(manifest));
  const context = await chromium.launchPersistentContext(path.join(temporary,'profile'), {
    channel:'chromium', headless:true, viewport:{width:1200,height:900},
    args:[`--disable-extensions-except=${extension}`,`--load-extension=${extension}`]
  });
  try {
    const worker = context.serviceWorkers()[0] || await context.waitForEvent('serviceworker');
    const extensionId = new URL(worker.url()).host;
    const page = await context.newPage();
    const errors = []; page.on('pageerror',error=>errors.push(error.message));
    await page.goto('http://127.0.0.1:8794');
    const tabId = await worker.evaluate(async () => (await chrome.tabs.query({})).find(tab=>tab.url==='http://127.0.0.1:8794/').id);
    const editor = await context.newPage();
    await editor.goto(`chrome-extension://${extensionId}/editor.html?full=1&tab=${tabId}`);
    const script = '# Hook\nHello *world*. తెలుగు స్క్రిప్ట్\n<img src=x onerror=alert(1)>\n\n' + 'Keep your eyes towards the camera and speak at your own pace.\n'.repeat(15);
    await editor.getByLabel('Your script',{exact:true}).fill(script);
    await expect.poll(()=>worker.evaluate(async()=> (await chrome.storage.local.get('script')).script)).toBe(script);
    await editor.getByRole('button',{name:'Show on this tab',exact:true}).click();
    await expect(editor.getByRole('status')).toHaveText('Ready on your selected tab.');
    await page.bringToFront();
    const host = page.locator('notch-prompter-overlay');
    await expect(host).toHaveCount(1);
    await expect(host.locator('.island')).toBeVisible();
    await expect(host.locator('mark')).toHaveText('world');
    await expect(host.locator('.script')).toContainText('<img src=x onerror=alert(1)>');
    await expect(host.locator('img')).toHaveCount(0);
    expect(await page.evaluate(()=>typeof globalThis.NotchPrompterCore)).toBe('undefined');
    await host.getByRole('button',{name:'Play or pause',exact:true}).click();
    await expect(host.locator('.counter')).toBeVisible();
    await expect(host.locator('.island')).toHaveAttribute('data-offset',/^[1-9]/,{timeout:6500});
    await host.getByRole('button',{name:'Play or pause',exact:true}).click();
    await expect(host.locator('.island')).toHaveAttribute('data-playing','false');
    await host.getByRole('button',{name:'Faster',exact:true}).click();
    await expect.poll(()=>worker.evaluate(async()=> (await chrome.storage.local.get('wpm')).wpm)).toBe(150);
    await host.getByRole('button',{name:'Resize teleprompter. Use arrow keys; Shift plus arrows changes width.',exact:true}).press('ArrowDown');
    await expect.poll(()=>worker.evaluate(async()=> (await chrome.storage.local.get('height')).height)).toBe(240);
    await host.getByRole('button',{name:'Back to top',exact:true}).click();
    await expect(host.locator('.island')).toHaveAttribute('data-offset','0');
    await page.screenshot({path:testInfo.outputPath('extension-overlay.png')});
    await editor.screenshot({path:testInfo.outputPath('extension-editor.png')});
    await worker.evaluate(async id=> {await chrome.storage.local.set({countdown:false}); await control(id,'play');},tabId);
    await expect(host.locator('.island')).toHaveAttribute('data-playing','true');
    await editor.bringToFront();
    await expect(host.locator('.island')).toHaveAttribute('data-playing','false');
    await page.bringToFront();
    await worker.evaluate(id=>control(id,'show'),tabId);
    await expect(host).toHaveCount(1);
    await worker.evaluate(id=>control(id,'hide'),tabId);
    await expect(host).toBeHidden();
    await page.getByLabel('Page input').fill('The page still works');
    await page.reload();
    await worker.evaluate(id=>control(id,'show'),tabId);
    await expect(host.locator('.script')).toContainText('తెలుగు స్క్రిప్ట్');
    await editor.reload();
    await expect(editor.getByLabel('Your script',{exact:true})).toHaveValue(script);
    await expect(editor.locator('#wpm')).toHaveValue('150');
    const blocked = await context.newPage();
    await blocked.goto('chrome://version');
    const blockedId = await worker.evaluate(async()=> (await chrome.tabs.query({})).find(tab=>tab.active).id);
    const failure = await worker.evaluate(async id=> {try{await control(id,'show');return '';}catch(error){return error.message;}},blockedId);
    expect(failure).toContain('normal website');
    expect(errors).toEqual([]);
  } finally {
    await context.close(); await rm(temporary,{recursive:true,force:true});
  }
});
