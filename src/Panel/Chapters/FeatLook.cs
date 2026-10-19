using System;
using System.Collections.Generic;
using System.Linq;

namespace Hearthwoven.Panel
{
    /// <summary>0.9.1 what lies under a feat card's text (Feats.TextBacking, live): nothing more, a soft band of the card's ground, a dark underlay on the
    /// letters, or both (the default).</summary>
    public enum FeatBacking { None, Fade, Shadow, Both }

    /// <summary>
    /// 0.9 feat look (work/hearthwoven-0.9/TRACK-featlook.md; boards sketch/project/FeatLooks2.dc.html). The cards on Feats > Earned, Unsung and
    /// Together: the family's ground (its hatch in its calm colour, fading to the right), a large icon per feat (FeatScene: the feat's drawn scene; the
    /// emblem where none exists) on a soft dark disc, a flat wide tier bar (one part per tier in its colour, the next part filling), the family's colour
    /// as the left edge, one short plain line. 0.9.2 (Joost in game, 19 Oct: "Let's stick with Look 2 and remove look 1"): the test period's second
    /// look is the one design; look 1 (the tier as a frame, I / II / III badges, the small scenes), its chips and its config entry are gone.
    /// The Titles page keeps its cards. Card fields (written only on these pages): Family (the family's name, "Forgekeeper"), Ground (its colour),
    /// Pattern (its hatch), Line (the short line), Fraction (the next tier's part filled); the "feats" block and every detail carry Look.
    /// </summary>
    public static partial class PanelModel
    {
        /// <summary>Block.Look of the "feats" grid and its details on Earned, Unsung and Together: the look's cards (2, as tested and written since 0.9;
        /// 0 = the plain cards, the Titles page).</summary>
        public const int FeatLookOn = 2;

        /// <summary>The page's line (what a feat is, how many) rides in the strip as a caption with no chips (FeatsIntroKey), so it costs the grid no row
        /// (PanelUi draws a strip switch's caption on the strip's left, here across the strip's whole width). <see cref="FeatsIntro"/> reads it either way.</summary>
        static void FeatsIntroInStrip(Block plate)
        {
            if (plate == null || string.IsNullOrEmpty(plate.Text) || plate.Text.Length > FeatsIntroMax) return;   // a line longer than the strip keeps its row
            (plate.Items ?? (plate.Items = new List<Block>())).Insert(0, new Block { Kind = "switch", Id = FeatsIntroKey, Title = plate.Text, Items = new List<Block>(), Source = plate.Text.Any(char.IsDigit) ? plate.Source ?? TagMeasured : plate.Source });   // its count is the page's (ISC-A-2: every number tagged; a line with no number claims none)
            plate.Text = null;
        }
        /// <summary>The strip's caption on a Feats page: a switch with no views (no chips, no key), only the line.</summary>
        public const string FeatsIntroKey = "Feats/intro";
        /// <summary>The longest line the strip carries (letters; the mirror measures about 6.7 px a letter): a longer one stays the plate's own row.
        /// 0.9.2: the strip's whole width (no Look chips at its end any more): about 750 px of 15 px text.</summary>
        public const int FeatsIntroMax = 110;
        /// <summary>A Feats page's line: the plate's, or the strip caption that carries it.</summary>
        public static string FeatsIntro(PanelView v) =>
            PlateOf(v)?.Text ?? PlateOf(v)?.Items?.FirstOrDefault(b => b.Kind == "switch" && b.Id == FeatsIntroKey)?.Title;

        /// <summary>0.8.93 (Joost in game: "Some bad legibility on the text"): every ink a look writes on a card or in the detail area (PanelLook's Text,
        /// Muted, Faint, Gold, Accent; the tiers' and the families' colours), and the grounds under that text (the card's own ground as the game and the
        /// mirror draw it; the family's ground stays in the icon's zone). FeatLookTests checks each pair at WCAG 4.5:1 or more.</summary>
        public static readonly string[] FeatLookInks = { "#e9dcc4", "#b9a688", "#b0a086", "#e8c27a", "#e8a948" };
        public static readonly string[] FeatLookTextGrounds = { "#090907", "#0c0c0a" };

        /// <summary>The band under the text and the letters' underlay, from the setting.</summary>
        public static bool FeatBand(FeatBacking b) => b == FeatBacking.Fade || b == FeatBacking.Both;
        public static bool FeatShade(FeatBacking b) => b == FeatBacking.Shadow || b == FeatBacking.Both;

