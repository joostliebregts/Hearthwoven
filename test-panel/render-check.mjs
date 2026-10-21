// Renders every preview in preview-data.js to preview-<name>.png and MEASURES every visible text run.
// Any text rendered below the floor (PanelLook.MinText, the preview's --text-floor) is a violation; the run exits 1 if any rule fails
// (unless --no-fail). Usage: node render-check.mjs <previewDir> [--only battle] [--min 14] [--strict-cut] [--no-fail] [--no-png] [--json out.json]
// [--jobs N] [--verbose]. The previews are shot by N workers at once (default: two thirds of the cores, at most 8), each in its own browser
// context; a page is shot the same way it always was, so the PNGs are byte-identical to a one-at-a-time run. The report is one line per
// rule (PASS, FAIL or info) with the offenders under a failing one; --verbose adds the per-chapter counts and every offender.
// Playwright is found via PLAYWRIGHT_MJS (default: the playwright package resolvable from here); Chromium via CHROMIUM_EXE.
import { readFileSync, writeFileSync, readdirSync } from "fs";
import { resolve } from "path";
import { availableParallelism } from "os";
const args = process.argv.slice(2);
const dir = resolve(args.find((a, i) => !a.startsWith("--") && !["--only", "--json", "--min", "--jobs"].includes(args[i - 1])));
const flag = (f) => args.includes(f);
// 0.8.1 US spelling (Joost: "armor, not armour"): the mirror's visible text never holds a British form. The same word list as test-panel/UsSpellingTests.cs
// (the C# source scan); Valheim's own names (Greydwarf, Greyling) are not forms of "grey".
const BRITISH = /\b(?:armour|defence|offence|colour|grey(?!dwarf|ling)|honour|favour|behaviour|neighbour|labour|flavour|harbour|centre|litre)\w*|\w*metres?\b|\b(?:practis|travell|cancell|labell|modell|levell|fuell|signall|marvell|counsell|jewell)(?:e|ed|es|er|ers|ing)\b|\b(?:recogni|organi|summari|customi|categori|normali|visuali|prioriti|minimi|maximi|reali|apologi|speciali|utili|synchroni|optimi|finali|initiali|locali|saniti|authori|personali|standardi|harmoni|fertili|analy|emphasi|memori|capitali|digiti|stabili)s(?:e|ed|es|ing)\b|\b\w+isation\b|\b(?:licence|programme|whilst|amongst|learnt)\b/gi;
const val = (f) => { const i = args.indexOf(f); return i >= 0 ? args[i + 1] : null; };
const started = Date.now();
// the floor: --min N, else PanelLook.MinText read from the C# source (single source of truth); the preview's --text-floor (layout.css, written from
// PanelLayout.Text.Floor) must agree with it
const csSrc = (() => { try { return readFileSync(`${dir}/../PanelLook.cs`, "utf8"); } catch { return ""; } })();
const csMin = parseFloat((csSrc.match(/MinText\s*=\s*([\d.]+)f/) || [])[1]);
const htmlMin = parseFloat((((() => { try { return readFileSync(`${dir}/layout.css`, "utf8"); } catch { return ""; } })()).match(/--text-floor:\s*([\d.]+)px/) || [])[1]);
const MIN = val("--min") ? parseFloat(val("--min")) : (csMin || 13);
// the one exception (PanelLook.MinText, Joost 2026-10-10): a number inside a damage-bar part, digits only and bold, may be PanelLook.MinBarDigits;
// the preview's --bardigits must agree, and nothing else may go under the floor
const csBarMin = parseFloat((csSrc.match(/MinBarDigits\s*=\s*([\d.]+)f/) || [])[1]);
const htmlBarMin = parseFloat(((readFileSync(`${dir}/panel-preview.html`, "utf8")).match(/--bardigits:\s*([\d.]+)px/) || [])[1]);
const BARMIN = csBarMin || MIN;
if ((csBarMin || htmlBarMin) && csBarMin !== htmlBarMin) { console.log(`FAIL  the in-bar digits' floor is one: PanelLook.MinBarDigits = ${csBarMin} but the preview --bardigits = ${htmlBarMin}`); process.exit(1); }
if (csMin && htmlMin && csMin !== htmlMin) { console.log(`FAIL  one floor for the Unity panel and the HTML preview: PanelLook.MinText = ${csMin} but the preview --text-floor = ${htmlMin}`); process.exit(1); }
// the game's own drawing (PanelUi and the Chapters' *Ui.cs): a text call with a literal size under the floor (Label, RichLabel, Since). Label
// lifts it to the floor, so the box around it was laid out for text smaller than what is drawn; the preview draws the floor and cannot
// show it (Company's fellow-row label asked for 11 px, MERGE-NOTES G6). Every size a UI file writes must be the floor or more.
const uiSmall = [];
for (const sub of ["", "Chapters/"]) {
  let files = []; try { files = readdirSync(`${dir}/../${sub}`).filter(f => f.endsWith(".cs")); } catch { }
  for (const f of files) readFileSync(`${dir}/../${sub}${f}`, "utf8").split("\n").forEach((line, i) => {
    // the size argument itself (Label's third, RichLabel's fourth, Since's second): 0.8.5 sizes come from PanelLayout.Text, so the first literal
    // after the call is no longer its size (a colour's 0.6f); a literal size anywhere is LayoutTests' to catch, one under the floor is caught here
    for (const m of line.matchAll(/\b(Label|RichLabel|Since)\(/g)) {
      const args = []; let depth = 0, from = m.index + m[0].length, k = from;
      for (; k < line.length; k++) { const c = line[k]; if ("([{".includes(c)) depth++; else if (")]}".includes(c)) { if (depth-- === 0) { args.push(line.slice(from, k)); break; } } else if (c === "," && depth === 0) { args.push(line.slice(from, k)); from = k + 1; } }
      if (k === line.length) args.push(line.slice(from));   // a call that goes on past this line: its size is still among the first arguments
      const size = (args[{ Label: 2, RichLabel: 3, Since: 1 }[m[1]]] || "").trim().match(/^(\d+(?:\.\d+)?)f?$/);
      if (size && parseFloat(size[1]) < MIN) uiSmall.push(`${sub}${f}:${i + 1}: ${m[1]}(..., ${size[1]})`);
    }
    // a size set straight on a text (past Label's floor): a literal under the floor never; the in-bar digits' sizes (MinBarDigits, DrDigits) only in
    // PanelUi.BarDigits (Chapters/DamageRowsUi.cs), the one exception, so it cannot spread
    const code = line.replace(/\/\/.*$/, "");   // the code, not its comments
    for (const m of code.matchAll(/fontSize\s*=(?!=)\s*([^;]+)/g))
      if ([...m[1].matchAll(/\b(\d+(?:\.\d+)?)f?\b/g)].some(x => parseFloat(x[1]) < MIN && parseFloat(x[1]) > 0)) uiSmall.push(`${sub}${f}:${i + 1}: fontSize = ${m[1].trim()}`);
    if (/MinBarDigits|DrDigits/.test(code) && !(sub + f === "Chapters/DamageRowsUi.cs" || f === "PanelLook.cs" || f === "PanelCheck.cs")) uiSmall.push(`${sub}${f}:${i + 1}: the in-bar digits' size outside PanelUi.BarDigits`);
  });
}
// the previews must be the same from run to run: the panel's model and samples never use .NET's per-process string hash
// (string/StringComparer GetHashCode is randomized at every start; deeds-*-10min differed between dumps)
const hashed = [];
for (const sub of ["", "Chapters/"]) {
  let files = []; try { files = readdirSync(`${dir}/../${sub}`).filter(f => f.endsWith(".cs")); } catch { }
  for (const f of files) readFileSync(`${dir}/../${sub}${f}`, "utf8").split("\n").forEach((line, i) => {
    if (/(StringComparer\.\w+\.GetHashCode\(|"[^"]*"\.GetHashCode\(|\b\w+\.GetHashCode\(\))/.test(line) && !/override|\/\/.*GetHashCode/.test(line)) hashed.push(`${sub}${f}:${i + 1}`);
  });
}
const { chromium } = await import(process.env.PLAYWRIGHT_MJS || "playwright");
const src = readFileSync(`${dir}/preview-data.js`, "utf8");
let names = [...src.matchAll(/^  "([^"]+)": \{/gm)].map(m => m[1]);
const only = val("--only"); if (only) names = names.filter(n => n.includes(only));
const JOBS = Math.max(1, parseInt(val("--jobs") || "0") || Math.min(8, Math.max(1, Math.floor(availableParallelism() * 2 / 3))));   // Mac Mini (12 cores): 8 workers, 62 s for 322 previews (one tab: 372 s)
const browser = await chromium.launch({ executablePath: process.env.CHROMIUM_EXE || `${process.env.HOME}/Library/Caches/ms-playwright/chromium-1148/chrome-mac/Chromium.app/Contents/MacOS/Chromium`, args: ["--disable-gpu", "--disable-gpu-rasterization", "--force-color-profile=srgb"] });   // software raster: the GPU path drew the filtered icons a shade apart from run to run (about-sharing flipped in 2 of 6 renders), so branches fought over unchanged PNGs

// runs in the page: every visible text run with its rendered size (computed font-size x the transform scale of its ancestors)
const measure = (MIN) => {
  const out = [];
  // 0.9.10 the rows whose texts stand side by side and must never cover each other: a page's hero (its numbers, words and dates), a fight's head
  // (the boss or the land, its name over its land, the numbers; PanelUi.FtHead) and the away totals, Battle's hero-form row; every other text
  // is held to the same within its block
  const HEAD_ROWS = ".hero, .ft, .awt";
  const cssPath = (el) => { const parts = []; for (let e = el; e && e !== document.body && parts.length < 4; e = e.parentElement) {
    let s = e.tagName.toLowerCase(); if (e.id) s += "#" + e.id; const c = (e.getAttribute("class") || "").trim().split(/\s+/).filter(Boolean).slice(0, 2); if (c.length) s += "." + c.join("."); parts.unshift(s); } return parts.join(" > "); };
  const clipRects = (el) => { const rs = []; for (let e = el; e && e !== document.documentElement; e = e.parentElement) { const cs = getComputedStyle(e);
    if (cs.overflowX !== "visible" || cs.overflowY !== "visible") rs.push(e.getBoundingClientRect()); } return rs; };
  // 0.8.6 the sticky head: a row scrolled under the pinned strip is hidden by it, as in game the scroll area's mask clips it at the strip's foot
  const pins = [...document.querySelectorAll(".content > .plate > .pinhead")].map(h => h.getBoundingClientRect());
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
    const pin = el.closest(".pinhead") ? null : pins.find(q => r.right > q.left && r.left < q.right && r.top < q.bottom && r.bottom > q.top);
    if (pin && r.bottom <= pin.bottom + 0.5) continue;   // wholly under the pinned strip
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
    const bar = el.classList.contains("num") && !!el.closest(".dr-seg") && /^[\d\s   ]+$/.test(txt) && /\d/.test(txt) && parseInt(cs.fontWeight) >= 600;
    // 0.9.10 (Joost in game: a boss fight's "Fader" and "Ashlands" drawn over 58 118): the ink as drawn, cut by every box that clips it (a name cut
    // with an ellipsis covers only its box), and the hero or stat row it stands in (HEAD_ROWS), where no two texts may cover each other
    const ink = [r.left, Math.max(r.top + 0.2 * eff, pin ? pin.bottom : -Infinity), r.right, r.bottom - 0.2 * eff];
    for (const c of clipRects(el)) { ink[0] = Math.max(ink[0], c.left); ink[1] = Math.max(ink[1], c.top); ink[2] = Math.min(ink[2], c.right); ink[3] = Math.min(ink[3], c.bottom); }
    const head = el.closest(HEAD_ROWS); if (head && !head.dataset.rcRow) head.dataset.rcRow = String(document.querySelectorAll("[data-rc-row]").length + 1);
    // what may lie over other text on purpose: the plate's fade with its "More below" cue, the page's hover tag; and a turned column head (Foes'
    // damage types), whose upright box is wider than its ink
    const overlay = !!el.closest(".plfade, .page-tip, .dr-tip");
    let turned = false; for (let e = el; e && e !== document.documentElement; e = e.parentElement) { const t = getComputedStyle(e).transform; if (t && t !== "none") { const m = new DOMMatrix(t); if (Math.abs(m.b) > 1e-3 || Math.abs(m.c) > 1e-3) { turned = true; break; } } }
    out.push({ ink, overlay, turned, row: head ? head.dataset.rcRow : null, rowKind: head ? (head.getAttribute("class") || "").split(/\s+/)[0] : null, text: txt.slice(0, 40), fs, bar, eff: Math.round(eff * 100) / 100, h: Math.round(r.height * 10) / 10, sel: cssPath(el), x: Math.round(r.left), y: Math.round(r.top), cut, side, blk, box: [r.left, Math.max(r.top + 0.2 * eff, pin ? pin.bottom : -Infinity), r.right, r.bottom - 0.2 * eff] });   // the ink, not the line box: a big number's line box reaches into the label under it
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
// 0.8.2 (Joost in game, Battle > Foes: "Surtling Fire Arr…"): the Foes table's Best arrow column holds the longest arrow names in the game data whole. Each is
// measured at the floor in the game's serif plus TMP's 4 % (as btFoeTypes measures) against the C# name box, BattleUi.BtFoeArrow - 26
const LONG_ARROWS = ["Ancient wood arrow", "Surtling Fire Arrow", "Bronzehead Arrow"];   // ValheimArmory 1.35 (on the server) and the game's own
const arrowBox = parseFloat(((() => { try { return readFileSync(`${dir}/../Chapters/BattleUi.cs`, "utf8"); } catch { return ""; } })().match(/BtFoeArrow = (\d+)/) || [])[1]) - 26;
// 0.8.2 (Joost in game, Battle > Foes: The Queen's Dealt read "12 6…"): the Dealt column holds a 6-digit number with its space whole beside a full
// bar. "123 456" and "12 645" measured bold at 16 px in the game's serif plus TMP's 4 % against the C# room, BattleUi.BtFoeDealt - BtFoeBar - BtFoeNumGap;
// and every Dealt number the previews draw stays inside its cell
const DEALT_WIDE = ["123 456", "12 645"];
const battleUi = (() => { try { return readFileSync(`${dir}/../Chapters/BattleUi.cs`, "utf8"); } catch { return ""; } })();
const csConst = n => parseFloat((battleUi.match(new RegExp(`\\b${n} = (\\d+)`)) || [])[1]);
const dealtBox = csConst("BtFoeDealt") - csConst("BtFoeBar") - csConst("BtFoeNumGap");
// 0.8.2 (Greydwarf Shaman was cut): the Foes table's name box holds the longest common foe names whole, measured as the arrow names against the C#
// name box, BattleUi.BtFoeName - BtFoeNamePad; and every foe name the previews draw is whole in it
const LONG_FOES = ["Greydwarf Shaman", "Charred Marksman", "Bonemaw Serpent"];   // the game's own: the Black Forest's shaman, the Ashlands' archer and serpent
const nameBox = csConst("BtFoeName") - csConst("BtFoeNamePad");
const MEAL_PIN_MAX = 60;   // 0.9.6 the pinned meal's island: a slot (40) and the book's least island padding (8) above and below, a little slack

// one preview: shot to its PNG and checked; everything it finds comes back in r (the runs on the report fold the pages in their order)
async function shoot(page, n) {
  const r = { rowOver: [], rowTexts: 0, histOff: [], histCharts: 0, histVaried: 0, overlaps: [], datesCut: [], feedSplit: [], feedTracks: [], fightTall: [], overviewTall: [], fellOff: [], overviewPages: 0, fellStrips: 0, awayTall: [], togetherTall: [], voyagesTall: [], smeltersTall: [], heroGrowth: [], heroLines: [], crewOff: [], deadMore: [], bareIslands: [], pastIsland: [], fireHeadsOff: [], fireCrossed: [], giftRowHits: [], british: [], hoverless: [], hoverHeavy: [], hoverCovers: [], foeNamesCut: [], dealtCut: [],
              rows: [], cuts: [], sides: [], heroCompact: false, fireDrawn: 0, fireHeads: 0, giftRows: 0, feedLogs: 0, pickPages: 0, fightPages: 0, awayPages: 0, togetherPages: 0, voyagesPages: 0, smeltersPages: 0, crewsDrawn: 0, crewPeople: 0, moreLinks: 0, barsDrawn: 0, dealtDrawn: 0, damageParts: 0, hoverPages: 0,
              arrowWidths: null, foeNameWidths: null, dealtWidths: null, pinOff: [], pinPages: 0, mealPinOff: [], mealPinPages: 0, mealPinH: [] };
  const over = (rule, what) => r.overlaps.push({ rule, page: n, what });
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
  for (const h of await page.evaluate(collide)) over("fire", h);
  // 0.8.6 the sticky head (Joost in game, a friend's Battle feed: "If you scroll underneath it, it needs to stay up there"): on a plate scrolled
  // down whose strip leads it, the strip stands where it stands at rest, and from the plate's top edge to the strip's foot no row of the plate is
  // drawn over it (every point there that hits the plate hits the strip's holder; a heading chip's tag hanging into it is not the plate's) nor
  // shows through it (the holder's ground is opaque)
  const pinned = await page.evaluate(() => {
    const p = document.querySelector(".content > .plate"); if (!p || p.scrollTop <= 1) return null;
    const first = [...p.children].find(c => !c.classList.contains("page-tip")), s = first && (first.classList.contains("earned") ? first : first.querySelector(":scope > .earned")); if (!s) return null;   // the page's hover tag leads the plate's children, drawn over them
    const at = p.scrollTop, P = p.getBoundingClientRect(), now = s.getBoundingClientRect().top - P.top;
    p.scrollTop = 0; const rest = s.getBoundingClientRect().top - P.top; p.scrollTop = at;
    if (Math.abs(now - rest) > 0.5) return `the strip ${(rest - now).toFixed(1)} px above its place at rest, the plate scrolled ${at} px`;
    const bg = getComputedStyle(first).backgroundColor; if (!/^rgb\(/.test(bg)) return `the strip's ground (${bg}) lets the rows under it show through`;   // opaque: the page's own ground
    const S = s.getBoundingClientRect();
    for (let y = P.top + 1; y < S.bottom - 0.5; y += 3) for (let x = P.left + 2; x < P.right - 2; x += 12) {
      const e = document.elementFromPoint(x, y);
      if (e && e !== p && p.contains(e) && !first.contains(e)) return `"${(e.textContent || e.className || e.tagName).trim().slice(0, 30)}" drawn over the strip at ${Math.round(x - P.left)}, ${Math.round(y - P.top)} (the plate scrolled ${at} px)`;
    }
    return "";
  });
  if (pinned !== null) { r.pinPages++; if (pinned) r.pinOff.push({ page: n, what: pinned }); }
  // 0.9.6 Meal plan's pinned meal (Joost: "Icons only would be good. And then the bar all at one row height"): the island one row, the slots' height and the
  // book's least island padding (MEAL_PIN_MAX px at most); three slots, no food name under them, every filled slot naming its food on hover; one row whose
  // bar and legend stand within the slots' height
  const mealPin = await page.evaluate((max) => {
    const isl = document.querySelector(".pinhead .isl.pin-isl"); if (!isl) return null;
    const h = isl.getBoundingClientRect().height, off = [];
    if (h > max) off.push(`the pinned meal ${h.toFixed(1)} px tall (at most ${max})`);
    if (isl.querySelector(".dr-combo-n")) off.push("food names under the slots");
    const slots = [...isl.querySelectorAll(".dr-combo > .pic")];
    if (slots.length !== 3) off.push(`${slots.length} slots`);
    for (const s of slots) if (!s.querySelector(".dr-slot") && !(s.getAttribute("title") || "").trim()) off.push("a filled slot with no name on hover");
    const rows = isl.querySelectorAll(".bf.dr"); if (rows.length !== 1) off.push(`${rows.length} rows`);
    const combo = isl.querySelector(".dr-combo"), track = isl.querySelector(".bf.dr .dr-track"), legend = isl.querySelector(".bf.dr .dr-pcs");
    if (!combo || !track || !legend) off.push("no slots, track or legend");
    else {
      const S = combo.getBoundingClientRect();
      for (const [what, e] of [["the bar", track], ["the legend", legend]]) { const B = e.getBoundingClientRect(); if (B.top < S.top - 0.5 || B.bottom > S.bottom + 0.5) off.push(`${what} outside the slots' height`); }
      if (legend.getBoundingClientRect().height > 19) off.push("the legend on more than one line");
    }
    return { h, off: off.join("; ") };
  }, MEAL_PIN_MAX);
  if (mealPin) { r.mealPinPages++; r.mealPinH.push(`${n} ${mealPin.h.toFixed(0)} px`); if (mealPin.off) r.mealPinOff.push({ page: n, what: mealPin.off }); }
  // the key line never runs past its room (PanelModel.KeyLine leaves keys out instead; Cooking and Company > Together ran past it, MERGE-NOTES G2/G6)
  // 0.8 hover (BarHover, PICKS 4 B): every bar with a list pairs each part with its row (the same data-k on both), so pointing at either lights the other
  const unpaired = await page.evaluate(() => [...document.querySelectorAll(".bf")].filter(bf => { const ks = sel => [...bf.querySelectorAll(sel)].map(e => e.dataset.k).sort().join(","); return ks(".bf-seg[data-k]") !== ks(".bf-tab .r[data-k]"); }).length);
  if (unpaired) over("pair", `${unpaired} bar(s) whose parts and list rows do not pair up for the hover`);
  // 0.8.2 (Joost: one rule for the whole book): a part that filters and its legend item are one target, and a chosen part is chosen in its legend too
  for (const t of await page.evaluate(() => [...document.querySelectorAll(".bf")].flatMap(bf => [...bf.querySelectorAll(".bf-seg[data-k]")].map(s => {
      const r = bf.querySelector(`.bf-tab .r[data-k="${s.dataset.k}"]`), name = r && r.querySelector(".nm > span:not(.sw)");
      if (!r) return null;
      if ((s.dataset.pick || "") !== (r.dataset.pick || "")) return `part "${name ? name.textContent : s.dataset.k}" filters by "${s.dataset.pick || ""}", its legend item by "${r.dataset.pick || ""}"`;
      if (s.classList.contains("sel") !== !!(name && name.classList.contains("sel"))) return `part "${name ? name.textContent : s.dataset.k}" chosen on ${s.classList.contains("sel") ? "the bar" : "the legend"} only`;
      return null; }).filter(Boolean))))
    over("pick", t);
  if (await page.evaluate(() => !!document.querySelector(".bf-seg[data-pick]"))) r.pickPages++;
  // 0.8.1 review 1: on the group's rows the name gives way, never the date ("as of 8 Oct 00:27" was cut beside any name longer than "Tor")
  for (const t of await page.evaluate(() => [...document.querySelectorAll(".gr-nm i")].filter(i => i.scrollWidth > i.clientWidth + 1).map(i => i.textContent)))
    r.datesCut.push({ page: n, what: `"${t}"` });
  // 0.8.1 (Joost sailing, Battle > Feed: "less than" / "1"): a feed line breaks only between items, at " · ": an amount never splits, nor leaves its
  // type or its "dealt" / "received" on the line before
  for (const t of await page.evaluate(() => [...document.querySelectorAll(".fdl b")].filter(b => {
      const r = b.getClientRects(); if (r.length !== 1) return r.length > 1;
      for (let s = b.previousElementSibling; s && !s.textContent.includes("·"); s = s.previousElementSibling) { const q = s.getClientRects(); if (q.length !== 1 || Math.abs(q[0].top - r[0].top) > 2) return true; }
      return false; }).map(b => b.textContent)))
    r.feedSplit.push({ page: n, what: `"${t}"` });
  // the received words (the Log's column, a card's line) are plain text: each item joined by no-break spaces, so they too break only at " · "
  for (const t of await page.evaluate(() => [...document.querySelectorAll(".bt-fl .rcv, .bt-card .rcv")].flatMap(e => e.textContent.split(" · ")).filter(item => item.includes(" "))))
    r.feedSplit.push({ page: n, what: `"${t}" (received column)` });
  // REVIEW-081 0.8.2 pass: every fight is its own island, and the Log's bars still start at one x and share one track over the whole page (the
  // columns measured over every fight, PanelModel.FeedPageEntries): a fight where nothing hurt you gets no longer track than the one above it
  const tracks = await page.evaluate(() => [...document.querySelectorAll(".bt-fl .drl")].map(e => { const r = e.getBoundingClientRect(); return [r.left, r.width, e.closest(".bt-fl").parentElement]; })
    .map(([x, w, fight], i, all) => [x, w, new Set(all.map(t => t[2])).size]));
  // 0.9 (Joost: the fight on one screen at the book's size): a fight's or a run's page (Chapters/FightPages.cs) fits the plate without scrolling;
  // only a row opened by a click or the About box may add to it. 0.8.6 (the lead, A): the head, the foe rows and the players fit; By type, together,
  // the one island at the page's foot (its bar's .cmp), may lie under the fold: its height and the gap above it are left out
  const tall = await page.evaluate(() => { const s = JSON.stringify(window.CURRENT.blocks || []);
    if (!s.includes('"kind":"fighthead"') || /"kind":"aboutnumbers"[^}]*"open":1/.test(s) || /"kind":"foe"[^}]*"selected":1/.test(s) || /"kind":"fightfoerows"[^}]*"tone":"dealt"/.test(s)) return null;   // stacked (not the default) takes two islands of rows
    const p = document.querySelector(".plate"); if (!p) return null;
    const foot = [...p.querySelectorAll(".isl")].filter(i => i.querySelector(".cmp")).pop(), prev = foot && foot.previousElementSibling;
    const under = foot ? foot.getBoundingClientRect().bottom - (prev ? prev.getBoundingClientRect().bottom : foot.getBoundingClientRect().top) : 0;
    return Math.round(p.scrollHeight - p.clientHeight - under); });
  if (tall !== null) { r.fightPages++; if (tall > 1) r.fightTall.push({ page: n, what: `${tall} px taller than the plate` }); }
  // 0.8.5 (Joost: "Overview should fit in one screen"): Battle > Overview fits the plate in every window, nine lands found or a fellow's copy
  // too; only the About box may add to it
  const ovTall = await page.evaluate(() => { const v = window.CURRENT; if (!v || v.active !== "Battle" || v.page !== "overview" || /"kind":"aboutnumbers"[^}]*"open":1/.test(JSON.stringify(v.blocks || []))) return null;
    const p = document.querySelector(".plate"); return p ? p.scrollHeight - p.clientHeight : null; });
  if (ovTall !== null) { r.overviewPages++; if (ovTall > 1) r.overviewTall.push({ page: n, what: `${ovTall} px taller than the plate` }); }
  // 0.8.5 Where you fell, the tiles grow: a tile with more falls is taller, as tall for as many (the drawn heights, against each tile's count)
  const fell = await page.evaluate(() => [...document.querySelectorAll(".bt-dz")].map(z => [...z.querySelectorAll(".bt-col[data-count]")].map(c => [+c.dataset.count, c.querySelector(".bt-tile").getBoundingClientRect().height, c.querySelector(".bt-nm").textContent])));
  for (const tiles of fell) {
    r.fellStrips++;
    for (const [a, ha, na] of tiles) for (const [b, hb, nb] of tiles) if (a < b && !(ha < hb - 0.5)) r.fellOff.push({ page: n, what: `${na} (${a} falls) ${ha.toFixed(1)} px, ${nb} (${b}) ${hb.toFixed(1)} px` }); else if (a === b && Math.abs(ha - hb) > 0.5) r.fellOff.push({ page: n, what: `${na} and ${nb} (${a} falls each) ${ha.toFixed(1)} and ${hb.toFixed(1)} px` });
  }
  // 0.9 History (Joost: "shouldn't those bars be different heights?"): a day with more is a taller column, as tall for as many (the drawn stacks, against each day's value)
  const hist = await page.evaluate(() => [...document.querySelectorAll(".hist")].map(c => [...c.querySelectorAll(".hit[data-col]")].map(hit => { const k = hit.dataset.col;
    const h = [...c.querySelectorAll(`i[data-col="${k}"]`)].filter(i => !i.classList.contains("z")).reduce((a, i) => a + i.getBoundingClientRect().height, 0); return [parseFloat(hit.dataset.v) || 0, h, hit.title.split(" · ")[0]]; })));
  for (const cols of hist) {
    r.histCharts++; if (new Set(cols.filter(([v]) => v > 0).map(([, h]) => Math.round(h))).size > 1) r.histVaried++;
    // 0.9.5 (Damage history, thousands a day): two days closer than the chart's pixel (2197 and 2200 on a 4 000 scale) may stand as tall
    const vmax = Math.max(0, ...cols.map(([v]) => v)), hmax = Math.max(0, ...cols.map(([, h]) => h)), apart = (a, b) => vmax <= 0 || (b - a) * hmax / vmax >= 1.5;
    for (const [a, ha, na] of cols) for (const [b, hb, nb] of cols) if (a < b && apart(a, b) && !(ha < hb - 0.5)) r.histOff.push({ page: n, what: `${na} (${a}) ${ha.toFixed(1)} px, ${nb} (${b}) ${hb.toFixed(1)} px` }); else if (a === b && Math.abs(ha - hb) > 0.5) r.histOff.push({ page: n, what: `${na} and ${nb} (${a} each) ${ha.toFixed(1)} and ${hb.toFixed(1)} px` });
  }
  // 0.8.5 (Joost: Company > While away on one screen): the page fits the plate without scrolling with up to five fellows (a sixth scrolls), About shut
  const away = await page.evaluate(() => { const find = bs => { for (const b of bs || []) { if (b.kind === "awayrows") return b; const f = find(b.items); if (f) return f; } return null; };
    const rows = find(window.CURRENT.blocks); if (!rows || rows.items.length > 5 || /"kind":"aboutnumbers"[^}]*"open":1/.test(JSON.stringify(window.CURRENT.blocks))) return null;   // About open may add to it
    const p = document.querySelector(".plate"); return p ? { n: rows.items.length, over: p.scrollHeight - p.clientHeight } : null; });
  // 0.8.5 (Joost: the damage grammar across the book): Company > Together fits the plate with three fellows, whichever category or window is
  // chosen; About open, Compare's rows and the line a pressed greyed day chip adds may add to it
  const tg = await page.evaluate(() => { const items = ((window.CURRENT.blocks || [])[0] || {}).items || [], at = items.findIndex(b => b.kind === "together"); if (at < 0) return null;
    const t = items[at], cats = (t.items || []).filter(c => c.kind === "category");
    if (!cats.length || (cats[0].items || []).length > 4 || (t.items || []).some(i => i.kind === "comparerows") || items.slice(0, at).some(b => b.kind === "note") || items.some(b => b.kind === "aboutnumbers" && b.open)) return null;
    const p = document.querySelector(".plate"); return p ? p.scrollHeight - p.clientHeight : null; });
  if (tg !== null) { r.togetherPages++; if (tg > 1) r.togetherTall.push({ page: n, what: `${tg} px taller than the plate` }); }
  // 0.8.5 (Joost's evening: Everyone > Smelters with the chests' tiles needed the wheel): the group's Smelters fits the plate with four players, About shut
  if (n.startsWith("everyone-hall-smelters")) {
    const sm = await page.evaluate(() => { if (/"kind":"aboutnumbers"[^}]*"open":1/.test(JSON.stringify(window.CURRENT.blocks))) return null;
      const p = document.querySelector(".plate"); return p ? p.scrollHeight - p.clientHeight : null; });
    if (sm !== null) { r.smeltersPages++; if (sm > 1) r.smeltersTall.push({ page: n, what: `${sm} px taller than the plate` }); }
  }
  // 0.8.5 (v085-together): Everyone on Voyages > Sailing and Cargo fits the plate with four players in every window (the day windows add the Compare line);
  // (v085-fellowvoy) a book's own Sailing and Cargo in a day window too, a fellow's with its two lines of what their days hold; Compare's rows may add to it
  const vy = await page.evaluate(() => { const c = window.CURRENT; if (c.active !== "Voyages" || (c.page !== "sailing" && c.page !== "cargo") || (c.compare && c.compare.selected)) return null;
    const find = bs => { for (const b of bs || []) { if (b.kind === "grouprows") return b; const f = find(b.items); if (f) return f; } return null; };
    const day = (c.windows || []).some(w => w.selected && ["Today", "SevenDays", "ThirtyDays"].includes(w.id));
    if (c.everyoneOn) { const rows = find(c.blocks); if (!rows || rows.items.length > 4) return null; } else if (!day) return null;
    const p = document.querySelector(".plate"); return p ? p.scrollHeight - p.clientHeight : null; });
  if (vy !== null) { r.voyagesPages++; if (vy > 1) r.voyagesTall.push({ page: n, what: `${vy} px taller than the plate` }); }
  if (away !== null) { r.awayPages++; if (away.over > 1) r.awayTall.push({ page: n, what: `${away.n} fellows, ${away.over} px taller than the plate` }); }
  if (tracks.length) {
    r.feedLogs++;
    const odd = tracks.filter(t => Math.abs(t[0] - tracks[0][0]) > 0.5 || Math.abs(t[1] - tracks[0][1]) > 0.5);
    if (odd.length) r.feedTracks.push({ page: n, what: `${odd.length} of ${tracks.length} Log bars (${tracks[0][2]} fights) off the first one's x ${tracks[0][0].toFixed(1)} and width ${tracks[0][1].toFixed(1)}, e.g. ${odd[0][0].toFixed(1)} and ${odd[0][1].toFixed(1)}` });
  }
  if (await page.evaluate(() => !!document.querySelector(".bt-foerow"))) {   // the widths of the longest names and numbers, measured once on a page with the Foes table
    r.arrowWidths = await page.evaluate(([names, min]) => { const c = document.createElement("canvas").getContext("2d"); c.font = `${min}px "Averia Serif Libre"`;
      return names.map(t => [t, Math.ceil(c.measureText(t).width * 1.04)]); }, [LONG_ARROWS, MIN]);
    r.foeNameWidths = await page.evaluate(([texts]) => { const c = document.createElement("canvas").getContext("2d"); c.font = `16px "Averia Serif Libre"`;
      return texts.map(t => [t, Math.ceil(c.measureText(t).width * 1.04)]); }, [LONG_FOES]);
    r.dealtWidths = await page.evaluate(([texts]) => { const c = document.createElement("canvas").getContext("2d"); c.font = `700 16px "Averia Serif Libre"`;
      return texts.map(t => [t, Math.ceil(c.measureText(t).width * 1.04)]); }, [DEALT_WIDE]);
  }
  for (const t of await page.evaluate(() => [...document.querySelectorAll(".bt-foerow .fn")].filter(e => e.scrollWidth > e.clientWidth + 0.5).map(e => e.textContent)))
    r.foeNamesCut.push({ page: n, what: `"${t}"` });
  for (const [t, past] of await page.evaluate(() => [...document.querySelectorAll(".bt-foerow .bt-c.dl")].map(c => { const b = c.querySelector("b"), r = c.getBoundingClientRect(), q = b.getBoundingClientRect();
      return [b.textContent, q.right - r.right]; }))) { r.dealtDrawn++; if (past > 0.5) r.dealtCut.push({ page: n, what: `"${t}" ${past.toFixed(1)} px past its cell` }); }
  const keysOver = await page.evaluate(() => { const k = document.querySelector(".keys"); return k && k.scrollWidth > k.clientWidth + 1 ? k.textContent.replace(/\s+/g, " ").trim() : null; });
  if (keysOver) over("keys", `the key line is wider than its room: "${keysOver}"`);
  // 0.8 layout D+ (Joost on the board): the hero's growth line takes no height of its own: each hero is as tall with it as without it
  const taller = await page.evaluate(() => [...document.querySelectorAll(".hero")].map(h => { const sp = h.querySelector(".hspark"); if (!sp || sp.style.display === "none") return null;
    const was = h.offsetHeight; sp.style.display = "none"; const without = h.offsetHeight; sp.style.display = ""; return was > without ? `${was} px with it, ${without} without` : null; }).filter(Boolean));
  for (const t of taller) r.heroGrowth.push({ page: n, what: t });
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
    return out; })) r.heroLines.push({ page: n, what: t });
  r.heroCompact = await page.evaluate(() => !!document.querySelector(".hero .hskill.compact"));   // information: the skill in its compact form
  // 0.8 layout D+: one legend grid per page: every legend laid on it, all with the same column count (never mixed)
  const grids = await page.evaluate(() => [...document.querySelectorAll(".bf-list")].filter(l => l.querySelector(".r")).map(l => l.dataset.page || "none"));
  if (new Set(grids).size > 1 || grids.includes("none")) over("grid", `legends not on one grid: ${grids.join(", ")}`);
  // the Fireside (0.8.5, Joost in game with four players: "arrows are kinda wonky"), measured as drawn: the page draws the plan the dump wrote
  // (window.FIRE_PLANS, FireLayout.ToJson) with every thread; every arrow's tip on its receiver's rim (FireLayout.Rim, 2 px either way), the head
  // along its line's last piece; no count on a point where two threads cross (two counts over each other: the "fire" rule above); and in the list
  // beside the fire no text of a row over another ("feas× 5": the count over the feast's words)
  const fm = await page.evaluate((n) => {
    const out = { drawn: 0, heads: 0, off: [], hits: [], rows: 0, rowHits: [] }, plan = (window.FIRE_PLANS || {})[n], svg = document.querySelector("svg[data-fire-rim]");
    if (!plan && !svg) return out;
    out.drawn = 1;
    if (!plan || !svg) { out.off.push(plan ? "the page drew no Fireside" : "the dump wrote no plan"); return out; }
    const groups = [...svg.querySelectorAll("g[data-to]")];
    if (groups.length !== plan.threads.length) out.off.push(`${groups.length} threads drawn of the plan's ${plan.threads.length}`);
    const sr = svg.getBoundingClientRect(), rim = parseFloat(svg.dataset.fireRim), lines = [];
    const P = s => s.trim().split(/\s+/).map(q => q.split(",").map(Number)).map(([x, y]) => [sr.left + x, sr.top + y]);
    for (const g of groups) {
      const tri = P(g.querySelector("polygon").getAttribute("points")), line = P(g.querySelector("polyline").getAttribute("points")); lines.push(line);
      const sh = document.querySelector(`[data-fire="shield"][data-id="${CSS.escape(g.dataset.to)}"]`); if (!sh) { out.off.push(`${g.dataset.from}>${g.dataset.to}: no shield`); continue; }
      const r = sh.getBoundingClientRect(), c = [r.left + r.width / 2, r.top + r.height / 2], tip = tri[0], d = Math.hypot(tip[0] - c[0], tip[1] - c[1]); out.heads++;
      const base = [(tri[1][0] + tri[2][0]) / 2, (tri[1][1] + tri[2][1]) / 2], a = line[line.length - 1];
      let k = line.length - 2; while (k > 0 && Math.hypot(line[k][0] - a[0], line[k][1] - a[1]) < 1.5) k--; const b = line[k];   // the line's last piece into the head
      const ang = Math.abs(Math.atan2(tip[1] - base[1], tip[0] - base[0]) - Math.atan2(a[1] - b[1], a[0] - b[0])) * 180 / Math.PI, turn = Math.min(ang, 360 - ang);
      if (Math.abs(d - rim) > 2) out.off.push(`${g.dataset.from}>${g.dataset.to}: tip ${d.toFixed(1)} px from the shield's middle (the rim is ${rim})`);
      if (turn > 10) out.off.push(`${g.dataset.from}>${g.dataset.to}: head ${turn.toFixed(0)} degrees off its line's end`);
    }
    const cross = [], cr = (o, p, q) => (p[0] - o[0]) * (q[1] - o[1]) - (p[1] - o[1]) * (q[0] - o[0]);
    for (let i = 0; i < lines.length; i++) for (let j = i + 1; j < lines.length; j++) for (let s = 0; s + 1 < lines[i].length; s++) for (let t = 0; t + 1 < lines[j].length; t++) {
      const a = lines[i][s], b = lines[i][s + 1], c = lines[j][t], d = lines[j][t + 1], d1 = cr(c, d, a), d2 = cr(c, d, b);
      if (d1 * d2 < 0 && cr(a, b, c) * cr(a, b, d) < 0) { const k = d1 / (d1 - d2); cross.push([a[0] + (b[0] - a[0]) * k, a[1] + (b[1] - a[1]) * k]); } }
    for (const e of document.querySelectorAll('[data-fire="pill"]')) { const r = e.getBoundingClientRect();
      if (cross.some(x => x[0] > r.left && x[0] < r.right && x[1] > r.top && x[1] < r.bottom)) out.hits.push(`count "${e.textContent.trim()}" (${e.dataset.gift}) sits on a crossing`); }
    for (const row of document.querySelectorAll(".gift-row")) {
      out.rows++; const runs = [], w = document.createTreeWalker(row, NodeFilter.SHOW_TEXT);
      for (let t = w.nextNode(); t; t = w.nextNode()) { if (!t.nodeValue.trim()) continue; const rg = document.createRange(); rg.selectNodeContents(t); for (const r of rg.getClientRects()) if (r.width > 0.5) runs.push({ t: t.nodeValue.trim(), r }); }
      for (let i = 0; i < runs.length; i++) for (let j = i + 1; j < runs.length; j++) { const a = runs[i].r, b = runs[j].r;
        if (Math.min(a.right, b.right) - Math.max(a.left, b.left) > 0.5 && Math.min(a.bottom, b.bottom) - Math.max(a.top, b.top) > 0.5) out.rowHits.push(`"${runs[i].t}" over "${runs[j].t}"`); }
    }
    return out;
  }, n);
  r.fireDrawn += fm.drawn; r.fireHeads += fm.heads; r.giftRows += fm.rows;
  for (const o of fm.off) r.fireHeadsOff.push({ page: n, what: o }); for (const o of fm.hits) r.fireCrossed.push({ page: n, what: o }); for (const o of fm.rowHits) r.giftRowHits.push({ page: n, what: o });
  // 0.8.2 (Joost in game, The Mead Hall: "I want to see everyone ... not plus three more"): a feat's crew shows every person whole inside its detail
  // area, none over another; and no "+N more" anywhere is dead words: each is a click (data-link) that shows the rest
  const crew = await page.evaluate(() => { const out = { crews: 0, people: 0, off: [] };
    for (const cr of document.querySelectorAll(".fdet > .fcrew")) {
      out.crews++; const det = cr.parentElement.getBoundingClientRect(), boxes = [];
      for (const e of cr.querySelectorAll(".ce")) { out.people++; const nm = e.querySelector(".cn"), r = [e.querySelector(".sw, .cmk"), nm, e.querySelector(".cnum"), e.querySelector(".csh")].filter(Boolean).map(x => x.getBoundingClientRect());
        const b = { l: Math.min(...r.map(q => q.left)), t: Math.min(...r.map(q => q.top)), r: Math.max(...r.map(q => q.right)), b: Math.max(...r.map(q => q.bottom)), who: nm.textContent };
        if (nm.getBoundingClientRect().width < 1 || b.b > det.bottom - 2 + 0.5 || b.r > det.right - 16 + 0.5) out.off.push(`${b.who} outside the detail area`);
        for (const o of boxes) if (Math.min(o.r, b.r) - Math.max(o.l, b.l) > 0.5 && Math.min(o.b, b.b) - Math.max(o.t, b.t) > 0.5) out.off.push(`${b.who} over ${o.who}`);
        boxes.push(b); } }
    return out; });
  r.crewsDrawn += crew.crews; r.crewPeople += crew.people; for (const o of crew.off) r.crewOff.push({ page: n, what: o });
  const more = await page.evaluate(() => { const w = document.createTreeWalker(document.body, NodeFilter.SHOW_TEXT), out = { links: 0, dead: [] };
    for (let t = w.nextNode(); t; t = w.nextNode()) { const txt = t.nodeValue.replace(/\s+/g, " ").trim(), el = t.parentElement;
      if (!/^\+[\d\s]+ more$/.test(txt) || !el || el.closest("#caption")) continue; const cs = getComputedStyle(el); if (cs.visibility === "hidden" || cs.display === "none" || !el.getClientRects().length) continue;
      if (el.closest("[data-link]")) out.links++; else out.dead.push(txt); }
    return out; });
  r.moreLinks += more.links; for (const d of more.dead) r.deadMore.push({ page: n, what: d });
  // 0.8.5 (Joost in game, Everyone > Smelters: "Put in together, by kind" stood as a heading with nothing under it): a bar's island always
  // draws its bar or its rows, never its heading alone (a one-kind bar without a bar shows its one row)
  const bare = await page.evaluate(() => { const out = { bars: 0, bare: [] };
    for (const c of document.querySelectorAll(".cmp")) { if (!c.getClientRects().length) continue; out.bars++;
      if (!c.querySelector(".bf-seg, .bf .r")) out.bare.push(((c.querySelector(".cmp-h") || c).textContent || "").replace(/\s+/g, " ").trim()); }
    return out; });
  r.barsDrawn += bare.bars; for (const b of bare.bare) r.bareIslands.push({ page: n, what: `"${b}" has no bar and no rows` });
  // 0.8.5 (the Hall overview: "1 340 items put in by hand" ran past its half island, unclipped): no visible text runs past its island's
  // edge; text a box clips is the clip rule's ("no text cut at the side of a box that clips it")
  const past = await page.evaluate(() => { const out = [];
    for (const isl of document.querySelectorAll(".isl")) { if (!isl.getClientRects().length) continue; const ir = isl.getBoundingClientRect();
      const w = document.createTreeWalker(isl, NodeFilter.SHOW_TEXT);
      for (let t = w.nextNode(); t; t = w.nextNode()) { const txt = t.nodeValue.replace(/\s+/g, " ").trim(), el = t.parentElement; if (!txt || !el) continue;
        const cs = getComputedStyle(el); if (cs.visibility === "hidden" || cs.display === "none") continue;
        const range = document.createRange(); range.selectNodeContents(t);
        for (const rc of range.getClientRects()) { if (rc.width < 1) continue; let l = rc.left, r = rc.right;
          for (let a = el; a && a !== isl; a = a.parentElement) { const o = getComputedStyle(a); if (/hidden|clip|auto|scroll/.test(o.overflowX)) { const ar = a.getBoundingClientRect(); l = Math.max(l, ar.left); r = Math.min(r, ar.right); } }
          if (r > ir.right + 1 || l < ir.left - 1) { out.push(`"${txt.slice(0, 40)}" ${Math.round(Math.max(r - ir.right, ir.left - l))} px past its island`); break; } } } }
    return out; });
  for (const x of past) r.pastIsland.push({ page: n, what: x });
  const runs = await page.evaluate(measure, MIN);
  const bad = runs.filter(x => x.eff < (x.bar ? BARMIN : MIN) - 0.01);
  r.perPage = { runs: runs.length, bad: bad.length, cut: runs.filter(x => x.cut).length };
  for (const c of runs.filter(x => x.cut)) r.cuts.push({ page: n, text: c.text, sel: c.sel });
  for (const c of runs.filter(x => x.side)) r.sides.push({ page: n, text: c.text, sel: c.sel });
  // text of one block drawn over text of another (Farming's "Other plants" row over the planted/picked legend, MERGE-NOTES G3): the page's
  // blocks stack, so any two text runs from different blocks that cover each other mean a block was drawn outside its own room
  const stacked = runs.filter(x => x.blk >= 0);
  for (let i = 0; i < stacked.length; i++) for (let j = i + 1; j < stacked.length; j++) {
    const a = stacked[i], b = stacked[j]; if (a.blk === b.blk) continue;
    const w = Math.min(a.box[2], b.box[2]) - Math.max(a.box[0], b.box[0]), h = Math.min(a.box[3], b.box[3]) - Math.max(a.box[1], b.box[1]);
    if (w > 1 && h > 1) over("blocks", `text "${a.text}" x text "${b.text}" (two blocks)`);
  }
  // 0.9.10: two texts of one hero or stat row never cover each other (the name over its land drawn on the first big number)
  // (a hero or stat row, else the block the text stands in; the fade's cue, the hover tag and turned column heads aside)
  const inRow = runs.filter(x => (x.row || x.blk >= 0) && !x.overlay && !x.turned).map(x => ({ ...x, key: x.row ? "r" + x.row : "b" + x.blk, rowKind: x.rowKind || "block" }));
  for (let i = 0; i < inRow.length; i++) for (let j = i + 1; j < inRow.length; j++) {
    const a = inRow[i], b = inRow[j]; if (a.key !== b.key) continue;
    const w = Math.min(a.ink[2], b.ink[2]) - Math.max(a.ink[0], b.ink[0]), h = Math.min(a.ink[3], b.ink[3]) - Math.max(a.ink[1], b.ink[1]);
    if (w > 1 && h > 1) r.rowOver.push({ page: n, what: `${a.rowKind}: "${a.text}" x "${b.text}" (${Math.round(w)} x ${Math.round(h)} px)` });
  }
  r.rowTexts += inRow.length;
  for (const b of bad) r.rows.push({ page: n, ...b });
  // 0.8.1 US spelling: every word the page draws (innerText skips what is hidden), the preview's own footnote (#caption) left out
  const drawn = await page.evaluate(() => { const c = document.getElementById("caption"); return document.body.innerText.replace(c ? c.innerText : "\u0000", " "); });
  for (const w of new Set(drawn.match(BRITISH) || [])) r.british.push({ page: n, what: w });
  // last on the page (it points at every part): every damage bar part shows its name and number on hover, and the hover costs no element per part
  // (0.8.2, Joost in game: "that mouse over should always be visible on all of them"): damage rows, the feed's Log and cards, the foe detail,
  // Everyone's Dealt bars, the armor bar. Each part is pointed at in turn (mouseover, then mouseout) and what shows is read; at most one tag on a
  // page, one label in an entry (PanelUi.PageTag, the entry's own words swapped; Unity counts the same in KsPanelTest's HoverCostTests)
  const hv = await page.evaluate(() => {
    const miss = [], covers = [], segs = [...document.querySelectorAll(".dr-seg")], tip = document.querySelector(".page-tip");
    const lit = [...document.querySelectorAll(".bf .hov[data-k]")];   // a hovered-state shot's lit part: lit again after
    for (const x of lit) x.dispatchEvent(new MouseEvent("mouseout", { bubbles: true }));
    const name = (bf, k) => `${(bf && bf.querySelector(".nm, b") || { textContent: "?" }).textContent.trim()}, part ${k}`;
    for (const s of segs) {
      const bf = s.closest(".bf"), k = s.dataset.k;
      if (!bf || k == null) { miss.push(name(bf, k) + ": no bar or no key"); continue; }
      s.dispatchEvent(new MouseEvent("mouseover", { bubbles: true }));
      const entry = [...bf.querySelectorAll(`.dr-e[data-k="${k}"] .w`)].find(w => w.dataset.on && w.textContent === w.dataset.on && w.offsetParent);
      const shown = entry ? entry.textContent : tip && tip.classList.contains("on") && tip.offsetParent && s.dataset.tip && tip.textContent === s.dataset.tip ? tip.textContent : "";
      // 0.8.6: the tag sits beside its bar (below it, or above it at the plate's or island's edge), never over it: its rect and the bar's do not meet
      const tr = tip && tip.getBoundingClientRect(), br = (s.closest("[data-tag-x]") || s.closest(".dr-track") || s).getBoundingClientRect();
      const over = !entry && tip && tip.classList.contains("on") && tr.left < br.right && tr.right > br.left && tr.top < br.bottom && tr.bottom > br.top;
      s.dispatchEvent(new MouseEvent("mouseout", { bubbles: true }));
      const after = [...bf.querySelectorAll(`.dr-e[data-k="${k}"] .w`)].every(w => w.textContent === (w.dataset.off ?? w.textContent)) && !(tip && tip.classList.contains("on"));
      if (over) covers.push(name(bf, k) + ": the tag covers its bar");
      if (!shown.trim() || !after) miss.push(name(bf, k) + (!shown.trim() ? ": nothing shown" : ": not set back on leaving"));
    }
    for (const x of lit) x.dispatchEvent(new MouseEvent("mouseover", { bubbles: true }));
    const tips = document.querySelectorAll(".dr-tip").length, labels = [...document.querySelectorAll(".dr-e .w")].filter(w => w.children.length > 0).length + document.querySelectorAll(".dr-e .b").length;
    return { parts: segs.length, miss, covers, tips, labels };
  });
  r.damageParts += hv.parts; if (hv.parts) r.hoverPages++;
  for (const m of hv.miss) r.hoverless.push({ page: n, what: m });
  for (const m of hv.covers) r.hoverCovers.push({ page: n, what: m });
  if (hv.tips > 1 || hv.labels > 0) r.hoverHeavy.push({ page: n, what: `${hv.tips} hover tags (at most 1 a page), ${hv.labels} entries with a second label` });
  return r;
}

