// Renders every preview in preview-data.js to preview-<name>.png and MEASURES every visible text run.
// Any text rendered below the floor (PanelLook.MinText, CSS --mintext) is a violation; the run exits 1 if there are any
// (unless --no-fail). Usage: node render-check.mjs <previewDir> [--only battle] [--min 14] [--strict-cut] [--no-fail] [--no-png] [--json out.json]
// Playwright is found via PLAYWRIGHT_MJS (default: the playwright package resolvable from here); Chromium via CHROMIUM_EXE.
import { readFileSync, writeFileSync } from "fs";
import { resolve } from "path";
const args = process.argv.slice(2);
const dir = resolve(args.find((a, i) => !a.startsWith("--") && !["--only", "--json", "--min"].includes(args[i - 1])));
const flag = (f) => args.includes(f);
// 0.8.1 US spelling (Joost: "armor, not armour"): the mirror's visible text never holds a British form. The same word list as test-panel/UsSpellingTests.cs
// (the C# source scan); Valheim's own names (Greydwarf, Greyling) are not forms of "grey".
const BRITISH = /\b(?:armour|defence|offence|colour|grey(?!dwarf|ling)|honour|favour|behaviour|neighbour|labour|flavour|harbour|centre|litre)\w*|\w*metres?\b|\b(?:practis|travell|cancell|labell|modell|levell|fuell|signall|marvell|counsell|jewell)(?:e|ed|es|er|ers|ing)\b|\b(?:recogni|organi|summari|customi|categori|normali|visuali|prioriti|minimi|maximi|reali|apologi|speciali|utili|synchroni|optimi|finali|initiali|locali|saniti|authori|personali|standardi|harmoni|fertili|analy|emphasi|memori|capitali|digiti|stabili)s(?:e|ed|es|ing)\b|\b\w+isation\b|\b(?:licence|programme|whilst|amongst|learnt)\b/gi;
const val = (f) => { const i = args.indexOf(f); return i >= 0 ? args[i + 1] : null; };
// the floor: --min N, else PanelLook.MinText read from the C# source (single source of truth); the HTML --mintext must agree with it
const csSrc = (() => { try { return readFileSync(`${dir}/../PanelLook.cs`, "utf8"); } catch { return ""; } })();
const csMin = parseFloat((csSrc.match(/MinText\s*=\s*([\d.]+)f/) || [])[1]);
const htmlMin = parseFloat(((readFileSync(`${dir}/panel-preview.html`, "utf8")).match(/--mintext:\s*([\d.]+)px/) || [])[1]);
const MIN = val("--min") ? parseFloat(val("--min")) : (csMin || 13);
// the one exception (PanelLook.MinText, Joost 2026-10-10): a number inside a damage-bar part, digits only and bold, may be PanelLook.MinBarDigits;
// the preview's --bardigits must agree, and nothing else may go under the floor
const csBarMin = parseFloat((csSrc.match(/MinBarDigits\s*=\s*([\d.]+)f/) || [])[1]);
const htmlBarMin = parseFloat(((readFileSync(`${dir}/panel-preview.html`, "utf8")).match(/--bardigits:\s*([\d.]+)px/) || [])[1]);
const BARMIN = csBarMin || MIN;
if ((csBarMin || htmlBarMin) && csBarMin !== htmlBarMin) { console.log(`FAIL: PanelLook.MinBarDigits = ${csBarMin} but the preview --bardigits = ${htmlBarMin}; the in-bar digits' floor must be one`); process.exit(1); }
if (csMin && htmlMin && csMin !== htmlMin) { console.log(`FAIL: PanelLook.MinText = ${csMin} but the preview --mintext = ${htmlMin}; the Unity panel and the HTML preview must share one floor`); process.exit(1); }
// the game's own drawing (PanelUi and the Chapters' *Ui.cs): a text call with a literal size under the floor (Label, RichLabel, Since). Label
// lifts it to the floor, so the box around it was laid out for text smaller than what is drawn; the preview draws the floor and cannot
// show it (Company's fellow-row label asked for 11 px, MERGE-NOTES G6). Every size a UI file writes must be the floor or more.
import { readdirSync } from "fs";
const uiSmall = [];
for (const sub of ["", "Chapters/"]) {
  let files = []; try { files = readdirSync(`${dir}/../${sub}`).filter(f => f.endsWith(".cs")); } catch { }
  for (const f of files) readFileSync(`${dir}/../${sub}${f}`, "utf8").split("\n").forEach((line, i) => {
    for (const m of line.matchAll(/\b(Label|RichLabel|Since)\((?:[^;]*?), (\d+(?:\.\d+)?)f?(?=[,)])/g))
      if (parseFloat(m[2]) < MIN) uiSmall.push(`${sub}${f}:${i + 1}: ${m[1]}(..., ${m[2]})`);
    // a size set straight on a text (past Label's floor): a literal under the floor never; the in-bar digits' sizes (MinBarDigits, DrDigits) only in
    // PanelUi.BarDigits (Chapters/DamageRowsUi.cs), the one exception, so it cannot spread
    const code = line.replace(/\/\/.*$/, "");   // the code, not its comments
    for (const m of code.matchAll(/fontSize\s*=(?!=)\s*([^;]+)/g))
      if ([...m[1].matchAll(/\b(\d+(?:\.\d+)?)f?\b/g)].some(x => parseFloat(x[1]) < MIN && parseFloat(x[1]) > 0)) uiSmall.push(`${sub}${f}:${i + 1}: fontSize = ${m[1].trim()}`);
    if (/MinBarDigits|DrDigits/.test(code) && !(sub + f === "Chapters/DamageRowsUi.cs" || f === "PanelLook.cs" || f === "PanelCheck.cs")) uiSmall.push(`${sub}${f}:${i + 1}: the in-bar digits' size outside PanelUi.BarDigits`);
  });
}
const { chromium } = await import(process.env.PLAYWRIGHT_MJS || "playwright");
const src = readFileSync(`${dir}/preview-data.js`, "utf8");
let names = [...src.matchAll(/^  "([^"]+)": \{/gm)].map(m => m[1]);
const only = val("--only"); if (only) names = names.filter(n => n.includes(only));
const browser = await chromium.launch({ executablePath: process.env.CHROMIUM_EXE || `${process.env.HOME}/Library/Caches/ms-playwright/chromium-1148/chrome-mac/Chromium.app/Contents/MacOS/Chromium`, args: ["--disable-gpu", "--disable-gpu-rasterization", "--force-color-profile=srgb"] });   // software raster: the GPU path drew the filtered icons a shade apart from run to run (about-sharing flipped in 2 of 6 renders), so branches fought over unchanged PNGs
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
    // text that runs past the side of a box that clips it ("Farming lev" at the plate's right edge, MERGE-NOTES G3): never right, a part is hidden
    const side = rects.some(q => clipRects(el).some(c => q.right > c.right + 1 || q.left < c.left - 1));
    // which of the page's stacked blocks it belongs to (a direct child of the plate or the page's block column): text of two blocks never overlaps
    let blk = -1; for (let e = el; e && e.parentElement; e = e.parentElement) { const p = e.parentElement; if (p.matches(".content > .plate, .content .blocks")) { blk = e.classList.contains("plfade") ? -1 : [...p.children].indexOf(e); break; } }   // .plfade: the fade and the "More below" cue lie over the grid on purpose
    // the one exception: a number inside a damage-bar part (.dr-seg .num), digits only, bold (PanelUi.BarDigits)
    const bar = el.classList.contains("num") && !!el.closest(".dr-seg") && /^[\d\s\u00a0\u2009\u202f]+$/.test(txt) && /\d/.test(txt) && parseInt(cs.fontWeight) >= 600;
    out.push({ text: txt.slice(0, 40), fs, bar, eff: Math.round(eff * 100) / 100, h: Math.round(r.height * 10) / 10, sel: cssPath(el), x: Math.round(r.left), y: Math.round(r.top), cut, side, blk, box: [r.left, r.top + 0.2 * eff, r.right, r.bottom - 0.2 * eff] });   // the ink, not the line box: a big number's line box reaches into the label under it
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
const rows = [], perPage = {}, cuts = [], sides = [], overlaps = [], fireDiffs = [], heroGrowth = [], datesCut = [], british = [], feedSplit = []; let fireCompared = 0;
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
  // the key line never runs past its room (PanelModel.KeyLine leaves keys out instead; Cooking and Company > Together ran past it, MERGE-NOTES G2/G6)
  // 0.8 hover (BarHover, PICKS 4 B): every bar with a list pairs each part with its row (the same data-k on both), so pointing at either lights the other
  const unpaired = await page.evaluate(() => [...document.querySelectorAll(".bf")].filter(bf => { const ks = sel => [...bf.querySelectorAll(sel)].map(e => e.dataset.k).sort().join(","); return ks(".bf-seg[data-k]") !== ks(".bf-tab .r[data-k]"); }).length);
  if (unpaired) overlaps.push({ page: n, what: `${unpaired} bar(s) whose parts and list rows do not pair up for the hover` });
  // 0.8.1 review 1: on the group's rows the name gives way, never the date ("as of 8 Oct 00:27" was cut beside any name longer than "Tor")
  for (const t of await page.evaluate(() => [...document.querySelectorAll(".gr-nm i")].filter(i => i.scrollWidth > i.clientWidth + 1).map(i => i.textContent)))
    datesCut.push({ page: n, what: `"${t}"` });
  // 0.8.1 (Joost sailing, Battle > Feed: "less than" / "1"): a feed line breaks only between items, at " · ": an amount never splits, nor leaves its
  // type or its "dealt" / "received" on the line before
  for (const t of await page.evaluate(() => [...document.querySelectorAll(".fdl b")].filter(b => {
      const r = b.getClientRects(); if (r.length !== 1) return r.length > 1;
      for (let s = b.previousElementSibling; s && !s.textContent.includes("·"); s = s.previousElementSibling) { const q = s.getClientRects(); if (q.length !== 1 || Math.abs(q[0].top - r[0].top) > 2) return true; }
      return false; }).map(b => b.textContent)))
    feedSplit.push({ page: n, what: `"${t}"` });
  // the received words (the Log's column, a card's line) are plain text: each item joined by no-break spaces, so they too break only at " · "
  for (const t of await page.evaluate(() => [...document.querySelectorAll(".bt-fl .rcv, .bt-card .rcv")].flatMap(e => e.textContent.split(" · ")).filter(item => item.includes(" "))))
    feedSplit.push({ page: n, what: `"${t}" (received column)` });
  const keysOver = await page.evaluate(() => { const k = document.querySelector(".keys"); return k && k.scrollWidth > k.clientWidth + 1 ? k.textContent.replace(/\s+/g, " ").trim() : null; });
  if (keysOver) overlaps.push({ page: n, what: `the key line is wider than its room: "${keysOver}"` });
  // 0.8 layout D+ (Joost on the board): the hero's growth line takes no height of its own: each hero is as tall with it as without it
  const taller = await page.evaluate(() => [...document.querySelectorAll(".hero")].map(h => { const sp = h.querySelector(".hspark"); if (!sp || sp.style.display === "none") return null;
    const was = h.offsetHeight; sp.style.display = "none"; const without = h.offsetHeight; sp.style.display = ""; return was > without ? `${was} px with it, ${without} without` : null; }).filter(Boolean));
  for (const t of taller) heroGrowth.push({ page: n, what: t });
  // 0.8 layout D+: one legend grid per page: every legend laid on it, all with the same column count (never mixed)
  const grids = await page.evaluate(() => [...document.querySelectorAll(".bf-list")].filter(l => l.querySelector(".r")).map(l => l.dataset.page || "none"));
  if (new Set(grids).size > 1 || grids.includes("none")) overlaps.push({ page: n, what: `legends not on one grid: ${grids.join(", ")}` });
  // the Fireside: the HTML port's plan must be the C# FireLayout's (the dump wrote it to window.FIRE_PLANS): the same arcs, sides, ends (0.5 px) and count places
  const fire = await page.evaluate((n) => ({ cs: (window.FIRE_PLANS || {})[n] || null, js: (window.FIRE_GEOM || [])[0] || null }), n);
  if (fire.cs || fire.js) {
    fireCompared++;
    const diff = [];
    if (!fire.cs || !fire.js) diff.push(fire.cs ? "the HTML drew no Fireside" : "the dump wrote no plan");
    else {
      if (fire.cs.threads.length !== fire.js.threads.length) diff.push(`threads ${fire.cs.threads.length} vs ${fire.js.threads.length}`);
      fire.cs.threads.forEach((t, i) => { const u = fire.js.threads[i]; if (!u || u.gift !== t.gift || u.arc !== t.arc || u.bend !== t.bend || [0, 1].some(k => Math.abs(u.p0[k] - t.p0[k]) > 0.5 || Math.abs(u.p1[k] - t.p1[k]) > 0.5) || Math.abs((u.w || 0) - (t.w || 0)) > 0.02) diff.push(`thread ${t.gift}`); });
      if (fire.cs.pills.length !== fire.js.pills.length) diff.push(`counts ${fire.cs.pills.length} vs ${fire.js.pills.length}`);
      fire.cs.pills.forEach((p, i) => { const q = fire.js.pills[i]; if (!q || q.gift !== p.gift || Math.abs(q.u - p.u) > 0.011 || q.compact !== p.compact || q.dropped !== p.dropped) diff.push(`count ${p.gift}`); });
    }
    if (diff.length) fireDiffs.push({ page: n, what: diff.slice(0, 4).join(", ") });
  }
  const runs = await page.evaluate(measure, MIN);
  const bad = runs.filter(r => r.eff < (r.bar ? BARMIN : MIN) - 0.01);
  perPage[n] = { runs: runs.length, bad: bad.length, cut: runs.filter(r => r.cut).length };
  for (const c of runs.filter(r => r.cut)) cuts.push({ page: n, text: c.text, sel: c.sel });
  for (const c of runs.filter(r => r.side)) sides.push({ page: n, text: c.text, sel: c.sel });
  // text of one block drawn over text of another (Farming's "Other plants" row over the planted/picked legend, MERGE-NOTES G3): the page's
  // blocks stack, so any two text runs from different blocks that cover each other mean a block was drawn outside its own room
  const stacked = runs.filter(r => r.blk >= 0);
  for (let i = 0; i < stacked.length; i++) for (let j = i + 1; j < stacked.length; j++) {
    const a = stacked[i], b = stacked[j]; if (a.blk === b.blk) continue;
    const w = Math.min(a.box[2], b.box[2]) - Math.max(a.box[0], b.box[0]), h = Math.min(a.box[3], b.box[3]) - Math.max(a.box[1], b.box[1]);
    if (w > 1 && h > 1) overlaps.push({ page: n, what: `text "${a.text}" x text "${b.text}" (two blocks)` });
  }
  for (const b of bad) rows.push({ page: n, ...b });
  // 0.8.1 US spelling: every word the page draws (innerText skips what is hidden), the preview's own footnote (#caption) left out
  const drawn = await page.evaluate(() => { const c = document.getElementById("caption"); return document.body.innerText.replace(c ? c.innerText : "\u0000", " "); });
  for (const w of new Set(drawn.match(BRITISH) || [])) british.push({ page: n, what: w });
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
console.log(`text cut at the side of a box that clips it: ` + (sides.length ? sides.length : "none"));
for (const c of sides.slice(0, 20)) console.log(`   side: ${c.page}: ${c.sel}  "${c.text}"`);
console.log(`heroes taller with their growth line: ` + (heroGrowth.length ? heroGrowth.length : "none"));
for (const g of heroGrowth.slice(0, 10)) console.log(`   growth: ${g.page}: ${g.what}`);
console.log(`group rows whose date is cut: ` + (datesCut.length ? datesCut.length : "none"));
for (const d of datesCut.slice(0, 10)) console.log(`   date: ${d.page}: ${d.what}`);
console.log(`feed amounts broken across lines: ` + (feedSplit.length ? feedSplit.length : "none"));
for (const d of feedSplit.slice(0, 10)) console.log(`   feed: ${d.page}: ${d.what}`);
console.log(`parts that overlap (the Fireside's counts, shields, names, hearth; text of two blocks): ` + (overlaps.length ? overlaps.length : "none"));
for (const o of overlaps.slice(0, 20)) console.log(`   overlap: ${o.page}: ${o.what}`);
if (val("--json")) writeFileSync(val("--json"), JSON.stringify({ min: MIN, byChapter: byChap, violations: rows, truncated: cuts }, null, 1));
console.log(`Fireside plans, HTML port vs C# FireLayout: ${fireCompared} compared, ` + (fireDiffs.length ? fireDiffs.length + " differ" : "all the same"));
for (const d of fireDiffs.slice(0, 10)) console.log(`   differs: ${d.page}: ${d.what}`);
console.log(`UI source: text sizes under the ${MIN} px floor: ` + (uiSmall.length ? uiSmall.length : "none"));
for (const u of uiSmall.slice(0, 20)) console.log(`   small: ${u}`);
if (uiSmall.length && !flag("--no-fail")) { console.log(`FAIL: a UI file asks for text under the ${MIN} px floor`); process.exit(1); }
// the previews must be the same from run to run: the panel's model and samples never use .NET's per-process string hash
// (string/StringComparer GetHashCode is randomized at every start; deeds-*-10min differed between dumps)
const hashed = [];
for (const sub of ["", "Chapters/"]) {
  let files = []; try { files = readdirSync(`${dir}/../${sub}`).filter(f => f.endsWith(".cs")); } catch { }
  for (const f of files) readFileSync(`${dir}/../${sub}${f}`, "utf8").split("\n").forEach((line, i) => {
    if (/(StringComparer\.\w+\.GetHashCode\(|"[^"]*"\.GetHashCode\(|\b\w+\.GetHashCode\(\))/.test(line) && !/override|\/\/.*GetHashCode/.test(line)) hashed.push(`${sub}${f}:${i + 1}`);
  });
}
console.log("model source: per-process string hashes: " + (hashed.length ? hashed.join(", ") : "none"));
console.log(`US spelling: British forms in the visible text of ${names.length} previews: ` + (british.length ? british.length : "none"));
for (const b of british.slice(0, 20)) console.log(`   British: ${b.page}: "${b.what}"`);
if (hashed.length && !flag("--no-fail")) { console.log("FAIL: a per-process hash makes the previews differ from run to run"); process.exit(1); }
if (british.length && !flag("--no-fail")) { console.log("FAIL: a British spelling is drawn (armor, defense, color, gray, meter: US spelling, as Valheim's own UI)"); process.exit(1); }
if (sides.length && !flag("--no-fail")) { console.log("FAIL: text cut at the side of its box"); process.exit(1); }
if (heroGrowth.length && !flag("--no-fail")) { console.log("FAIL: a hero is taller with its growth line"); process.exit(1); }
if (datesCut.length && !flag("--no-fail")) { console.log("FAIL: a group row's date is cut (the name gives way, never the date)"); process.exit(1); }
if (feedSplit.length && !flag("--no-fail")) { console.log("FAIL: a feed amount breaks across lines (a line breaks only between items)"); process.exit(1); }
if (overlaps.length && !flag("--no-fail")) { console.log("FAIL: parts overlap"); process.exit(1); }
if (fireDiffs.length && !flag("--no-fail")) { console.log("FAIL: the HTML Fireside is not the C# one"); process.exit(1); }
if (flag("--strict-cut") && cuts.length && !flag("--no-fail")) { console.log("FAIL: text cut off by its own box"); process.exit(1); }
if (rows.length && !flag("--no-fail")) { console.log(`FAIL: text under the ${MIN} px floor`); process.exit(1); }
console.log(`PASS: no text under the ${MIN} px floor`);
