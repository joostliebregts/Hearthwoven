using System;
using System.Globalization;
using UnityEngine;

namespace Hearthwoven.Panel
{
    /// <summary>
    /// 0.8.5 the mirror's translucent colours as the game draws them. Unity draws the book in linear colour space: a translucent colour and what
    /// lies under it are turned linear, mixed there and turned back. A browser, and so the mirror and its preview pictures, mixes the sRGB values
    /// as they are. A light wash at a low alpha so comes out much lighter in game, a dark veil over a light part much fainter: the damage rows'
    /// track, rgba(255,240,210,.05) on an island, is (32, 29, 25) in the mirror and was (67, 63, 54) in game (0.8.3, Joost: the loud band under
    /// every bar; the island's own ground had the same fix by hand in 0.8.2). Every wash is written here once, as the mirror writes it (Css, in
    /// its Twin rule or line of preview/panel-preview.html), over the ground it sits on there; Game is the colour that draws in game what the
    /// mirror draws on that ground. The mirror stays the look; only the game's numbers follow it. A colour that changes per row (a player's,
    /// a part's) takes Toward when it is drawn. Plain C# over Unity's Color struct, no engine calls, so test-panel/WashTests.cs reads it offline.
    /// </summary>
    public static class PanelWash
    {
        /// <summary>One wash: the mirror's colour, the ground it sits on, the colour the game takes (Toward).</summary>
        public sealed class Wash
        {
            public readonly string Twin, Css; public readonly Color Mirror, Ground, Game;
            public Wash(string twin, string css, Color ground) { Twin = twin; Css = css; Mirror = Rgba(css); Ground = ground; Game = Toward(Mirror, ground); }
            /// <summary>What the mirror shows: the wash over its ground, mixed in sRGB.</summary>
            public Color Look => Over(Mirror, Ground);
        }

        // the grounds as the mirror draws them: the page (the plate, rgba(15,11,8,.95) over the frame), an island on it (IslandGround), the frame
        // beside the page's head and behind the players' bar (its texture, measured in the previews), a menu or tag (rgba(9,7,5,.98)), the
        // strip at the page's head (black at 55 % over the page)
        public static readonly Color Page = Rgb(10, 9, 7);
        /// <summary>PanelLook.IslandGround (0.8.2 by hand, shade 4 that Joost picked in game; the same look).</summary>
        public static readonly Wash IslandGround = new Wash(".isl {", "rgba(255,240,210,.04)", Page);
        public static readonly Color Island = IslandGround.Look, Head = Rgb(23, 17, 15), Bar = Rgb(40, 31, 22), Menu = Rgb(9, 7, 5), Strip = Rgb(4, 4, 3);