// the workers: each its own browser context and tab, taking the next preview until none is left
const results = new Array(names.length), pageErrors = [];
let next = 0;
async function worker() {
  const ctx = await browser.newContext({ viewport: { width: 1280, height: 860 } });
  const page = await ctx.newPage();
  let current = null; page.on("pageerror", e => pageErrors.push({ page: current, what: e.message }));
  for (let i = next++; i < names.length; i = next++) { current = names[i]; results[i] = await shoot(page, names[i]); }
  await ctx.close();
}
await Promise.all(Array.from({ length: Math.min(JOBS, names.length) }, worker));
await browser.close();
const all = (k) => results.flatMap(r => r[k]), sum = (k) => results.reduce((a, r) => a + r[k], 0), firstOf = (k) => (results.find(r => r[k]) || {})[k] || null;
const rows = all("rows"), cuts = all("cuts"), sides = all("sides"), overlaps = all("overlaps");
const arrowWidths = firstOf("arrowWidths"), foeNameWidths = firstOf("foeNameWidths"), dealtWidths = firstOf("dealtWidths");
const whole = !val("--only");   // a full run must have drawn each kind of thing it checks; --only may not

// the report: one line per rule, the offenders under a failing one (all of them with --verbose)
let failed = [];
const noFail = flag("--no-fail");
function rule(name, ok, stat, offenders = []) {
  console.log(`${ok ? "PASS" : "FAIL"}  ${name}: ${stat}`);
  if (!ok) { failed.push(name); for (const o of offenders.slice(0, flag("--verbose") ? offenders.length : 10)) console.log(`        ${o.page ? o.page + ": " : ""}${o.what ?? o}`); if (offenders.length > 10 && !flag("--verbose")) console.log(`        ... ${offenders.length - 10} more (--verbose)`); }
}
const info = (name, stat) => console.log(`info  ${name}: ${stat}`);
const count = (xs, none) => xs.length ? `${xs.length}` : none;
const chapter = (n) => n.startsWith("battle") || n.endsWith("-battle") ? "battle" : (n.split("-")[0].replace(/^(edda|tor|finch)$/, "fellows"));
console.log(`rendered ${names.length} previews in ${((Date.now() - started) / 1000).toFixed(0)} s with ${Math.min(JOBS, names.length)} workers`);
if (flag("--verbose")) {
  const byChap = {}; names.forEach((n, i) => { const c = chapter(n), p = results[i].perPage; (byChap[c] ??= { pages: 0, runs: 0, bad: 0 }); byChap[c].pages++; byChap[c].runs += p.runs; byChap[c].bad += p.bad; });
  console.log("per chapter (pages / text runs / under the floor):"); for (const [c, v] of Object.entries(byChap)) console.log(`  ${c.padEnd(10)} ${v.pages} / ${v.runs} / ${v.bad}`);
}
const groups = {}; for (const x of rows) { const k = `${x.eff}px  ${x.sel}`; (groups[k] ??= { n: 0, pages: new Set(), sample: x.text }).n++; groups[k].pages.add(x.page); }
rule(`text at or above the ${MIN} px floor (${BARMIN} px for the digits inside a damage bar)`, !rows.length, `${results.reduce((a, r) => a + r.perPage.runs, 0)} text runs measured, ${count(rows, "none under")}`,
     Object.entries(groups).sort((a, b) => b[1].n - a[1].n).map(([k, g]) => `${String(g.n).padStart(4)}x ${k}   e.g. "${g.sample}"   [${[...g.pages].slice(0, 4).join(", ")}${g.pages.size > 4 ? ", +" + (g.pages.size - 4) : ""}]`));
