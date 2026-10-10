using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Hearthwoven.Panel
{
    /// <summary>
    /// The Everyone chip (0.8, EveryoneModel.cs; work/hearthwoven-0.8/prototypes/PICKS.md section 1, chip C and block G2), drawn:
    ///   the chip, the last entry of the player row after a thin gold rule: the kit's row, its icon a tiny bar in the player colours (an icon,
    ///     not data); on, the kit's chosen row with a gold glow, and every name gets a short line in its own colour (the row is the colour key);
    ///     greyed, dimmed with a second line saying where it works ("on Damage, Defence") and, on hover, a tag under the row saying what a
    ///     press does, drawn over the chapter tabs on a canvas of its own, so nothing moves; a press opens that page with Everyone on;
    ///   the "Everyone" tag before the page's title;
    ///   the group's rows (grouprows): per player the shield, the name and when their numbers are from (too long for both: "8 Oct 00:27"
    ///     without "as of", then the name gives way, never the date),
    ///     a bar in their colour at their share of the whole over a faint track, the number, the share in gold; words instead of a bar where
    ///     there is nothing or no number.
    /// Laid out once per fill; nothing per frame. Mirrored in preview/panel-preview.html (everyoneChip, groupRows).
    /// </summary>
    public partial class PanelUi
    {
        // PICKS G2: grid 190 / 1fr / 72 / 70, gap 16; 0.8 layout D+: rows 32 px with the name and when on one line (name 18 bold, note 14 italic),
        // no rules (the island sets the group apart), so a group page fits one screen; shield 30; track 6 px, bar 22 px
        const float GroupNameW = 190, GroupNumW = 72, GroupPctW = 70, GroupColGap = 16, GroupRowH = 32, GroupShield = 30, GroupTrackH = 6;
        const float EveryoneIconW = 38, EveryoneIconH = 12, EveryoneTagW = 380;
        static readonly float[] EveryoneIconParts = { 5, 2, 1, 2 };   // the mini bar's four parts (palette slots 0-3): an icon, not data

        RectTransform everyoneHead;

        static void GroupRows(RectTransform col, Block b)
        {
            var rows = b.Items ?? new List<Block>();
            if (rows.Count == 0) return;
            if (b.Tone == PanelModel.DamagePlayersDealt || b.Tone == PanelModel.DamagePlayersReceived) { DamagePlayers(col, b); return; }   // 0.8: Battle's rows by player (DamageRowsUi.cs)
            // the share column takes the widest share it holds, so nothing overlaps the number
            var pctW = GroupPctW;
            var wide = rows.Where(r => r.Kind == PanelModel.GroupMemberKind && !string.IsNullOrEmpty(r.Value2) && r.Value2.Length > 5).Select(r => r.Value2).ToList();
            if (wide.Count > 0)
            {
                var probe = Label(col, "", 22, PanelLook.Gold, style: FontStyles.Bold); probe.textWrappingMode = TextWrappingModes.NoWrap;
                foreach (var w in wide) pctW = Mathf.Max(pctW, Mathf.Ceil(probe.GetPreferredValues(w).x) + 4);
                UnityEngine.Object.DestroyImmediate(probe.gameObject);
            }
            float barX = GroupNameW + GroupColGap, pctX = Column - pctW, numX = pctX - GroupColGap - GroupNumW, barW = Mathf.Max(40f, numX - GroupColGap - barX);
            if (!string.IsNullOrEmpty(b.Title) || !string.IsNullOrEmpty(b.Text))
            {
                var cap = Node("Caption", col); Size(cap, Column, 20);
                if (!string.IsNullOrEmpty(b.Title) || !string.IsNullOrEmpty(b.Value)) { var head = Line(cap, 8); head.Box(barX, 0, numX + GroupNumW - barX, 20); Sect(head, b.Title, b.Value); }   // the island heading style, with its total
                if (!string.IsNullOrEmpty(b.Text)) { var s = Label(cap, b.Text, 14, PanelLook.Muted, style: FontStyles.UpperCase, align: TextAlignmentOptions.MidlineRight); s.characterSpacing = 14; OneLine(s).rectTransform.Box(pctX - 20, 0, pctW + 20, 20); }
            }
            var track = new Color(PanelLook.Edge.r, PanelLook.Edge.g, PanelLook.Edge.b, 0.22f);
            foreach (var r in rows)
            {
                var box = Node("Player", col); Size(box, Column, GroupRowH);
                Marker(box, r.Icon, GroupShield, layout: false).Box(0, (GroupRowH - GroupShield) / 2, GroupShield, GroupShield);
                // the name and when, on one line: too long for both, "as of 8 Oct 00:27" says "8 Oct 00:27" (PanelModel.GroupNoteFit); still too long, the name
                // gives way, never the date (the shield and colour still say who it is; 0.8.1 review 1)
                var name = OneLine(Label(box, r.Title, 18, PanelLook.Text, style: FontStyles.Bold, align: TextAlignmentOptions.MidlineLeft));
                var note = string.IsNullOrEmpty(r.Note) ? null : OneLine(Label(box, r.Note, 14, PanelLook.Faint, style: FontStyles.Italic, align: TextAlignmentOptions.MidlineLeft));
                float room = barX - GroupColGap / 2 - 48, nameNeed = Mathf.Ceil(name.preferredWidth) + 2, noteW = 0f;   // the name, 8 px, the note
                if (note)
                {
                    note.text = PanelModel.GroupNoteFit(r.Note, nameNeed, t => Mathf.Ceil(note.GetPreferredValues(t).x) + 2, room);
                    noteW = Mathf.Min(room, Mathf.Ceil(note.GetPreferredValues(note.text).x) + 2);
                }
                var nameW = Mathf.Max(0f, Mathf.Min(nameNeed, room - noteW)); name.rectTransform.Box(40, (GroupRowH - 26) / 2, nameW, 26);
                if (note) note.rectTransform.Box(40 + nameW + 8, (GroupRowH - 20) / 2 + 1, room - nameW, 20);
                if (r.Kind == PanelModel.GroupMemberKind)
                {
                    Img(box, "Track", null, track).rectTransform.Box(barX, (GroupRowH - GroupTrackH) / 2, barW, GroupTrackH);
                    RoundedPart(box, "Bar", Hex(r.Colour, PanelLook.Accent)).rectTransform.Box(barX, (GroupRowH - PanelModel.BarFormBar) / 2, Mathf.Max(6f, barW * Mathf.Clamp01(r.Fraction)), PanelModel.BarFormBar);
                    OneLine(Label(box, r.Value, 18, PanelLook.Text, style: FontStyles.Bold, align: TextAlignmentOptions.MidlineRight)).rectTransform.Box(numX, (GroupRowH - 28) / 2, GroupNumW, 28);
                    OneLine(Label(box, r.Value2, 22, PanelLook.Gold, style: FontStyles.Bold, align: TextAlignmentOptions.MidlineRight)).rectTransform.Box(pctX, (GroupRowH - 30) / 2, pctW, 30);
                }
                else OneLine(Label(box, r.Text, 16, PanelLook.Muted, style: FontStyles.Italic, align: TextAlignmentOptions.MidlineLeft)).rectTransform.Box(barX, (GroupRowH - 24) / 2, Column - barX, 24);
            }
        }

        // a name's colour key while the group is shown: a 3 px line in the player's colour at the entry's foot (PICKS: left 14, right 18, bottom 6)
        static void NameKey(GameObject entry, string name)
        {
            if (!entry || string.IsNullOrEmpty(name)) return;
            var line = Img(entry.transform, "Key", null, PersonTint(name)).rectTransform;
            line.anchorMin = Vector2.zero; line.anchorMax = new Vector2(1, 0); line.pivot = new Vector2(0.5f, 0);
            line.offsetMin = new Vector2(14, 6); line.offsetMax = new Vector2(-18, 9);
        }

        // the chip at the row's end, after a 1 px gold rule; v.EveryoneWhy set: greyed, where it works on its second line, what a press does on hover
        void EveryoneChip(PanelView v)
        {
            var rule = Node("EveryoneRule", players); var rl = rule.gameObject.AddComponent<LayoutElement>(); rl.minWidth = rl.preferredWidth = 1; rl.minHeight = rl.preferredHeight = 30;
            Img(rule, "Line", null, new Color(PanelLook.Gold.r, PanelLook.Gold.g, PanelLook.Gold.b, 0.35f)).rectTransform.Stretch();
            var on = v.EveryoneOn; var off = !string.IsNullOrEmpty(v.EveryoneWhy) && !on;
            var img = Kit(players, PanelModel.EveryoneLabel, on ? "row-selected" : "row", raycast: true);
            var le = img.gameObject.AddComponent<LayoutElement>(); le.minHeight = le.preferredHeight = 48;
            var icon = Node("Icon", img.transform); icon.anchorMin = icon.anchorMax = icon.pivot = new Vector2(0, 0.5f); icon.anchoredPosition = new Vector2(14, 0); icon.sizeDelta = new Vector2(EveryoneIconW, EveryoneIconH);
            float x = 0, unit = (EveryoneIconW - 2 * (EveryoneIconParts.Length - 1)) / EveryoneIconParts.Sum();
            for (int k = 0; k < EveryoneIconParts.Length; k++)
            {
                var p = RoundedPart(icon, "Part", PanelLook.PersonColor(k)).rectTransform;
                p.anchorMin = p.anchorMax = p.pivot = new Vector2(0, 0.5f); p.anchoredPosition = new Vector2(x, 0); p.sizeDelta = new Vector2(EveryoneIconParts[k] * unit, EveryoneIconH);
                x += EveryoneIconParts[k] * unit + 2;
            }
            const float textX = 14 + EveryoneIconW + 10;
            var label = Label(img.transform, PanelModel.EveryoneLabel, off ? 16 : 18, on ? PanelLook.Gold : PanelLook.Text, align: TextAlignmentOptions.MidlineLeft);
            label.textWrappingMode = TextWrappingModes.NoWrap; label.rectTransform.Stretch();
            label.rectTransform.offsetMin = new Vector2(textX, off ? 48 / 2 - 4 : 0); label.rectTransform.offsetMax = new Vector2(-16, off ? -3 : 0);
            TextMeshProUGUI sub = null;
            if (off && !string.IsNullOrEmpty(v.EveryoneSub))   // the "just joined" pattern: the name in the top half, the line in the bottom
            {
                sub = Label(img.transform, v.EveryoneSub, PanelLook.MinText, PanelLook.Muted, align: TextAlignmentOptions.MidlineLeft);
                sub.textWrappingMode = TextWrappingModes.NoWrap; sub.rectTransform.Stretch();
                sub.rectTransform.offsetMin = new Vector2(textX, 4); sub.rectTransform.offsetMax = new Vector2(-16, -(48 / 2 - 2));
            }
            // never squeezed: the chip keeps its whole width, so it stays where it is (the names give way first)
            le.minWidth = le.preferredWidth = Mathf.Ceil(Mathf.Max(label.preferredWidth, sub != null ? sub.preferredWidth : 0f)) + textX + 16 + 4;
            if (on) Glow(img.gameObject, PanelLook.Gold);
            if (!off) { Clickable(img, () => { state.Everyone = !on; state.Player = ""; Render(true); }); return; }
            img.gameObject.AddComponent<CanvasGroup>().alpha = 0.55f;
            var tag = EveryoneWhyTag(img.rectTransform, v.EveryoneWhy);
            img.gameObject.AddComponent<SwapOnHover>().On = tag;
            // a press opens the page its tag names, Everyone on (EveryoneJump); nobody else sharing yet: the press shows the reason (no hover on a pad)
            Clickable(img, () => { if (PanelModel.EveryoneJump(state, v)) Render(true); else tag.SetActive(!tag.activeSelf); });
        }

        // the reason, in a tag under the chip on a canvas of its own (drawn over the chapter tabs, no layout change), at full strength under the dimmed chip
        static GameObject EveryoneWhyTag(RectTransform chip, string why)
        {
            var tag = Node("Why", chip);
            tag.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
            tag.anchorMin = tag.anchorMax = new Vector2(1, 0); tag.pivot = new Vector2(1, 1); tag.anchoredPosition = new Vector2(0, -6);
            var canvas = tag.gameObject.AddComponent<Canvas>(); canvas.overrideSorting = true;
            var root = chip.GetComponentInParent<Canvas>();   // over the book, whatever its order (as CompareUi.ReasonTag)
            if (root) { canvas.sortingLayerID = root.rootCanvas.sortingLayerID; canvas.sortingOrder = root.rootCanvas.sortingOrder + 10; }
            var group = tag.gameObject.AddComponent<CanvasGroup>(); group.ignoreParentGroups = true; group.blocksRaycasts = false; group.interactable = false;
            Img(tag, "Ground", null, new Color(0.03f, 0.025f, 0.02f, 0.96f)).rectTransform.Stretch();
            Edge(tag, new Color(PanelLook.Gold.r, PanelLook.Gold.g, PanelLook.Gold.b, 0.5f));
            var t = Label(tag, why, 15, PanelLook.Text, align: TextAlignmentOptions.TopLeft);
            t.rectTransform.Stretch(); t.rectTransform.offsetMin = new Vector2(12, 8); t.rectTransform.offsetMax = new Vector2(-12, -8);
            var w = Mathf.Min(EveryoneTagW, Mathf.Ceil(t.GetPreferredValues(why).x) + 24 + 2);   // one short sentence: as wide as it is, up to EveryoneTagW
            var h = Mathf.Ceil(t.GetPreferredValues(why, w - 24, 0).y) + 16;
            tag.sizeDelta = new Vector2(w, h);
            tag.gameObject.SetActive(false);
            return tag.gameObject;
        }

        // "Everyone" before the page's title (as the book chip of a fellow's book): a 1 px gold edge, gold at 16 %, 15 px bold; returns its width
        float EveryoneTag(RectTransform row, float x)
        {
            var bg = Img(row, PanelModel.EveryoneLabel, null, new Color(PanelLook.Gold.r, PanelLook.Gold.g, PanelLook.Gold.b, 0.16f));
            Edge(bg.rectTransform, PanelLook.Gold);
            var t = Label(bg.transform, PanelModel.EveryoneLabel, 15, PanelLook.Gold, style: FontStyles.Bold, align: TextAlignmentOptions.Center);
            t.textWrappingMode = TextWrappingModes.NoWrap; t.rectTransform.Stretch();
            var w = Mathf.Ceil(t.preferredWidth) + 20;
            bg.rectTransform.Box(x, 5, w, 26);
            everyoneHead = bg.rectTransform;
            return w;
        }
    }
}
