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
    let blk = -1; for (let e = el; e && e.parentElement; e = e.parentElement) { const p = e.parentElement; if (p.matches(".content > .plate, .content .blocks")) { blk = e.classList.contains("plfade") || e.classList.contains("page-tip") ? -1 : [...p.children].indexOf(e); break; } }   // .plfade: the fade and the "More below" cue lie over the grid on purpose; the page's hover tag over a bar (PanelUi.PageTag) too
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
const rows = [], perPage = {}, cuts = [], sides = [], overlaps = [], fireDiffs = [], heroGrowth = [], heroLines = [], heroCompact = [], datesCut = [], british = [], feedSplit = [], feedTracks = [], fightTall = []; let fireCompared = 0, feedLogs = 0, pickPages = 0, fightPages = 0;
// 0.8.2 (Joost in game, The Mead Hall: "I want to see everyone ... not plus three more"): a feat's crew shows every person whole inside its detail
// area, none over another; and no "+N more" anywhere is dead words: each is a click (data-link) that shows the rest
const crewOff = [], deadMore = []; let crewsDrawn = 0, crewPeople = 0, moreLinks = 0;
// 0.8.2 (Joost in game: "Surtling Fire Arr…"): the Foes table's Best arrow column holds the longest arrow names in the game data whole. Each is
// measured at the floor in the game's serif plus TMP's 4 % (as btFoeTypes measures) against the C# name box, BattleUi.BtFoeArrow - 26
const LONG_ARROWS = ["Ancient wood arrow", "Surtling Fire Arrow", "Bronzehead Arrow"];   // ValheimArmory 1.35 (on the server) and the game's own
const arrowBox = parseFloat(((() => { try { return readFileSync(`${dir}/../Chapters/BattleUi.cs`, "utf8"); } catch { return ""; } })().match(/BtFoeArrow = (\d+)/) || [])[1]) - 26;
let arrowWidths = null;
// 0.8.2 (Joost in game, Battle > Foes: The Queen's Dealt read "12 6…"): the Dealt column holds a 6-digit number with its space whole beside a full
// bar. "123 456" and "12 645" measured bold at 16 px in the game's serif plus TMP's 4 % against the C# room, BattleUi.BtFoeDealt - BtFoeBar - BtFoeNumGap;
// and every Dealt number the previews draw stays inside its cell
const DEALT_WIDE = ["123 456", "12 645"];
const battleUi = (() => { try { return readFileSync(`${dir}/../Chapters/BattleUi.cs`, "utf8"); } catch { return ""; } })();
const csConst = n => parseFloat((battleUi.match(new RegExp(`\\b${n} = (\\d+)`)) || [])[1]);
const dealtBox = csConst("BtFoeDealt") - csConst("BtFoeBar") - csConst("BtFoeNumGap");
let dealtWidths = null; const dealtCut = []; let dealtDrawn = 0;
// 0.8.2 (Greydwarf Shaman was cut): the Foes table's name box holds the longest common foe names whole, measured as the arrow names against the C#
// name box, BattleUi.BtFoeName - BtFoeNamePad; and every foe name the previews draw is whole in it
const LONG_FOES = ["Greydwarf Shaman", "Charred Marksman", "Bonemaw Serpent"];   // the game's own: the Black Forest's shaman, the Ashlands' archer and serpent
const nameBox = csConst("BtFoeName") - csConst("BtFoeNamePad");
let foeNameWidths = null; const foeNamesCut = [];
// 0.8.2 (Joost in game: "that mouse over should always be visible on all of them, because then it shows you what damage type that actually is"):
// every part of every damage bar (damage rows, the feed's Log and cards, the foe detail, Everyone's Dealt bars, the armor bar), pointed at, shows
// its name and number (PanelModel.DamageHoverWords): its share entry's words swap to them, or the page's one tag says them in its bar's room.
// Each part is pointed at in turn (mouseover, then mouseout) and what shows is read. And none of it costs an element per part (0.8.1's speed: 4e72aff's
// tag per part and second label per entry were about 3 ms a drawing on the sample feed, 20 ms on a long one): at most one tag on a page, one label in
// an entry (PanelUi.PageTag, the entry's own words swapped; Unity counts the same in KsPanelTest's HoverCostTests)
const hoverless = [], hoverHeavy = []; let damageParts = 0, hoverPages = 0;
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
  // 0.8.2 (Joost: one rule for the whole book): a part that filters and its legend item are one target, and a chosen part is chosen in its legend too
  for (const t of await page.evaluate(() => [...document.querySelectorAll(".bf")].flatMap(bf => [...bf.querySelectorAll(".bf-seg[data-k]")].map(s => {
      const r = bf.querySelector(`.bf-tab .r[data-k="${s.dataset.k}"]`), name = r && r.querySelector(".nm > span:not(.sw)");
      if (!r) return null;
      if ((s.dataset.pick || "") !== (r.dataset.pick || "")) return `part "${name ? name.textContent : s.dataset.k}" filters by "${s.dataset.pick || ""}", its legend item by "${r.dataset.pick || ""}"`;
      if (s.classList.contains("sel") !== !!(name && name.classList.contains("sel"))) return `part "${name ? name.textContent : s.dataset.k}" chosen on ${s.classList.contains("sel") ? "the bar" : "the legend"} only`;
      return null; }).filter(Boolean))))
    overlaps.push({ page: n, what: t });
  if (await page.evaluate(() => !!document.querySelector(".bf-seg[data-pick]"))) pickPages++;
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
  // REVIEW-081 0.8.2 pass: every fight is its own island, and the Log's bars still start at one x and share one track over the whole page (the
  // columns measured over every fight, PanelModel.FeedPageEntries): a fight where nothing hurt you gets no longer track than the one above it
  const tracks = await page.evaluate(() => [...document.querySelectorAll(".bt-fl .drl")].map(e => { const r = e.getBoundingClientRect(); return [r.left, r.width, e.closest(".bt-fl").parentElement]; })
    .map(([x, w, fight], i, all) => [x, w, new Set(all.map(t => t[2])).size]));
  // 0.9 (Joost: the fight on one screen at the book's size): a fight's or a run's page (Chapters/FightPages.cs) fits the plate without scrolling;
  // only a row opened by a click or the About box may add to it
  const tall = await page.evaluate(() => { const s = JSON.stringify(window.CURRENT.blocks || []);
    if (!s.includes('"kind":"fighthead"') || /"kind":"aboutnumbers"[^}]*"open":1/.test(s) || /"kind":"foe"[^}]*"selected":1/.test(s) || /"kind":"fightfoerows"[^}]*"tone":"dealt"/.test(s)) return null;   // stacked (not the default) takes two islands of rows
    const p = document.querySelector(".plate"); return p ? p.scrollHeight - p.clientHeight : null; });
  if (tall !== null) { fightPages++; if (tall > 1) fightTall.push({ page: n, what: `${tall} px taller than the plate` }); }
  if (tracks.length) {
    feedLogs++;
    const odd = tracks.filter(t => Math.abs(t[0] - tracks[0][0]) > 0.5 || Math.abs(t[1] - tracks[0][1]) > 0.5);
    if (odd.length) feedTracks.push({ page: n, what: `${odd.length} of ${tracks.length} Log bars (${tracks[0][2]} fights) off the first one's x ${tracks[0][0].toFixed(1)} and width ${tracks[0][1].toFixed(1)}, e.g. ${odd[0][0].toFixed(1)} and ${odd[0][1].toFixed(1)}` });
  }
  if (!arrowWidths && await page.evaluate(() => !!document.querySelector(".bt-foerow")))
    arrowWidths = await page.evaluate(([names, min]) => { const c = document.createElement("canvas").getContext("2d"); c.font = `${min}px "Averia Serif Libre"`;
      return names.map(t => [t, Math.ceil(c.measureText(t).width * 1.04)]); }, [LONG_ARROWS, MIN]);
  if (!foeNameWidths && await page.evaluate(() => !!document.querySelector(".bt-foerow")))
    foeNameWidths = await page.evaluate(([texts]) => { const c = document.createElement("canvas").getContext("2d"); c.font = `16px "Averia Serif Libre"`;
      return texts.map(t => [t, Math.ceil(c.measureText(t).width * 1.04)]); }, [LONG_FOES]);
  for (const t of await page.evaluate(() => [...document.querySelectorAll(".bt-foerow .fn")].filter(e => e.scrollWidth > e.clientWidth + 0.5).map(e => e.textContent)))
    foeNamesCut.push({ page: n, what: `"${t}"` });
  if (!dealtWidths && await page.evaluate(() => !!document.querySelector(".bt-foerow")))
    dealtWidths = await page.evaluate(([texts]) => { const c = document.createElement("canvas").getContext("2d"); c.font = `700 16px "Averia Serif Libre"`;
      return texts.map(t => [t, Math.ceil(c.measureText(t).width * 1.04)]); }, [DEALT_WIDE]);
  for (const [t, over] of await page.evaluate(() => [...document.querySelectorAll(".bt-foerow .bt-c.dl")].map(c => { const b = c.querySelector("b"), r = c.getBoundingClientRect(), q = b.getBoundingClientRect();
      return [b.textContent, q.right - r.right]; }))) { dealtDrawn++; if (over > 0.5) dealtCut.push({ page: n, what: `"${t}" ${over.toFixed(1)} px past its cell` }); }
  const keysOver = await page.evaluate(() => { const k = document.querySelector(".keys"); return k && k.scrollWidth > k.clientWidth + 1 ? k.textContent.replace(/\s+/g, " ").trim() : null; });
  if (keysOver) overlaps.push({ page: n, what: `the key line is wider than its room: "${keysOver}"` });
  // 0.8 layout D+ (Joost on the board): the hero's growth line takes no height of its own: each hero is as tall with it as without it
  const taller = await page.evaluate(() => [...document.querySelectorAll(".hero")].map(h => { const sp = h.querySelector(".hspark"); if (!sp || sp.style.display === "none") return null;
    const was = h.offsetHeight; sp.style.display = "none"; const without = h.offsetHeight; sp.style.display = ""; return was > without ? `${was} px with it, ${without} without` : null; }).filter(Boolean));
  for (const t of taller) heroGrowth.push({ page: n, what: t });
  // 0.8.2 (Joost in game, Mining): the one-hero rule. Every number of a hero line, its label and its date stand on one baseline (1 px), and the
  // skill never has a line of its own (PanelModel.HeroLines): a hero line holds a number
  for (const t of await page.evaluate(() => {
    const out = [], base = el => { const p = document.createElement("span"); p.style.cssText = "display:inline-block;width:0;height:0;vertical-align:baseline"; el.prepend(p); const y = p.getBoundingClientRect().bottom; p.remove(); return y; };
    for (const h of document.querySelectorAll(".hero")) {
      const lines = h.classList.contains("wrapped") ? [...h.querySelectorAll(":scope > .hline")] : [h];
      for (const l of lines) {
        const nums = [...l.querySelectorAll(":scope > .hn")];
        if (!nums.length && [...l.children].some(k => k.classList.contains("hskill") && k.style.display !== "none")) { out.push(`the skill on a line of its own`); continue; }
        const texts = nums.flatMap(hn => [hn.querySelector(".num"), ...hn.querySelectorAll(".what > *, .what")].filter(Boolean)).filter(e => e.offsetParent && e.textContent.trim());
        const ys = texts.map(e => [e, base(e)]), y0 = ys.length ? ys[0][1] : 0, off = ys.filter(([, y]) => Math.abs(y - y0) > 1);
        for (const [e, y] of off) out.push(`"${e.textContent.trim().slice(0, 30)}" ${(y - y0).toFixed(1)} px off the hero number's baseline`);
      }
    }
    return out; })) heroLines.push({ page: n, what: t });
  if (await page.evaluate(() => !!document.querySelector(".hero .hskill.compact"))) heroCompact.push(n);   // information: the skill in its compact form
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
  const crew = await page.evaluate(() => { const out = { crews: 0, people: 0, off: [] };
    for (const cr of document.querySelectorAll(".fdet > .fcrew")) {
      out.crews++; const det = cr.parentElement.getBoundingClientRect(), boxes = [];
      for (const e of cr.querySelectorAll(".ce")) { out.people++; const nm = e.querySelector(".cn"), r = [e.querySelector(".sw, .cmk"), nm, e.querySelector(".cnum"), e.querySelector(".csh")].filter(Boolean).map(x => x.getBoundingClientRect());
        const b = { l: Math.min(...r.map(q => q.left)), t: Math.min(...r.map(q => q.top)), r: Math.max(...r.map(q => q.right)), b: Math.max(...r.map(q => q.bottom)), who: nm.textContent };
        if (nm.getBoundingClientRect().width < 1 || b.b > det.bottom - 2 + 0.5 || b.r > det.right - 16 + 0.5) out.off.push(`${b.who} outside the detail area`);
        for (const o of boxes) if (Math.min(o.r, b.r) - Math.max(o.l, b.l) > 0.5 && Math.min(o.b, b.b) - Math.max(o.t, b.t) > 0.5) out.off.push(`${b.who} over ${o.who}`);
        boxes.push(b); } }
    return out; });
  crewsDrawn += crew.crews; crewPeople += crew.people; for (const o of crew.off) crewOff.push({ page: n, what: o });
  const more = await page.evaluate(() => { const w = document.createTreeWalker(document.body, NodeFilter.SHOW_TEXT), out = { links: 0, dead: [] };
    for (let t = w.nextNode(); t; t = w.nextNode()) { const txt = t.nodeValue.replace(/\s+/g, " ").trim(), el = t.parentElement;
      if (!/^\+[\d\s]+ more$/.test(txt) || !el || el.closest("#caption")) continue; const cs = getComputedStyle(el); if (cs.visibility === "hidden" || cs.display === "none" || !el.getClientRects().length) continue;
      if (el.closest("[data-link]")) out.links++; else out.dead.push(txt); }
    return out; });
  moreLinks += more.links; for (const d of more.dead) deadMore.push({ page: n, what: d });
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
  // last on the page (it points at every part): every damage bar part shows its name and number on hover, and the hover costs no element per part
  const hv = await page.evaluate(() => {
    const miss = [], segs = [...document.querySelectorAll(".dr-seg")], tip = document.querySelector(".page-tip");
    const lit = [...document.querySelectorAll(".bf .hov[data-k]")];   // a hovered-state shot's lit part: lit again after
    for (const x of lit) x.dispatchEvent(new MouseEvent("mouseout", { bubbles: true }));
    const name = (bf, k) => `${(bf && bf.querySelector(".nm, b") || { textContent: "?" }).textContent.trim()}, part ${k}`;
    for (const s of segs) {
      const bf = s.closest(".bf"), k = s.dataset.k;
      if (!bf || k == null) { miss.push(name(bf, k) + ": no bar or no key"); continue; }
      s.dispatchEvent(new MouseEvent("mouseover", { bubbles: true }));
      const entry = [...bf.querySelectorAll(`.dr-e[data-k="${k}"] .w`)].find(w => w.dataset.on && w.textContent === w.dataset.on && w.offsetParent);
      const shown = entry ? entry.textContent : tip && tip.classList.contains("on") && tip.offsetParent && s.dataset.tip && tip.textContent === s.dataset.tip ? tip.textContent : "";
      const pr = s.getBoundingClientRect(), tr = tip && tip.getBoundingClientRect();
      const near = entry || (tr && Math.abs(tr.top + tr.height / 2 - (pr.top + pr.height / 2)) <= 4);   // the tag on the part's own line
      s.dispatchEvent(new MouseEvent("mouseout", { bubbles: true }));
      const after = [...bf.querySelectorAll(`.dr-e[data-k="${k}"] .w`)].every(w => w.textContent === (w.dataset.off ?? w.textContent)) && !(tip && tip.classList.contains("on"));
      if (!shown.trim() || !near || !after) miss.push(name(bf, k) + (!shown.trim() ? ": nothing shown" : !near ? ": the tag is off its line" : ": not set back on leaving"));
    }
    for (const x of lit) x.dispatchEvent(new MouseEvent("mouseover", { bubbles: true }));
    const tips = document.querySelectorAll(".dr-tip").length, labels = [...document.querySelectorAll(".dr-e .w")].filter(w => w.children.length > 0).length + document.querySelectorAll(".dr-e .b").length;
    return { parts: segs.length, miss, tips, labels };
  });
  damageParts += hv.parts; if (hv.parts) hoverPages++;
  for (const m of hv.miss) hoverless.push({ page: n, what: m });
  if (hv.tips > 1 || hv.labels > 0) hoverHeavy.push({ page: n, what: `${hv.tips} hover tags (at most 1 a page), ${hv.labels} entries with a second label` });
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
console.log(`heroes off the one-hero rule (a baseline per line, no line for the skill alone): ` + (heroLines.length ? heroLines.length : "none"));
for (const g of heroLines.slice(0, 10)) console.log(`   hero: ${g.page}: ${g.what}`);
console.log(`heroes whose skill takes its compact form (information): ` + (heroCompact.length ? heroCompact.join(", ") : "none"));
console.log(`heroes taller with their growth line: ` + (heroGrowth.length ? heroGrowth.length : "none"));
for (const g of heroGrowth.slice(0, 10)) console.log(`   growth: ${g.page}: ${g.what}`);
console.log(`group rows whose date is cut: ` + (datesCut.length ? datesCut.length : "none"));
for (const d of datesCut.slice(0, 10)) console.log(`   date: ${d.page}: ${d.what}`);
console.log(`feed amounts broken across lines: ` + (feedSplit.length ? feedSplit.length : "none"));
for (const d of feedSplit.slice(0, 10)) console.log(`   feed: ${d.page}: ${d.what}`);
console.log(`the battle feed's Log bars on one x and one track: ${feedLogs} pages, ` + (feedTracks.length ? feedTracks.length + " differ" : "all the same"));
console.log(`the fight pages on one screen (no row open, About shut): ${fightPages} pages, ` + (fightTall.length ? fightTall.map(t => `${t.page} ${t.what}`).join("; ") : "all fit"));
for (const d of feedTracks.slice(0, 10)) console.log(`   feed track: ${d.page}: ${d.what}`);
console.log(`feat crews: ${crewsDrawn} drawn, ${crewPeople} people, ` + (crewOff.length ? crewOff.length + " off" : "every one whole inside its detail area, none over another"));
for (const o of crewOff.slice(0, 10)) console.log(`   crew: ${o.page}: ${o.what}`);
console.log(`"+N more": ${moreLinks} drawn as a click that shows the rest, ` + (deadMore.length ? deadMore.length + " dead words" : "none dead"));
for (const o of deadMore.slice(0, 10)) console.log(`   dead: ${o.page}: "${o.what}"`);
console.log(`bars whose parts filter, their legend items on the same target (0.8.2): ${pickPages} pages drew one`);
console.log(`parts that overlap (the Fireside's counts, shields, names, hearth; text of two blocks): ` + (overlaps.length ? overlaps.length : "none"));
for (const o of overlaps.slice(0, 20)) console.log(`   overlap: ${o.page}: ${o.what}`);
if (val("--json")) writeFileSync(val("--json"), JSON.stringify({ min: MIN, byChapter: byChap, violations: rows, truncated: cuts }, null, 1));
console.log(`Fireside plans, HTML port vs C# FireLayout: ${fireCompared} compared, ` + (fireDiffs.length ? fireDiffs.length + " differ" : "all the same"));
for (const d of fireDiffs.slice(0, 10)) console.log(`   differs: ${d.page}: ${d.what}`);
const arrowsCut = (arrowWidths || []).filter(([, w]) => !(w <= arrowBox));
console.log(`Foes: the longest arrow names in the ${arrowBox} px name box: ` + (arrowWidths ? arrowWidths.map(([t, w]) => `${t} ${w}`).join(", ") + (arrowsCut.length ? `; ${arrowsCut.length} cut` : ", all whole") : "no foe table drawn"));
if ((arrowsCut.length || (!arrowWidths && !val("--only"))) && !flag("--no-fail")) { console.log("FAIL: the Foes table's Best arrow column cuts an arrow name (or none was drawn to measure)"); process.exit(1); }
const namesNarrow = (foeNameWidths || []).filter(([, w]) => !(w <= nameBox));
console.log(`Foes: the longest foe names in the ${nameBox} px name box: ` + (foeNameWidths ? foeNameWidths.map(([t, w]) => `${t} ${w}`).join(", ") + (namesNarrow.length ? `; ${namesNarrow.length} cut` : ", all whole") : "no foe table drawn") +
            `; drawn names cut: ` + (foeNamesCut.length ? foeNamesCut.length : "none"));