rule("no text cut at the side of a box that clips it", !sides.length, count(sides, "none"), sides.map(c => ({ page: c.page, what: `${c.sel}  "${c.text}"` })));
if (flag("--strict-cut")) rule("no text cut off by its own box (--strict-cut)", !cuts.length, count(cuts, "none"), cuts.map(c => ({ page: c.page, what: `${c.sel}  "${c.text}"` })));
else { const cutBy = {}; for (const c of cuts) { const k = chapter(c.page); cutBy[k] = (cutBy[k] || 0) + 1; } info("text cut off by its own box", cuts.length ? Object.entries(cutBy).map(([c, n]) => `${c} ${n}`).join(", ") : "none"); }
rule("the one-hero rule: a hero line's numbers, labels and dates on one baseline, the skill never on a line of its own", !all("heroLines").length, count(all("heroLines"), "every hero"), all("heroLines"));
info("heroes whose skill takes its compact form", names.filter((n, i) => results[i].heroCompact).join(", ") || "none");
rule("a hero is no taller with its growth line", !all("heroGrowth").length, count(all("heroGrowth"), "none taller"), all("heroGrowth"));
rule("a group row's date is never cut (the name gives way)", !all("datesCut").length, count(all("datesCut"), "none cut"), all("datesCut"));
rule("a feed line breaks only between items, never inside an amount", !all("feedSplit").length, count(all("feedSplit"), "none broken"), all("feedSplit"));
rule("the battle feed's Log bars share one x and one track across its fights", !all("feedTracks").length && (sum("feedLogs") > 0 || !whole), `${sum("feedLogs")} pages, ` + (all("feedTracks").length ? all("feedTracks").length + " differ" : sum("feedLogs") ? "all the same" : "none drawn"), all("feedTracks"));
rule("Company > While away fits one screen with up to five fellows (About shut)", !all("awayTall").length && (sum("awayPages") > 0 || !whole), `${sum("awayPages")} pages, ` + (all("awayTall").length ? all("awayTall").length + " taller" : sum("awayPages") ? "all fit" : "none drawn"), all("awayTall"));
rule("Everyone > Smelters fits one screen with four players (About shut)", !all("smeltersTall").length && (sum("smeltersPages") > 0 || !whole), `${sum("smeltersPages")} pages, ` + (all("smeltersTall").length ? all("smeltersTall").length + " taller" : sum("smeltersPages") ? "all fit" : "none drawn"), all("smeltersTall"));
rule("Voyages > Sailing and Cargo fit one screen: Everyone with four players in every window, a book in the day windows (no Compare)", !all("voyagesTall").length && (sum("voyagesPages") > 0 || !whole), `${sum("voyagesPages")} pages, ` + (all("voyagesTall").length ? all("voyagesTall").length + " taller" : sum("voyagesPages") ? "all fit" : "none drawn"), all("voyagesTall"));
rule("Company > Together fits one screen with three fellows (About shut, no Compare)", !all("togetherTall").length && (sum("togetherPages") > 0 || !whole), `${sum("togetherPages")} pages, ` + (all("togetherTall").length ? all("togetherTall").length + " taller" : sum("togetherPages") ? "all fit" : "none drawn"), all("togetherTall"));
rule("a fight's page fits one screen (no row open, About shut)", !all("fightTall").length && (sum("fightPages") > 0 || !whole), `${sum("fightPages")} pages, ` + (all("fightTall").length ? all("fightTall").length + " taller" : sum("fightPages") ? "all fit" : "none drawn"), all("fightTall"));
rule("Battle > Overview fits one screen in every window (nine lands found, a fellow's copy; About shut)", !all("overviewTall").length && (sum("overviewPages") > 0 || !whole), `${sum("overviewPages")} pages, ` + (all("overviewTall").length ? all("overviewTall").length + " taller" : sum("overviewPages") ? "all fit" : "none drawn"), all("overviewTall"));
rule(`Meal plan's pinned meal is one row (at most ${MEAL_PIN_MAX} px): three slots as pictures only, each filled one named on hover, the bar and its one-line legend within the slots' height`,
  !all("mealPinOff").length && (sum("mealPinPages") > 0 || !whole), `${sum("mealPinPages")} pages (${all("mealPinH").join(", ")}), ` + count(all("mealPinOff"), "all one row"), all("mealPinOff"));