        /// <summary>0.9.1 the worst pixel under a card's text as the mirror shows it (and the game, through FeatLookShape's Veil and Ink): the card's ground
        /// with a hatch line of the family's colour at full coverage, after the fade at <paramref name="x"/> (0..1 across the card) and the band, and the
        /// family's wash. "#rrggbb".</summary>
        public static string FeatUnderText(string family, int strength, FeatBacking backing, float x)
        {
            var g = FeatLookTextGrounds[0];
            double C(string h, int i) => Convert.ToInt32(h.Substring(i, 2), 16);
            var line = FeatLookShape.UnderText(strength, FeatBand(backing), x);
            var wash = FeatLookShape.Wash * FeatLookShape.Ramp(FeatLookShape.WashStops, x) * (1f - FeatLookShape.Ramp(FeatLookShape.GroundStops, x));
            var a = 1 - (1 - line) * (1 - wash);
            return "#" + string.Concat(new[] { 1, 3, 5 }.Select(i => ((int)Math.Round(C(g, i) + a * (C(family, i) - C(g, i)))).ToString("x2")));
        }

        /// <summary>WCAG contrast of two "#rrggbb" colours (1 to 21).</summary>
        public static double Contrast(string a, string b)
        {
            double Lum(string h)
            {
                double Ch(int i) { var c = Convert.ToInt32(h.Substring(i, 2), 16) / 255.0; return c <= 0.04045 ? c / 12.92 : Math.Pow((c + 0.055) / 1.055, 2.4); }
                return 0.2126 * Ch(1) + 0.7152 * Ch(3) + 0.0722 * Ch(5);
            }
            double la = Lum(a), lb = Lum(b);
            return (Math.Max(la, lb) + 0.05) / (Math.Min(la, lb) + 0.05);
        }

        /// <summary>
        /// The families' fixed calm colours and hatches (one each; VOCABULARY.md: no tone darker than about CIELAB L 45, every pair at least CIEDE2000 15
        /// apart, and apart from the tiers' bronze, silver and gold: test-panel/FeatLookTests.cs prints the check). The hatch is one of the vocabulary's
        /// grain-hatch tiles; two families share a hatch only where their colours differ most.
        /// </summary>
        public static readonly (string id, string colour, string hatch)[] FeatFamilies =
        {
            ("cook", "#c4807f", "vocab:grain-hatch-dots"),       // Hearth Cook: rose
            ("smith", "#6f97c2", "vocab:grain-hatch-cross"),     // Forgekeeper: steel blue
            ("hauler", "#8a8650", "vocab:grain-hatch-horiz"),    // Storekeeper: olive
            ("sailor", "#3f9183", "vocab:grain-hatch-diag"),     // Helmskeeper: sea green
            ("defender", "#8a7aa0", "vocab:grain-hatch-check"),  // Shieldbearer: muted violet
            ("tamer", "#a3b89a", "vocab:grain-hatch-vert"),      // Beastkeeper: sage
            ("builder", "#c6a6c6", "vocab:grain-hatch-cross"),   // Hallwright: heather
            ("helper", "#56b2bf", "vocab:grain-hatch-dots"),     // Waymate's helper: sky
            // 0.9 more feats: the families that had no feat yet, and the roles (Joost: "archery/ranged ..., magic stuff"); found by search against the rule above
            ("fighter", "#ff7eb4", "vocab:grain-hatch-diag"),    // Battlehand: warm rose-red
            ("archer", "#94de7d", "vocab:grain-hatch-horiz"),    // Bowhand: fletching green
            ("mage", "#b389ea", "vocab:grain-hatch-dots"),       // Staffbearer: eitr violet
            ("bossbane", "#bf5a91", "vocab:grain-hatch-vert"),   // Bossbane: deep berry
            ("delver", "#bba9a4", "vocab:grain-hatch-check"),    // Delver: crypt stone
            ("explorer", "#55a651", "vocab:grain-hatch-cross"),  // Trailfinder: forest green
            ("mapmaker", "#f7bf98", "vocab:grain-hatch-diag"),   // Mapmaker: parchment
            ("mender", "#feb3b7", "vocab:grain-hatch-horiz"),    // Mender: soft coral
            ("wallwarden", "#3acaab", "vocab:grain-hatch-vert"), // Wallwarden: verdigris
            ("woodcutter", "#8b7c71", "vocab:grain-hatch-vert"), // Woodcutter: bark
            ("miner", "#6d7e81", "vocab:grain-hatch-dots"),      // Stonebreaker: slate
            ("farmer", "#a8b34b", "vocab:grain-hatch-check"),    // Fieldkeeper: barley
            ("fisher", "#84cdfe", "vocab:grain-hatch-diag"),     // Tidecatcher: shallow water
            // 0.9.1 batch 2: the weapon styles' own family, found by the same search (CIEDE2000 15.1 from the nearest, its hatch's others 26 or more apart)
            ("melee", "#b8befa", "vocab:grain-hatch-horiz"),     // Armsman: periwinkle steel
        };
        /// <summary>The family of a feat with no title of its own yet (Waymate): its line's word.</summary>
        public const string HelperFamily = "Helper";