for (const d of foeNamesCut.slice(0, 10)) console.log(`   name: ${d.page}: ${d.what}`);
if ((namesNarrow.length || foeNamesCut.length || (!foeNameWidths && !val("--only"))) && !flag("--no-fail")) { console.log("FAIL: the Foes table's name box cuts a foe name (or none was drawn to measure)"); process.exit(1); }
const dealtNarrow = (dealtWidths || []).filter(([, w]) => !(w <= dealtBox));
console.log(`Foes: the widest Dealt numbers in the ${dealtBox} px room beside a full bar: ` + (dealtWidths ? dealtWidths.map(([t, w]) => `${t} ${w}`).join(", ") + (dealtNarrow.length ? `; ${dealtNarrow.length} cut` : ", all whole") : "no foe table drawn") +
            `; ${dealtDrawn} drawn, ` + (dealtCut.length ? `${dealtCut.length} past their cell` : "all inside their cell"));
for (const d of dealtCut.slice(0, 10)) console.log(`   dealt: ${d.page}: ${d.what}`);
if ((dealtNarrow.length || dealtCut.length || (!dealtWidths && !val("--only"))) && !flag("--no-fail")) { console.log("FAIL: the Foes table's Dealt column cuts a number (or none was drawn to measure)"); process.exit(1); }
console.log(`damage bar parts pointed at, each showing its name and number: ${damageParts} parts on ${hoverPages} pages, ` + (hoverless.length ? hoverless.length + " without" : "every one shows them") +
            `; hover elements: ` + (hoverHeavy.length ? hoverHeavy.length + " pages over one tag and one label an entry" : "at most one tag a page, one label an entry"));