rule("the sticky head: on a scrolled plate the strip stays at its top and no row is drawn over it", !all("pinOff").length && (sum("pinPages") > 0 || !whole), `${sum("pinPages")} scrolled pages, ` + count(all("pinOff"), "the strip pinned on each"), all("pinOff"));
rule("History: a day with more is a taller column, as tall for as many; the history sample has days of different heights", !all("histOff").length && (sum("histVaried") > 0 || !whole), `${sum("histCharts")} charts, ${sum("histVaried")} with varied days, ` + count(all("histOff"), "every column follows its count"), all("histOff"));
rule("Where you fell: taller where more fell, as tall for as many", !all("fellOff").length && (sum("fellStrips") > 0 || !whole), `${sum("fellStrips")} strips, ` + count(all("fellOff"), "every height follows its count"), all("fellOff"));
rule("a feat's crew: every person whole inside its detail area, none over another", !all("crewOff").length && (sum("crewsDrawn") > 0 || !whole), `${sum("crewsDrawn")} crews, ${sum("crewPeople")} people, ` + count(all("crewOff"), "none off"), all("crewOff"));
rule("every \"+N more\" is a click that shows the rest", !all("deadMore").length, `${sum("moreLinks")} clicks, ` + count(all("deadMore"), "none dead"), all("deadMore"));
rule("no text runs past its island's edge (unclipped)", !all("pastIsland").length, count(all("pastIsland"), "none"), all("pastIsland"));
rule("a bar's island draws its bar or its rows, never its heading alone", !all("bareIslands").length, `${sum("barsDrawn")} bars, ` + count(all("bareIslands"), "none bare"), all("bareIslands"));
const ofRule = (k) => overlaps.filter(o => o.rule === k);
rule("a bar's part that filters and its legend item are one target, chosen in both", !ofRule("pick").length && (sum("pickPages") > 0 || !whole), `${sum("pickPages")} pages drew one, ` + count(ofRule("pick"), "all one target"), ofRule("pick"));
rule("every bar's parts and list rows pair up for the hover", !ofRule("pair").length, count(ofRule("pair"), "all paired"), ofRule("pair"));
rule("one legend grid per page", !ofRule("grid").length, count(ofRule("grid"), "one each"), ofRule("grid"));
rule("the key line fits its room", !ofRule("keys").length, count(ofRule("keys"), "fits everywhere"), ofRule("keys"));
rule("the Fireside's counts, shields, names and hearth never touch", !ofRule("fire").length, count(ofRule("fire"), "none touch"), ofRule("fire"));
rule("no two texts of one row drawn over each other: a hero or stat row (a fight's head, a page's hero, the away totals), else one block", !all("rowOver").length, `${sum("rowTexts")} texts in rows, ` + count(all("rowOver"), "none over another"), all("rowOver"));
rule("no text of two blocks drawn over each other", !ofRule("blocks").length, count(ofRule("blocks"), "none"), ofRule("blocks"));
rule("the Fireside's arrows: every thread of the game's plan drawn, each tip on its receiver's rim (2 px), the head along its line's end", !all("fireHeadsOff").length && (sum("fireHeads") > 0 || !whole),
     `${sum("fireDrawn")} drawings, ${sum("fireHeads")} heads, ` + count(all("fireHeadsOff"), "every one on its rim"), all("fireHeadsOff"));