        // ----- grounds and lines on the page -----
        /// <summary>The damage rows' track (DamageRowsUi.DrTrack), Fight's lanes and runs (.ft-track).</summary>
        public static readonly Wash Track = new Wash(".dr-track {", "rgba(255,240,210,.05)", Island);
        /// <summary>One item's tint (BattleUi.BtBand): the open foe, the feed's cards, the open fight (.bt-open, .bt-card, .ft-band).</summary>
        public static readonly Wash Band = new Wash(".bt-band {", "rgba(242,232,214,.045)", Island);
        /// <summary>A row that opens, pointed at (BattleUi.BtRowHover; .ft-row.opens:hover).</summary>
        public static readonly Wash RowHover = new Wash(".bt-foerow.opens:hover {", "rgba(242,232,214,.035)", Island);
        /// <summary>The line under a Battle row (BattleUi.BtLine; .ft-c, .bt-fl).</summary>
        public static readonly Wash Line = new Wash(".bt-row {", "rgba(107,82,51,.28)", Island);
        public static readonly Wash BattleTrack = new Wash(".bt-track {", "rgba(107,82,51,.45)", Island);
        /// <summary>PanelLook.Rule, --rule: a divider on the page (in an island it shows 3 levels darker than the mirror's).</summary>
        public static readonly Wash Rule = new Wash(":root {", "rgba(107,82,51,.6)", Page);
        /// <summary>Fight's tree, from a fight to the fights it holds (FightUi.FtTree).</summary>
        public static readonly Wash Tree = new Wash(".ft-tree {", "rgba(107,82,51,.6)", Island);
        /// <summary>The group bars' track on Everyone (EveryoneUi).</summary>
        public static readonly Wash GroupTrack = new Wash(".gr-track {", "rgba(107,82,50,.22)", Island);
        /// <summary>A damage row's true-length line (DamageRowsUi): gold for damage dealt, quiet for damage received.</summary>
        public static readonly Wash TrueDealt = new Wash(".dr-true {", "rgba(232,194,122,.8)", Island), TrueReceived = new Wash(".dr-true.recv {", "rgba(185,166,136,.8)", Island);
        /// <summary>About's Since when (AboutUi.SinceWhen): its rules, the install day's dashes.</summary>
        public static readonly Wash SinceRule = new Wash("out += bar(x0, foot", "rgba(107,82,51,.3)", Island), InstallMark = new Wash("out += bar(xi, y, 1, 3", "rgba(232,169,72,.6)", Island);
        /// <summary>A skill ladder's reach (PanelUi.LadderPicture).</summary>
        public static readonly Wash Reach = new Wash("<svg class=\"ld\"", "rgba(232,169,72,.36)", Island);
        /// <summary>A growth line's columns and its zero mark (GrowthLinesUi).</summary>
        public static readonly Wash SparkBar = new Wash(".spark .cols i {", "rgba(232,194,122,.85)", Island), SparkMark = new Wash(".spark .cols i.z {", "rgba(232,194,122,.35)", Island);
        /// <summary>Battle's Fell tile when no place is shared (BattleUi.BtNoPlaceEdge), its dashed edge.</summary>
        public static readonly Wash NoPlaceEdge = new Wash("class=\"bt-none\"", "rgba(176,160,134,.8)", Island);
        /// <summary>A feat card's notch not reached yet, and a feat Hearthwoven does not count yet: its dashed rim (FeatsUi).</summary>
        public static readonly Wash NotchNext = new Wash(".fcard .nt.nx {", "rgba(158,140,102,.9)", Page), WaitingRim = new Wash(".fcard.waiting, .fdet.waiting {", "rgba(185,166,136,.7)", Page);
        /// <summary>A feat's tier colour when it has none of its own (FeatsUi.FeatRim; the mirror's var(--tier) fallback).</summary>
        public static readonly Wash TierRim = new Wash(".kfor .kfc {", "rgba(232,194,122,.85)", Page);

        // ----- what you point at and what you chose -----
        /// <summary>A lit legend entry's band (HoverUi.Band).</summary>
        public static readonly Wash HoverBand = new Wash(".dr-e.hov {", "rgba(232,194,122,.15)", Island);
        /// <summary>The 1 px line round anything pointed at (HoverMark).</summary>
        public static readonly Wash HoverLine = new Wash(":hover { outline", "rgba(232,194,122,.45)", Island);
        /// <summary>A chosen filter chip and a chosen token (FacetUi.FacetOn).</summary>
        public static readonly Wash ChipOn = new Wash(".fb-chip.on {", "rgba(232,169,72,.2)", Page);
        /// <summary>Compare's menu: the chosen option's ground and edge (CompareUi).</summary>
        public static readonly Wash MenuChosen = new Wash(".cmenu .cm-opt.sel {", "rgba(232,194,122,.16)", Menu), MenuChosenEdge = new Wash(".cmenu .cm-opt.sel {", "rgba(232,194,122,.55)", Menu);

        // ----- marks -----
        /// <summary>A boss's stretch on a fight's run (FightUi.FtBossBand, FtBossEdge), over the run's track.</summary>
        public static readonly Wash BossBand = new Wash(".ft-boss {", "rgba(232,194,122,.16)", Track.Look), BossEdge = new Wash(".ft-boss {", "rgba(232,194,122,.55)", Track.Look);
        /// <summary>"Everyone" before the page's title, and the rule between the players and Everyone in the players' bar (EveryoneUi).</summary>
        public static readonly Wash EveryoneTag = new Wash(".ev-tag {", "rgba(232,194,122,.16)", Head), EveryoneRule = new Wash(".ev-rule {", "rgba(232,194,122,.35)", Bar);
        /// <summary>The gold edge of a dark tag: why Everyone is off (EveryoneUi), the Feats grid's More below cue (.fcue span).</summary>
        public static readonly Wash TagEdge = new Wash(".ev-why {", "rgba(232,194,122,.5)", Menu);
        /// <summary>A key's cap in the frame (PanelUi.KeyCap), its edge.</summary>
        public static readonly Wash KeycapEdge = new Wash(".kc {", "rgba(232,194,122,.35)", Head);
        /// <summary>Compare's legend in the strip: the solid swatch and the outline (CompareUi).</summary>
        public static readonly Wash KeySolid = new Wash("i.cmp-sol {", "rgba(242,232,214,.92)", Strip), KeyTrace = new Wash("i.cmp-out {", "rgba(242,232,214,.8)", Strip);