for (const h of hoverless.slice(0, 10)) console.log(`   no hover: ${h.page}: ${h.what}`);
for (const h of hoverHeavy.slice(0, 10)) console.log(`   heavy: ${h.page}: ${h.what}`);
if ((hoverless.length || hoverHeavy.length || (!damageParts && !val("--only"))) && !flag("--no-fail")) { console.log("FAIL: a damage bar's part shows nothing on hover, or the hover costs an element per part (or no damage bar was drawn to check)"); process.exit(1); }
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
if (heroLines.length && !flag("--no-fail")) { console.log("FAIL: a hero breaks the one-hero rule (its numbers, labels and dates on one baseline; the skill never on a line of its own)"); process.exit(1); }
if (datesCut.length && !flag("--no-fail")) { console.log("FAIL: a group row's date is cut (the name gives way, never the date)"); process.exit(1); }
if (feedSplit.length && !flag("--no-fail")) { console.log("FAIL: a feed amount breaks across lines (a line breaks only between items)"); process.exit(1); }
if ((fightTall.length || (!fightPages && !val("--only"))) && !flag("--no-fail")) { console.log(fightPages ? "FAIL: a fight's page is taller than the plate (it should fit one screen; what does not fit opens on a click)" : "FAIL: no preview drew a fight's page, so its one screen went unchecked"); process.exit(1); }
if ((feedTracks.length || (!feedLogs && !val("--only"))) && !flag("--no-fail")) { console.log(feedLogs ? "FAIL: the battle feed's Log bars do not share one x and one track across its fights" : "FAIL: no preview drew the battle feed's Log, so its one track went unchecked"); process.exit(1); }
if (!pickPages && !val("--only") && !flag("--no-fail")) { console.log("FAIL: no preview drew a bar whose parts filter, so its legend items went unchecked"); process.exit(1); }
if (overlaps.length && !flag("--no-fail")) { console.log("FAIL: parts overlap"); process.exit(1); }
if ((crewOff.length || (!crewsDrawn && !val("--only"))) && !flag("--no-fail")) { console.log(crewsDrawn ? "FAIL: a feat's crew leaves a person outside its detail area or over another" : "FAIL: no preview drew a feat's crew to check"); process.exit(1); }
if (deadMore.length && !flag("--no-fail")) { console.log("FAIL: a \"+N more\" is dead words (it must be a click that shows the rest)"); process.exit(1); }
if (fireDiffs.length && !flag("--no-fail")) { console.log("FAIL: the HTML Fireside is not the C# one"); process.exit(1); }
if (flag("--strict-cut") && cuts.length && !flag("--no-fail")) { console.log("FAIL: text cut off by its own box"); process.exit(1); }
if (rows.length && !flag("--no-fail")) { console.log(`FAIL: text under the ${MIN} px floor`); process.exit(1); }
console.log(`PASS: no text under the ${MIN} px floor`);