rule("the Fireside's counts: none on a crossing of two threads", !all("fireCrossed").length, count(all("fireCrossed"), "none"), all("fireCrossed"));
rule("the Fireside's list: no text of a row over another (\"feas× 5\")", !all("giftRowHits").length && (sum("giftRows") > 0 || !whole), `${sum("giftRows")} rows, ` + count(all("giftRowHits"), "no text over text"), all("giftRowHits"));
const arrowsCut = (arrowWidths || []).filter(([, w]) => !(w <= arrowBox));
rule(`the Foes table's Best arrow column holds the longest arrow names (${arrowBox} px)`, !arrowsCut.length && (!!arrowWidths || !whole), arrowWidths ? arrowWidths.map(([t, w]) => `${t} ${w}`).join(", ") + (arrowsCut.length ? `; ${arrowsCut.length} cut` : ", all whole") : "no foe table drawn");
const namesNarrow = (foeNameWidths || []).filter(([, w]) => !(w <= nameBox));
rule(`the Foes table's name box holds the longest foe names (${nameBox} px) and every drawn name`, !namesNarrow.length && !all("foeNamesCut").length && (!!foeNameWidths || !whole),
     (foeNameWidths ? foeNameWidths.map(([t, w]) => `${t} ${w}`).join(", ") + (namesNarrow.length ? `; ${namesNarrow.length} cut` : ", all whole") : "no foe table drawn") + "; drawn names cut: " + count(all("foeNamesCut"), "none"), all("foeNamesCut"));
