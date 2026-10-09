// Renders every preview in preview-data.js to preview-<name>.png and MEASURES every visible text run.
// Any text rendered below the floor (PanelLook.MinText, CSS --mintext) is a violation; the run exits 1 if there are any
// (unless --no-fail). Usage: node render-check.mjs <previewDir> [--only battle] [--min 14] [--strict-cut] [--no-fail] [--no-png] [--json out.json]
// Playwright is found via PLAYWRIGHT_MJS (default: the playwright package resolvable from here); Chromium via CHROMIUM_EXE.
import { readFileSync, writeFileSync } from "fs";
import { resolve } from "path";
const args = process.argv.slice(2);
const dir = resolve(args.find((a, i) => !a.startsWith("--") && !["--only", "--json", "--min"].includes(args[i - 1])));
const flag = (f) => args.includes(f);
const val = (f) => { const i = args.indexOf(f); return i >= 0 ? args[i + 1] : null; };
// the floor: --min N, else PanelLook.MinText read from the C# source (single source of truth); the HTML --mintext must agree with it
const csSrc = (() => { try { return readFileSync(`${dir}/../PanelLook.cs`, "utf8"); } catch { return ""; } })();
const csMin = parseFloat((csSrc.match(/MinText\s*=\s*([\d.]+)f/) || [])[1]);
const htmlMin = parseFloat(((readFileSync(`${dir}/panel-preview.html`, "utf8")).match(/--mintext:\s*([\d.]+)px/) || [])[1]);
const MIN = val("--min") ? parseFloat(val("--min")) : (csMin || 13);
if (csMin && htmlMin && csMin !== htmlMin) { console.log(`FAIL: PanelLook.MinText = ${csMin} but the preview --mintext = ${htmlMin}; the Unity panel and the HTML preview must share one floor`); process.exit(1); }
const { chromium } = await import(process.env.PLAYWRIGHT_MJS || "playwright");
const src = readFileSync(`${dir}/preview-data.js`, "utf8");
let names = [...src.matchAll(/^  "([^"]+)": \{/gm)].map(m => m[1]);
const only = val("--only"); if (only) names = names.filter(n => n.includes(only));
const browser = await chromium.launch({ executablePath: process.env.CHROMIUM_EXE || `${process.env.HOME}/Library/Caches/ms-playwright/chromium-1148/chrome-mac/Chromium.app/Contents/MacOS/Chromium` });
const page = await browser.newPage({ viewport: { width: 1280, height: 860 } });
page.on("pageerror", e => console.log("PAGE ERROR", e.message));

// runs in the page: every visible text run with its rendered size (computed font-size x the transform scale of its ancestors)
const measure = (MIN) => {
  const out = [];
  const cssPath = (el) => { const parts = []; for (let e = el; e && e !== document.body && parts.length < 4; e = e.parentElement) {
    let s = e.tagName.toLowerCase(); if (e.id) s += "#" + e.id; const c = (e.getAttribute("class") || "").trim().split(/\s+/).filter(Boolean).slice(0, 2); if (c.length) s += "." + c.join("."); parts.unshift(s); } return parts.join(" > "); };
  const clipRects = (el) => { const rs = []; for (let e = el; e && e !== document.documentElement; e = e.parentElement) { const cs = getComputedStyle(e);
    if (cs.overflowX !== "visible" || cs.overflowY !== "visible") rs.push(e.getBoundingClientRect()); } return rs; };
  const w = document.createTreeWalker(document.body, NodeFilter.SHOW_TEXT);
  for (let n = w.nextNode(); n; n = w.nextNode()) {
    const txt = n.nodeValue.replace(/\s+/g, " ").trim(); if (!txt) continue;
    const el = n.parentElement; if (!el || /^(SCRIPT|STYLE|TITLE|HEAD)$/.test(el.tagName) || el.closest("#caption")) continue;   // #caption = the preview page's own footnote, not panel text
    const cs = getComputedStyle(el); if (cs.visibility === "hidden" || cs.display === "none") continue;
    let op = 1; for (let e = el; e; e = e.parentElement) op *= parseFloat(getComputedStyle(e).opacity); if (op < 0.05) continue;
    const range = document.createRange(); range.selectNodeContents(n); const rects = [...range.getClientRects()].filter(r => r.width > 0.5 && r.height > 0.5);
    if (!rects.length) continue;
    let r = rects[0];
    if (r.right <= 0 || r.bottom <= 0 || r.left >= innerWidth || r.top >= innerHeight) continue;
    if (clipRects(el).some(c => r.right <= c.left || r.left >= c.right || r.bottom <= c.top || r.top >= c.bottom)) continue;   // clipped away entirely
    const col = cs.color.match(/[\d.]+/g) || []; if (col.length > 3 && parseFloat(col[3]) === 0) continue;
    // rendered size: font-size x accumulated scale (rect height of the run vs. its unscaled line box)
    let scale = 1; for (let e = el; e && e !== document.documentElement; e = e.parentElement) { const t = getComputedStyle(e).transform; if (t && t !== "none") { const m = new DOMMatrix(t); scale *= Math.sqrt(Math.abs(m.a * m.d - m.b * m.c)) || 1; } }   // transform scale of the ancestors; zoom is not used
    const fs = parseFloat(cs.fontSize), eff = fs * scale;
    // text cut off by its own box: the nearest ancestor that clips (overflow hidden) is narrower/shorter than its content ("Best arrow" read "Arrow Carapa")
    let cut = false; for (let e = el, k = 0; e && e !== document.body && k < 4; e = e.parentElement, k++) { const c = getComputedStyle(e);
      if (c.overflowX !== "visible" && (c.textOverflow === "ellipsis" || c.whiteSpace.startsWith("nowrap")) && e.scrollWidth > e.clientWidth + 1) { cut = true; break; }   // a one-line box that holds less than its words
      if (c.overflowY !== "visible" && (c.webkitLineClamp !== "none" || c.display === "-webkit-box") && e.scrollHeight > e.clientHeight + 1) { cut = true; break; } }   // a line-clamped box that holds fewer lines than the text
    out.push({ text: txt.slice(0, 40), fs, eff: Math.round(eff * 100) / 100, h: Math.round(r.height * 10) / 10, sel: cssPath(el), x: Math.round(r.left), y: Math.round(r.top), cut });
  }
  return out;
};

