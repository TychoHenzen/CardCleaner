// Renders page.svg to a full-resolution page.png, the deliverable.
import os from 'node:os';
import path from 'node:path';
import { fileURLToPath, pathToFileURL } from 'node:url';

const root = path.join(path.dirname(fileURLToPath(import.meta.url)), '..');
// The one-page-design skill's scripts folder; ONE_PAGE_DESIGN_SCRIPTS overrides the default install path.
const scripts = process.env.ONE_PAGE_DESIGN_SCRIPTS ||
  path.join(os.homedir(), '.claude', 'skills', 'one-page-design', 'scripts');
const { launch } = await import(pathToFileURL(path.join(scripts, 'browser.mjs')).href);

const browser = await launch();
const page = await browser.newPage({ deviceScaleFactor: 1 });
await page.goto(pathToFileURL(path.join(root, 'page.svg')).href);
const meta = await page.evaluate(() => JSON.parse(document.getElementById('layout').textContent));
await page.setViewportSize({ width: Math.ceil(meta.page.width), height: Math.ceil(meta.page.height) });
await page.evaluate(() => document.fonts.ready);
await page.screenshot({ path: path.join(root, 'page.png'), clip: { x: 0, y: 0, width: meta.page.width, height: meta.page.height } });
await browser.close();
console.log('page.png', Math.ceil(meta.page.width), 'x', Math.ceil(meta.page.height));