const dealtNarrow = (dealtWidths || []).filter(([, w]) => !(w <= dealtBox));
rule(`the Foes table's Dealt column holds a 6-digit number beside a full bar (${dealtBox} px) and every drawn number`, !dealtNarrow.length && !all("dealtCut").length && (!!dealtWidths || !whole),
     (dealtWidths ? dealtWidths.map(([t, w]) => `${t} ${w}`).join(", ") + (dealtNarrow.length ? `; ${dealtNarrow.length} cut` : ", all whole") : "no foe table drawn") + `; ${sum("dealtDrawn")} drawn, ` + count(all("dealtCut"), "all inside their cell"), all("dealtCut"));
rule("every damage bar part shows its name and number on hover, at no element per part (one tag a page, one label an entry)", !all("hoverless").length && !all("hoverHeavy").length && (sum("damageParts") > 0 || !whole),
     `${sum("damageParts")} parts on ${sum("hoverPages")} pages, ` + (all("hoverless").length ? all("hoverless").length + " without" : "every one shows them") + "; " + (all("hoverHeavy").length ? all("hoverHeavy").length + " pages heavier" : "one tag a page, one label an entry"), all("hoverless").concat(all("hoverHeavy")));
rule("a hover tag never covers the bar it describes: beside it, below or (at the plate's or island's edge) above", !all("hoverCovers").length && (sum("damageParts") > 0 || !whole),
     `${sum("damageParts")} parts checked, ` + count(all("hoverCovers"), "no tag over its bar"), all("hoverCovers"));