        /// <summary>Every wash above, for the test.</summary>
        public static readonly Wash[] All =
        {
            IslandGround, Track, Band, RowHover, Line, BattleTrack, Rule, Tree, GroupTrack, TrueDealt, TrueReceived, SinceRule, InstallMark, Reach, SparkBar, SparkMark,
            NoPlaceEdge, NotchNext, WaitingRim, TierRim, HoverBand, HoverLine, ChipOn, MenuChosen, MenuChosenEdge, BossBand, BossEdge, EveryoneTag, EveryoneRule, TagEdge,
            KeycapEdge, KeySolid, KeyTrace,
        };

        /// <summary>The colour that draws in game what <paramref name="mirror"/> (a translucent colour as the mirror writes it) draws over
        /// <paramref name="ground"/> in the mirror. The mirror's look, channel by channel, is reached in linear light at the alpha where the channel
        /// that needs the most reaches it with the mirror's own colour; the other channels take a colour between theirs and the ground's. The alpha
        /// is a whole step of 1/255 (a UI vertex colour is bytes), rounded up so every channel stays inside 0..1.</summary>
        public static Color Toward(Color mirror, Color ground)
        {
            double[] c = { mirror.r, mirror.g, mirror.b }, under = { ground.r, ground.g, ground.b }, g = new double[3], t = new double[3];
            double a = 0;
            for (int i = 0; i < 3; i++)
            {
                g[i] = Lin(under[i]); t[i] = Lin(mirror.a * c[i] + (1 - mirror.a) * under[i]);   // the ground and the mirror's look, linear
                var span = Lin(c[i]) - g[i];
                if (Math.Abs(span) > 1e-6) a = Math.Max(a, (t[i] - g[i]) / span);
            }
            if (a <= 0) return new Color(ground.r, ground.g, ground.b, 0f);
            a = Math.Min(1, Math.Ceiling(a * 255 - 1e-6) / 255);
            float Channel(int i) => (float)Srgb(Math.Min(1, Math.Max(0, g[i] + (t[i] - g[i]) / a)));
            return new Color(Channel(0), Channel(1), Channel(2), (float)a);
        }

        /// <summary>A translucent colour over an opaque one, mixed as the mirror mixes it (sRGB).</summary>
        public static Color Over(Color wash, Color ground) =>
            new Color(wash.a * wash.r + (1 - wash.a) * ground.r, wash.a * wash.g + (1 - wash.a) * ground.g, wash.a * wash.b + (1 - wash.a) * ground.b, 1f);

        /// <summary>An sRGB value (0..1) in linear light and back (the sRGB curve, as Unity's Mathf.GammaToLinearSpace).</summary>
        public static double Lin(double v) => v <= 0.04045 ? v / 12.92 : Math.Pow((v + 0.055) / 1.055, 2.4);
        public static double Srgb(double v) => v <= 0.0031308 ? v * 12.92 : 1.055 * Math.Pow(v, 1 / 2.4) - 0.055;

        static Color Rgb(int r, int g, int b) => new Color(r / 255f, g / 255f, b / 255f, 1f);
        /// <summary>"rgba(255,240,210,.05)" as the mirror writes it.</summary>
        public static Color Rgba(string css)
        {
            var p = css.Substring(css.IndexOf('(') + 1).TrimEnd(')').Split(',');
            float N(int i) => float.Parse(p[i].Trim(), CultureInfo.InvariantCulture);
            return new Color(N(0) / 255f, N(1) / 255f, N(2) / 255f, N(3));
        }
    }
}
