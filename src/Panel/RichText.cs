using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace Hearthwoven.Panel
{
    /// <summary>
    /// Text with TextMeshPro rich-text tags, made only here (richtext-fix, the 0.6.1 bug: Feats cards and the detail area showed
    /// "&lt;b&gt;&lt;color=#C27A4A&gt;I&lt;/color&gt;&lt;/…" as letters, because PanelUi.Label turns rich text off and three Feats labels got tags all the same,
    /// and TMP's ellipsis then cut the tags as if they were letters).
    /// The model never writes markup: it hands over parts (a title, a numeral, a colour, a note) and the UI styles them with this type.
    /// A tag can only be written here, model text always goes in literal (<see cref="Plain"/>: a '&lt;' in it is kept as a letter), and a
    /// Rich value reaches a label only through PanelUi.RichLabel, SetRich or BtRich, which turn rich text on for the block kinds in
    /// <see cref="PanelRich.Kinds"/>. TMP's own ellipsis then cuts visible characters and never a tag; nothing substrings a marked-up string.
    /// Pure C# (no Unity), so the tests and the HTML bridge's map read the same code.
    /// </summary>
    public readonly struct Rich
    {
        /// <summary>The TMP string the label is given.</summary>
        public readonly string Markup;
        Rich(string markup) { Markup = markup ?? ""; }

        public static readonly Rich Empty = new Rich("");
        public bool IsEmpty => string.IsNullOrEmpty(Markup);

        /// <summary>Text from the model, kept literal: unchanged when it holds no '&lt;', else inside &lt;noparse&gt; (a player's name like "&lt;3" stays a name).</summary>
        public static Rich Plain(string text) =>
            string.IsNullOrEmpty(text) ? Empty : text.IndexOf('<') < 0 ? new Rich(text) : new Rich("<noparse>" + text.Replace("</noparse>", "") + "</noparse>");

        public Rich Bold() => new Rich("<b>" + Markup + "</b>");
        public Rich Italic() => new Rich("<i>" + Markup + "</i>");
        /// <summary>In a colour ("#rrggbb" or "#rrggbbaa"); anything else leaves the colour as it is.</summary>
        public Rich Ink(string hex)
        {
            if (string.IsNullOrEmpty(hex)) return this;
            var h = hex[0] == '#' ? hex : "#" + hex;
            return HexColour.IsMatch(h) ? new Rich("<color=" + h + ">" + Markup + "</color>") : this;
        }
        public Rich Sized(float px) => new Rich("<size=" + px.ToString(CultureInfo.InvariantCulture) + ">" + Markup + "</size>");

        public static Rich operator +(Rich a, Rich b) => new Rich(a.Markup + b.Markup);
        public static Rich operator +(Rich a, string plain) => a + Plain(plain);
        public static Rich operator +(string plain, Rich b) => Plain(plain) + b;

        /// <summary>What a label with rich text on shows: the tags gone, a noparse part as written (for the tests and the measuring of widths).</summary>
        public string Shown
        {
            get
            {
                var s = Markup; var sb = new StringBuilder(s.Length);
                for (int i = 0; i < s.Length;)
                {
                    if (string.CompareOrdinal(s, i, "<noparse>", 0, 9) == 0)
                    {
                        var end = s.IndexOf("</noparse>", i + 9, System.StringComparison.Ordinal); if (end < 0) end = s.Length;
                        sb.Append(s, i + 9, end - i - 9); i = System.Math.Min(s.Length, end + 10); continue;
                    }
                    if (s[i] == '<') { var close = s.IndexOf('>', i); if (close > i) { i = close + 1; continue; } }
                    sb.Append(s[i]); i++;
                }
                return sb.ToString();
            }
        }

        static readonly Regex HexColour = new Regex("^#(?:[0-9A-Fa-f]{6}|[0-9A-Fa-f]{8})$");
    }

    /// <summary>
    /// The one switch for rich text in the panel (richtext-fix). <see cref="Kinds"/>: the block kinds whose UI draws a label made of
    /// <see cref="Rich"/> parts, and so has rich text on there (PanelUi.RichLabel, SetRich, BtRich read <see cref="On"/>). The HTML bridge
    /// gets the same list in preview-data.js (window.PANEL_RICH) and draws the raw tags, as the game would, for a kind not in it.
    /// <see cref="HasMarkup"/>: does a text hold a TMP tag? The test over every model string and the in-game self-check use it.
    /// </summary>
    public static class PanelRich
    {
        public static readonly string[] Kinds =
        {
            "feats",       // a card's name with its tier numeral in the tier's colour (FeatsUi.FeatCard)
            "featdetail",  // the moment, "Earned 8 Oct" and a quiet italic "(or earlier)" (FeatsUi.FeatDetailFill)
            "featband",    // the band's feat names with their tier (FeatsUi.FeatBandBlock)
            "filterbar",   // a chip's name and its live count, a token with its × (FacetUi)
            "cropgrid",    // "72 planted" beside the paired bars (CropsUi)
            "itemgrid",    // a layered second line, the quiet "fuel" after a name (DeedsUi)
            "strip",       // a layered number (DeedsUi.Strip)
            "hero",        // a layered hero number with its "before install" and "since install" (PanelUi.HeroNumber)
            "feed",        // a battle feed entry's damage both ways, each type in its colour, the amounts bold (BattleFeedUi.FeedLine, 0.8)
        };
        static readonly HashSet<string> kinds = new HashSet<string>(Kinds);

        /// <summary>Rich text on for a label of this block kind?</summary>
        public static bool On(string kind) => kind != null && kinds.Contains(kind);

        // '<' then a TMP tag name (or a "<#rgb>" colour): what a label shows as letters when its tags were not parsed
        static readonly Regex Tag = new Regex(@"</?(?:b|i|u|s|color|colour|size|alpha|mark|noparse|sprite|font|material|sup|sub|link|align|cspace|indent|line-height|line-indent|margin|mspace|nobr|page|pos|rotate|smallcaps|space|style|voffset|width|lowercase|uppercase|allcaps|br|gradient)(?=[\s=>/]|$)|<#[0-9A-Fa-f]{3,8}>",
                                              RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        /// <summary>True when the text holds a TMP tag ("&lt;b&gt;", "&lt;color=#C27A4A&gt;", "&lt;/i&gt;"); "&lt;3", "a &lt; b" or "&lt;Boar&gt;" are not tags.</summary>
        public static bool HasMarkup(string text) => !string.IsNullOrEmpty(text) && Tag.IsMatch(text);

        /// <summary>The first tag in the text, for a report line ("" when none).</summary>
        public static string FirstTag(string text) { var m = string.IsNullOrEmpty(text) ? null : Tag.Match(text); return m != null && m.Success ? m.Value : ""; }
    }
}
