// Zones (design B, src/Panel/ZonesModel.cs + ZonesUi.cs) for the static preview: the stone zone (your character's counts)
// and the ember zone (this PC's), as PanelUi draws them: the kit's meter fill tinted as the ground, the meter track as the
// edge, a thin amber rim on top of the ember zone, the source sprite as the glyph. panel-preview.html calls zone() and
// bosses() from block(); everything else inside a zone is drawn by block() itself.
(function () {
  const css = `
  .zone { position:relative; border-radius:5px; padding:10px 16px 10px; display:flex; flex-direction:column; gap:8px; }
  .zone.stone { background:#33373c; box-shadow:inset 0 0 0 1px #9ea8b8, inset 0 0 0 2px rgba(0,0,0,.55); }
  .zone.server { background:#132126; box-shadow:inset 0 0 0 1px #548594, inset 0 0 0 2px rgba(0,0,0,.55); }
  .zone.ember { background:#261709; box-shadow:inset 0 0 0 1px #9e6129, inset 0 0 0 2px rgba(0,0,0,.55); padding-top:13px; }
  .zone.ember::before { content:""; position:absolute; left:3px; right:3px; top:1px; height:3px; background:#e8a948; border-radius:2px; }
  .zone.tight { padding:6px 16px 6px; gap:2px; } .zone.ember.tight { padding-top:9px; } .zone.tight .zh { height:20px; } .plate .zone.tight .section { margin-top:3px; }
  .zone.ember.dim { box-shadow:inset 0 0 0 1px #5c3a1c, inset 0 0 0 2px rgba(0,0,0,.55); } .zone.ember.dim::before { background:#5c3a1c; }
  .zh { display:flex; align-items:center; gap:8px; height:22px; white-space:nowrap; }
  .zh img { width:20px; height:20px; flex:none; } .zone.dim .zh img { filter:grayscale(1); opacity:.45; }
  .zt { font-size:15px; letter-spacing:.14em; text-transform:uppercase; font-weight:700; }
  .zr { margin-left:auto; font-size:15px; }
  .zone.stone .zt { color:#d6d0bf; } .zone.stone .zr { color:#a7a18e; }
  .zone.server .zt { color:#9ed1e0; } .zone.server .zr { color:#85adba; }
  .zone.ember .zt { color:#f0bd62; } .zone.ember .zr { color:#b58a57; }
  .zl { font-size:15px; color:#e4c99f; }
  .zbosses { display:flex; gap:34px; align-items:center; height:34px; } .zbl { font-size:15px; color:#a7a18e; margin-right:-14px; }
  .zboss { display:flex; align-items:center; gap:12px; font-size:20px; }
  .zring { position:relative; width:34px; height:34px; flex:none; } .zring > * { position:absolute; inset:0; }
  .zring .m { left:2px; top:2px; }
  .zring > img { width:34px; height:34px; }
  .zone .emptytext { margin-top:-6px; }
  .zone.stone .hn .num { color:#efe6d0; text-shadow:0 3px 0 #0b0c0d; } .zone.stone .hn .what { color:#efe6d0; }`;
  const st = document.createElement("style"); st.textContent = css; document.head.appendChild(st);

  window.zone = function (b) {
    const stone = b.id === "character", server = b.id === "server", empty = b.tone === "empty";
    const was = COLUMN; COLUMN = was - 32;
    const inner = b.items.map(x => x.kind === "hero" && x.tone === "second" ? `<div class="hero">${heroNum(x, true)}</div>` : block(x)).join("");
    COLUMN = was;
    return `<section class="zone ${stone ? "stone" : server ? "server" : "ember"}${empty ? " dim" : ""}${b.tone === "tight" ? " tight" : ""}">
      <div class="zh"><img src="../vocab/${stone ? "src-stone" : server ? "src-fellows" : "src-hearth"}.png" alt=""><span class="zt">${esc(b.title)}</span><span class="zr">${esc(b.text)}</span></div>
      ${b.note ? `<div class="zl">${esc(b.note)}</div>` : ""}${inner}</section>`;
  };

  window.bosses = function (b) {
    return `<div class="zbosses">${b.title ? `<span class="zbl">${esc(b.title)}</span>` : ""}${b.items.map(x => `<span class="zboss"><span class="zring">${marker(x.icon, 30)}<img src="../vocab/boss-ring.png" alt=""></span><span>${esc(x.title)}</span></span>`).join("")}</div>`;
  };
})();