rule(`the UI source asks for no text under the ${MIN} px floor`, !uiSmall.length, count(uiSmall, "none"), uiSmall);
rule("the model source uses no per-process string hash (the previews are the same every run)", !hashed.length, count(hashed, "none"), hashed);
rule("US spelling in the visible text (armor, defense, color, gray, meter)", !all("british").length, count(all("british"), "no British form"), all("british").map(b => ({ page: b.page, what: `"${b.what}"` })));
info("page errors in the preview", count(pageErrors, "none") + (pageErrors.length ? ": " + pageErrors.slice(0, 5).map(e => `${e.page}: ${e.what}`).join("; ") : ""));
if (val("--json")) {
  const byChapter = {}; names.forEach((n, i) => { const c = chapter(n), p = results[i].perPage; (byChapter[c] ??= { pages: 0, runs: 0, bad: 0 }); byChapter[c].pages++; byChapter[c].runs += p.runs; byChapter[c].bad += p.bad; });
  writeFileSync(val("--json"), JSON.stringify({ min: MIN, byChapter, violations: rows, truncated: cuts, failed }, null, 1));
}
if (failed.length && !noFail) { console.log(`FAIL: ${failed.length} rules fail on ${names.length} previews: ${failed.join("; ")}`); process.exit(1); }
console.log(`PASS: every rule holds on ${names.length} previews (no text under the ${MIN} px floor)`);
