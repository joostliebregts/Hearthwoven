// Renders the feat scenes (src/Panel/vocab-src/feats/<id>.svg) into the vocabulary sprites the panel embeds:
//   src/Panel/vocab/feat-scene-<id>.png        112 px, the feat's card and its detail (drawn at 56 px: two texels a pixel)
// (0.9.2: look 1 is gone, and with it the 72 px small render with heavier lines and no fine detail.)
// White line on transparent, fills in greys: PanelLook.GoldLine multiplies it by the feat gold; Unsung tints it with its dimmed ink.
// Usage: node src/Panel/vocab-src/feats/render.mjs   (PLAYWRIGHT_MJS and CHROMIUM may point at a local Playwright and its browser)
import { readdirSync, readFileSync, writeFileSync } from "node:fs";
import { dirname, join } from "node:path";
import { fileURLToPath } from "node:url";

const here = dirname(fileURLToPath(import.meta.url));
const out = join(here, "..", "..", "vocab");
const LARGE = 112;
const pw = process.env.PLAYWRIGHT_MJS || "playwright";   // set PLAYWRIGHT_MJS to a playwright index.mjs when it is not installed here
const exe = process.env.CHROMIUM || process.env.HOME + "/Library/Caches/ms-playwright/chromium-1148/chrome-mac/Chromium.app/Contents/MacOS/Chromium";

const { chromium } = await import(pw);
const browser = await chromium.launch({ executablePath: exe });
const page = await browser.newPage({ deviceScaleFactor: 1 });
const ids = readdirSync(here).filter(f => f.endsWith(".svg")).map(f => f.slice(0, -4)).sort();
async function shot(svg, px, file) {
  const sized = svg.replace("<svg ", `<svg width="${px}" height="${px}" `);
  await page.setContent(`<html><body style="margin:0;background:transparent">${sized}</body></html>`);
  writeFileSync(file, await page.locator("svg").screenshot({ omitBackground: true }));
}
for (const id of ids) {
  const svg = readFileSync(join(here, id + ".svg"), "utf8");
  await shot(svg, LARGE, join(out, `feat-scene-${id}.png`));
}
await browser.close();
console.log(`rendered ${ids.length} feat scenes (${LARGE} px) into ${out}`);