        public static (string id, string colour, string hatch) FeatFamily(string id) => FeatFamilies.FirstOrDefault(f => f.id == id);
        /// <summary>A family's name as its title says it ("Forgekeeper"); Waymate's helper has none yet.</summary>
        public static string FeatFamilyName(string id) => SagaTitles.FirstOrDefault(t => t.Id == id)?.Title ??
            (id == "helper" ? HelperFamily : id == "delver" ? "Delver" : id == "archer" ? "Bowhand" : id == "mage" ? "Staffbearer" : id == "melee" ? "Armsman" : id);   // 0.9: the families with no title

        /// <summary>
        /// A feat's own picture (0.9.1, Joost: "creative designs for the icons ... for all of these"): its drawn scene "vocab:feat-scene-&lt;id&gt;" (112 px,
        /// the card and the detail, drawn at 56 px; 0.9.2: look 1's small 72 px render went with look 1), else the feat's emblem (FeatPicture).
        /// <paramref name="has"/> says whether a vocabulary sprite exists (the game: PanelLook.VocabNames; the tests: the files in src/Panel/vocab). The scenes' sources: src/Panel/vocab-src/feats/&lt;id&gt;.svg and render.mjs there.
        /// </summary>
        public static string FeatScene(string id, string emblem, Func<string, bool> has)
        {
            var name = FeatSceneSprite + id;
            return has != null && has(name) ? "vocab:" + name : emblem;
        }
        public const string FeatSceneSprite = "feat-scene-";
        /// <summary>Where the scenes are looked up (PanelUi sets PanelLook.Vocab; null: none yet, the emblem).</summary>
        public static Func<string, bool> SceneExists;

        /// <summary>The card's one short plain line: what the highest tier reached took ("5 000 ore and fuel"), the next tier's on an Unsung card, the
        /// waiting words on a feat not counted yet; the feat's own few words (its brief), a veiled feat's naming no material.</summary>
        static string FeatLookLine(PanelInput input, FeatDef d, int tier, bool waiting, string text) =>
            waiting ? text : FeatBrief(input, d, Math.Max(0, Math.Min(tier, d.Tiers.Length) - 1)) ?? text ?? FeatWords(input, FeatHonours(d, input));

        /// <summary>Dresses one page's cards and their details for the look (FeatsChapter): family, ground, hatch, the line, the next tier's part.</summary>
        static void FeatLookDress(PanelInput input, List<Block> cards)
        {
            foreach (var c in cards)
            {
                var d = FeatById(c.Id); if (d == null) continue;
                var fam = FeatFamily(d.Family);
                var tier = (int)c.Level; var waiting = c.Tone == FeatWaitingTone;
                c.Family = FeatFamilyName(d.Family); c.Ground = fam.colour; c.Pattern = fam.hatch;
                c.Line = FeatLookLine(input, d, tier, waiting, c.Text);
                var detail = c.Items?.FirstOrDefault();
                if (!FeatVeiled(d, input))   // the feat's own picture on the card and in its detail; a veiled feat keeps its land's emblem (its scene would name it)
                {
                    c.Icon = FeatScene(d.Id, c.Icon, SceneExists);
                    if (detail != null) detail.Icon = FeatScene(d.Id, detail.Icon, SceneExists);
                }
                var progress = detail?.Items?.FirstOrDefault(i => i.Kind == "progress");
                c.Fraction = progress != null ? progress.Fraction : 0f;   // the tier bar: the next part filled as far as your own exact number goes
                if (detail == null) continue;
                detail.Look = FeatLookOn; detail.Family = c.Family; detail.Ground = c.Ground; detail.Pattern = c.Pattern;
            }
        }
    }
}