// the Fireside's parts must not touch each other (fix4-rest: "no overlaps anywhere"): the count pills, the shields, the names (their text) and the hearth, as rendered
const collide = () => {
  const box = (el) => { const r = el.getBoundingClientRect(); return { l: r.left, t: r.top, r: r.right, b: r.bottom }; };
  const items = [];
  for (const el of document.querySelectorAll("[data-fire]")) {
    const kind = el.dataset.fire, inner = kind === "name" ? el.querySelector("span") || el : el, r = box(inner);
    if (r.r - r.l < 1) continue;
    items.push({ kind, text: (el.textContent || "").trim().slice(0, 12), ...r });
  }
  const hearth = document.querySelector('img[src$="hearth-fire.png"]');
  if (hearth && items.length) { const r = box(hearth); items.push({ kind: "hearth", text: "hearth", l: r.l + 14, t: r.t + 14, r: r.r - 14, b: r.b - 14 }); }   // the sprite's soft rim is not the hearth
  const hits = [];
  for (let i = 0; i < items.length; i++) for (let j = i + 1; j < items.length; j++) {
    const a = items[i], b = items[j]; if (a.kind === b.kind && a.kind !== "pill") continue;
    const w = Math.min(a.r, b.r) - Math.max(a.l, b.l), h = Math.min(a.b, b.b) - Math.max(a.t, b.t);
    if (w > 0.5 && h > 0.5) hits.push(`${a.kind} "${a.text}" x ${b.kind} "${b.text}"`);
  }
  return hits;
};
const rows = [], perPage = {}, cuts = [], overlaps = [], fireDiffs = []; let fireCompared = 0;
for (const n of names) {
  await page.goto(`file://${dir}/panel-preview.html?view=${n}`);
  await page.evaluate(() => document.fonts.ready); await page.waitForTimeout(400);
  // every picture the page uses (img, and the CSS backgrounds and masks the threads are drawn with) must be loaded and decoded before the shot, or two renders of one page differ
  await page.evaluate(async () => {
    const urls = new Set(); for (const el of document.querySelectorAll("*")) { const cs = getComputedStyle(el); for (const v of [cs.backgroundImage, cs.maskImage, cs.webkitMaskImage]) for (const m of (v || "").matchAll(/url\(["']?([^"')]+)["']?\)/g)) urls.add(m[1]); if (el.tagName === "IMG" && el.src) urls.add(el.src); }
    await Promise.all([...urls].map(u => new Promise(res => { const i = new Image(); i.onload = i.onerror = () => res(); i.src = u; })));
    await Promise.all([...document.images].map(i => i.decode().catch(() => {})));   // the page's own pictures, decoded at their drawn size
    await new Promise(r => requestAnimationFrame(() => requestAnimationFrame(r)));
  });
  await page.waitForTimeout(150);
  if (!flag("--no-png")) { await page.screenshot({ path: `${dir}/preview-${n}.png` }); await page.waitForTimeout(120); await page.screenshot({ path: `${dir}/preview-${n}.png` }); }   // twice: the first shot settles the SVG-filtered icons, the second is the one kept (two renders of a page come out byte-identical)
  for (const h of await page.evaluate(collide)) overlaps.push({ page: n, what: h });
  // the Fireside: the HTML port's plan must be the C# FireLayout's (the dump wrote it to window.FIRE_PLANS): the same arcs, sides, ends (0.5 px) and count places
  const fire = await page.evaluate((n) => ({ cs: (window.FIRE_PLANS || {})[n] || null, js: (window.FIRE_GEOM || [])[0] || null }), n);
  if (fire.cs || fire.js) {
    fireCompared++;
    const diff = [];
    if (!fire.cs || !fire.js) diff.push(fire.cs ? "the HTML drew no Fireside" : "the dump wrote no plan");
    else {
      if (fire.cs.threads.length !== fire.js.threads.length) diff.push(`threads ${fire.cs.threads.length} vs ${fire.js.threads.length}`);
      fire.cs.threads.forEach((t, i) => { const u = fire.js.threads[i]; if (!u || u.gift !== t.gift || u.arc !== t.arc || u.bend !== t.bend || [0, 1].some(k => Math.abs(u.p0[k] - t.p0[k]) > 0.5 || Math.abs(u.p1[k] - t.p1[k]) > 0.5)) diff.push(`thread ${t.gift}`); });
      if (fire.cs.pills.length !== fire.js.pills.length) diff.push(`counts ${fire.cs.pills.length} vs ${fire.js.pills.length}`);
      fire.cs.pills.forEach((p, i) => { const q = fire.js.pills[i]; if (!q || q.gift !== p.gift || Math.abs(q.u - p.u) > 0.011 || q.compact !== p.compact || q.dropped !== p.dropped) diff.push(`count ${p.gift}`); });
    }
    if (diff.length) fireDiffs.push({ page: n, what: diff.slice(0, 4).join(", ") });
  }
  const runs = await page.evaluate(measure, MIN);
  const bad = runs.filter(r => r.eff < MIN - 0.01);
  perPage[n] = { runs: runs.length, bad: bad.length, cut: runs.filter(r => r.cut).length };
  for (const c of runs.filter(r => r.cut)) cuts.push({ page: n, text: c.text, sel: c.sel });
  for (const b of bad) rows.push({ page: n, ...b });
}
await browser.close();
const chapter = (n) => n.startsWith("battle") || n.endsWith("-battle") ? "battle" : (n.split("-")[0].replace(/^(edda|tor|finch)$/, "fellows"));
const byChap = {}; for (const [n, p] of Object.entries(perPage)) { const c = chapter(n); (byChap[c] ??= { pages: 0, runs: 0, bad: 0 }); byChap[c].pages++; byChap[c].runs += p.runs; byChap[c].bad += p.bad; }
// the distinct offenders: selector + size, with the pages they appear on
const groups = {}; for (const r of rows) { const k = `${r.eff}px  ${r.sel}`; (groups[k] ??= { n: 0, pages: new Set(), sample: r.text }).n++; groups[k].pages.add(r.page); }
// text truncated by its own box, per chapter (information; --strict-cut makes it fail too: a cut-off word hides data)
const cutBy = {}; for (const c of cuts) { const k = chapter(c.page); cutBy[k] = (cutBy[k] || 0) + 1; }
console.log(`rendered ${names.length} previews, ${rows.length} text runs under ${MIN}px`);
console.log("per chapter (pages / text runs / violations):"); for (const [c, v] of Object.entries(byChap)) console.log(`  ${c.padEnd(10)} ${v.pages} / ${v.runs} / ${v.bad}`);
for (const [k, g] of Object.entries(groups).sort((a, b) => b[1].n - a[1].n)) console.log(`  ${String(g.n).padStart(4)}x ${k}   e.g. "${g.sample}"   [${[...g.pages].slice(0, 4).join(", ")}${g.pages.size > 4 ? ", +" + (g.pages.size - 4) : ""}]`);
console.log(`text cut off by its own box (information${flag("--strict-cut") ? ", failing" : ""}): ` + (cuts.length ? Object.entries(cutBy).map(([c, n]) => `${c} ${n}`).join(", ") : "none"));
const cutGroups = {}; for (const c of cuts) { const k = `${c.sel}  "${c.text}"`; (cutGroups[k] ??= new Set()).add(c.page); }
for (const [k, p] of Object.entries(cutGroups).slice(0, 25)) console.log(`   cut: ${k}   [${[...p].slice(0, 3).join(", ")}${p.size > 3 ? ", +" + (p.size - 3) : ""}]`);
console.log(`parts that overlap (the Fireside's counts, shields, names, hearth): ` + (overlaps.length ? overlaps.length : "none"));
for (const o of overlaps.slice(0, 20)) console.log(`   overlap: ${o.page}: ${o.what}`);
if (val("--json")) writeFileSync(val("--json"), JSON.stringify({ min: MIN, byChapter: byChap, violations: rows, truncated: cuts }, null, 1));
console.log(`Fireside plans, HTML port vs C# FireLayout: ${fireCompared} compared, ` + (fireDiffs.length ? fireDiffs.length + " differ" : "all the same"));
for (const d of fireDiffs.slice(0, 10)) console.log(`   differs: ${d.page}: ${d.what}`);
if (overlaps.length && !flag("--no-fail")) { console.log("FAIL: parts overlap"); process.exit(1); }
if (fireDiffs.length && !flag("--no-fail")) { console.log("FAIL: the HTML Fireside is not the C# one"); process.exit(1); }
if (flag("--strict-cut") && cuts.length && !flag("--no-fail")) { console.log("FAIL: text cut off by its own box"); process.exit(1); }
if (rows.length && !flag("--no-fail")) { console.log(`FAIL: text under the ${MIN} px floor`); process.exit(1); }
console.log(`PASS: no text under the ${MIN} px floor`);
