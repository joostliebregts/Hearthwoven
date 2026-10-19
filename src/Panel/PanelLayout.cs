namespace Hearthwoven.Panel
{
    /// <summary>
    /// 0.8.5 the book's layout numbers in one table (work/hearthwoven-0.8.5/LAYOUT-TABLE-PLAN.md): px at 1080p, scale 1, named by the parts of
    /// the page. The game reads them here. The preview reads the same values as CSS custom properties from preview/layout.css, which
    /// `KsPanelTest --dump` writes from this table (Hero.Second is --hero-second; never edited by hand). A page drawing with geometry of its own
    /// (the Fireside solver, the compass, the ladders, the battle grid, the feats detail) keeps its numbers as named constants at the top of its
    /// file until it joins here; test-panel/LayoutTests.cs counts the numbers still written out, and that count only goes down.
    /// Plain C# without Unity types, so the model and the tests read it offline; static readonly, not const, so a value can later change
    /// while the game runs. Colours stay in PanelLook.
    /// </summary>
    public static class PanelLayout
    {
        /// <summary>The text sizes, today's steps (fewer steps would be a change people see: Joost's call).</summary>
        public static class Text
        {
            /// <summary>The floor (PanelLook.MinText): notes, qualifiers, chips, a legend's names, captions. Nothing is smaller, but the
            /// digits inside a damage bar (PanelLook.MinBarDigits).</summary>
            public static readonly float Floor = PanelLook.MinText;
            /// <summary>A quiet line: a legend's number, the keys line, a strip's words, a section heading's total.</summary>
            public static readonly float Small = 15;
            public static readonly float Body = 16;
            /// <summary>A name in a list row and its number (rankings, compare rows, damage rows).</summary>
            public static readonly float Name = 17;
            /// <summary>A list entry, the scope line under the heading.</summary>
            public static readonly float Row = 18;
            /// <summary>A count in a list, a tile or a ledger.</summary>
            public static readonly float Count = 19;
            /// <summary>The owner's name; a section heading on a page without a plate.</summary>
            public static readonly float Lead = 20;
            /// <summary>A band's title, a toggle.</summary>
            public static readonly float Band = 21;
            /// <summary>The list title, a stat's name.</summary>
            public static readonly float Title = 22;
            /// <summary>The plate's heading row.</summary>
            public static readonly float Heading = 24;
            /// <summary>The page heading.</summary>
            public static readonly float Page = 26;
            /// <summary>A big number in a page drawing: a skill's level, a journey's length.</summary>
            public static readonly float Big = 28;
            /// <summary>A stat's number.</summary>
            public static readonly float Stat = 30;
            /// <summary>The wordmark, where the logo is missing.</summary>
            public static readonly float Wordmark = 32;
            /// <summary>A card's number.</summary>
            public static readonly float Card = 38;
            /// <summary>The number at an axis's end.</summary>
            public static readonly float Axis = 48;
            /// <summary>Battle's guard numbers.</summary>
            public static readonly float Guard = 50;
        }

        /// <summary>The book's window: the frame, the tabs, the list column on the left, the content column right of it, the key line.</summary>
        public static class Frame
        {
            public static readonly float W = 1180, H = 760;
            /// <summary>The list column's left edge.</summary>
            public static readonly float Inset = 32;
            public static readonly float ListW = 190;
            /// <summary>The page heading and the list title stand here; the list starts at ListTop; the list and content end Foot above the bottom.</summary>
            /// 0.9.1 (Joost: "where it now says all those key binds ... we can probably take a little bit more vertical space"): Foot 72 -> 52 with the
            /// slimmer key line (Keys), 20 px more for every page's content.
            public static readonly float HeadTop = 144, ListTop = 180, Foot = 52;
            /// <summary>The tabs: TabTop down, TabH high, TabInset in from each side (room for the Q and E keys), TabGap apart; in a tab its icon
            /// (TabIcon square) and its name TabIconGap apart, centred.</summary>
            public static readonly float TabTop = 77, TabH = 52, TabInset = 70, TabGap = 8, TabIcon = 26, TabIconGap = 8;
            /// <summary>The top row, centred TopRow down: the logo LogoH high from LogoLeft (the wordmark, where the logo is missing, in the owner's
            /// box from WordmarkLeft); the owner's name from OwnerLeft in a box OwnerW by OwnerH; the player chips PlayersH high and PlayersGap
            /// apart, ending PlayersRight in from the frame's right edge, in a row PlayersW wide (PlayersWideW with the Everyone chip).</summary>
            public static readonly float TopRow = 44, LogoLeft = 64, LogoH = 60, WordmarkLeft = 66, OwnerLeft = 385, OwnerW = 300, OwnerH = 44,
                                         PlayersRight = 64, PlayersH = 48, PlayersGap = 8, PlayersW = 620, PlayersWideW = 810;
            public static float LogoTop => TopRow - LogoH / 2;
            public static float OwnerTop => TopRow - OwnerH / 2;
            public static float PlayersTop => TopRow - PlayersH / 2;
            /// <summary>A list entry's height and the gap between entries; the room under the list for the S key.</summary>
            public static readonly float ListRow = 40, ListGap = 8, KeyRoom = 40;
            /// <summary>The content column: ContentGap right of the list, its right edge ContentRight in from the frame's.</summary>
            public static readonly float ContentGap = 22, ContentRight = 40;
            /// <summary>The content column's width as the game draws it: a page heading's width, the width blocks are drawn at off a plate.</summary>
            public static readonly float ContentW = 889;
            /// <summary>A key cap (Q, E, W, S).</summary>
            public static readonly float Keycap = 24;
        }

        /// <summary>The page head above the plate: the heading (and the scope line under it on a page without a plate), the list title.</summary>
        public static class Heading
        {
            public static readonly float H = 36, ScopeTop = 38, ScopeH = 26, ListTitleH = 30;
            /// <summary>The heading's icon, its top, and the gap between the chips at the heading row's right end.</summary>
            public static readonly float Icon = 26, IconTop = 5, ChipGap = 4;
            /// <summary>0.8.5 (proto/4-compare A): a heading that would shrink below TwoAt goes on two lines of TwoSize instead, TwoSpacing (TMP's
            /// line spacing, em/100) closer than the face's own lines, so the two fit the row (PanelModel.HeadingLines).</summary>
            public static readonly float TwoAt = 20, TwoSize = 18, TwoSpacing = -22, TwoTop = -3, TwoH = 42;
            /// <summary>A page without a plate (the page that could not be drawn): its blocks from this far down, BlocksGap apart.</summary>
            public static readonly float Blocks = 76, BlocksGap = 10;
        }

        /// <summary>The plate: the recessed page body, from the list's top line.</summary>
        public static class Plate
        {
            /// <summary>Top: under the heading row; Inset: the edge's width; the padding inside it; Gap: between its islands.</summary>
            public static readonly float Top = 36, Inset = 4, PadX = 24, PadTop = 8, PadBottom = 10, Gap = 14;
            /// <summary>The Feats chapter's full plate keeps its own measures (FeatsUi.FeatsRoom).</summary>
            public static readonly float FullPadTop = 12, FullGap = 10;
            /// <summary>The plate's inner width: what a wide island is.</summary>
            public static readonly float Column = 840;
            /// <summary>0.8.6 the sticky head (StickyHeadUi.cs): Fade, the soft edge at a scroll area's end that has more beyond it (PanelUi.Fade); StripShade,
            /// the shade under the pinned strip while rows lie above it.</summary>
            public static readonly float Fade = 40, StripShade = 10;
        }

        /// <summary>An island: a plain rounded rectangle on the plate around one group of blocks.</summary>
        /// <summary>0.9 Armor sets (DeedsUi.SetBand): a set's band (its padding and the gap between its head line and its tiles), and a piece not made
        /// yet: the gap between its materials, between a material's picture and its amount, the picture's size.</summary>
        public static class Sets
        {
            public static readonly float BandPadX = 12, BandPadTop = 10, BandPadBottom = 12, BandGap = 8, CostGap = 6, CostPicGap = 2, CostPic = 16;
        }

        /// <summary>
        /// 0.9 an item tile (DeedsUi.ItemGrid): its padding above and below the count and the name, the least it is high and what a wrapped name, a second
        /// number or a since line adds. Fault (Joost in game on Crafting, 18 Oct: three tiles without names): a tile whose names all fit one line was 46 px,
        /// so its 40 px inside held less than the count's line box (25) and the name's (19): the layout squeezed the name below one line and TMP's ellipsis
        /// then drew none of it. Least is the two line boxes and the padding, so a tile never holds less than one line of its name.
        /// </summary>
        public static class Tile
        {
            public static readonly float Pad = 3, Base = 46, Line = 14;
            /// <summary>The count's and one name line's line boxes with the padding: the least a tile is high.</summary>
            public static float Least => 2 * Pad + LineBox(Text.Count) + LineBox(Text.Floor);
            /// <summary>A tile's height: at least Least; a wrapped name adds a name line, a second number or a since line a line each.</summary>
            public static float Height(bool wraps, int extraLines) => System.Math.Max(Base, Least) + (wraps ? LineBox(Text.Floor) : 0) + extraLines * Line;
        }

        /// <summary>0.9 the level meter (Crafting's Upgrades per item, DeedsUi.Ranking's level rows): the weakness meter of Battle > Foes (BattleUi.BattleMeter)
        /// at a row's size: four parts PartW wide and H high, Gap apart; the words "now level 3 of 4" after it in a column Words wide; a row Row high, its
        /// picture, name, meter, words and count RowGap apart.</summary>
        public static class Level
        {
            public static readonly float PartW = 28, H = 12, Gap = 2, Words = 132, Row = 34, RowGap = 10;
            public static float MeterW => 4 * PartW + 3 * Gap;
        }

        /// <summary>
        /// 0.9 History (a Deeds page's growth line drawn large, GrowthLinesUi.SparkLarge): the columns' room (ChartH), the gap between two columns, the widest a
        /// column grows, the least height of a day with something, an empty day's flat mark, the gold tick over a day with a first or a feat (its size and
        /// its gap above the column), the date line under the columns.
        /// </summary>
        public static class History
        {
            public static readonly float ChartH = 150, ColumnGap = 5, MaxColumn = 30, Least = 2, Flat = 2, Tick = 6, TickGap = 3, Axis = 20;
        }

        public static class Island
        {
            /// <summary>Pad: inside it; Inner: between its blocks (a heading and its list); PairGap: between two half islands side by side.</summary>
            public static readonly float Pad = 14, Inner = 6, PairGap = 14, Radius = 10;
            /// <summary>The hero's island: little space above its number.</summary>
            public static readonly float HeroTop = 8, HeroBottom = 10;
            /// <summary>A column narrower than this is a half island.</summary>
            public static readonly float Narrow = 500;
        }

        /// <summary>The hero: the page's big number and its words, further numbers smaller after a thin rule.</summary>
        public static class Hero
        {
            public static readonly float Size = 67, Label = 26, Second = 43, SecondLabel = 20;
            /// <summary>RuleGap: either side of the rule; NumberGap: between a number and its words; WordsGap: between the words and their notes.</summary>
            public static readonly float RuleGap = 34, NumberGap = 14, WordsGap = 8;
        }

        /// <summary>The one bar form's bar (BAR-FORM.md).</summary>
        public static class Bar
        {
            /// <summary>Height: every bar; Thin: the Hall's; Tall: a bar with a wood grain or groundwork pattern.</summary>
            public static readonly float Height = 22, Thin = 22, Tall = 24;
            /// <summary>Gap: between the parts; MinPart: the least a part is drawn; ToList: between the bar and its legend; After: after the whole form
            /// (none: it sits in an island, whose padding is the air).</summary>
            public static readonly float Gap = 4, MinPart = 6, ToList = 10, After = 0;
            /// <summary>A bar's head line (its heading in the island) when it has one.</summary>
            public static readonly float HeadLine = 18;
            /// <summary>The number inside a part.</summary>
            public static readonly float Number = Text.Small;
        }

        /// <summary>A bar's legend: compact entries on the page's one legend grid.</summary>
        public static class Legend
        {
            /// <summary>Row: an entry's height; RowGap: between entries; ColumnGap: between columns; CellGap: between an entry's cells; Gap: the least
            /// space between two columns of the page grid.</summary>
            public static readonly float Row = 22, RowGap = 6, ColumnGap = 36, CellGap = 8, Gap = 10;
            public static readonly float Swatch = 12, SwatchGap = 8;
            /// <summary>The text: the name at the floor, the number, the share.</summary>
            public static readonly float Name = Text.Floor, Number = Text.Small, Share = Text.Floor;
        }

        /// <summary>A small chip: the heading row's pill, a view switch, the window choices.</summary>
        public static class Chip
        {
            /// <summary>Pad: each side; Icon: its picture, IconLeft in from the left, the label IconRoom in; WindowPad: the window chips' tighter sides
            /// (0.9.1: 4, was 8: eleven window chips with Yesterday and 3 days, and the heading beside them still whole). Swatch: a part's colour
            /// before the word (0.9.8 Meal plan's Best for: Health, Stamina, Eitr; the bars' legends' 10 px square), SwatchGap to the word.</summary>
            public static readonly float Height = 26, Pad = 11, Icon = 18, IconLeft = 9, IconRoom = 32, WindowPad = 4, Swatch = 10, SwatchGap = 7;
            public static readonly float Text = PanelLayout.Text.Floor;
        }

        /// <summary>A section heading on the plate: small capitals, its total beside it.</summary>
        public static class Section
        {
            public static readonly float Text = PanelLayout.Text.Floor, Total = PanelLayout.Text.Small;
        }

        /// <summary>Columns: stretches of blocks side by side, equal widths; Stack: between the blocks in one.</summary>
        public static class Columns
        {
            public static readonly float Gap = 34, Stack = 12;
        }

        /// <summary>The key line at the frame's foot. 0.9.1: slimmer (30 -> 24 high, 22 -> 18 from the frame's foot), so the content ends 20 px lower.</summary>
        public static class Keys
        {
            public static readonly float Side = 64, Bottom = 18, Height = 24;
            public static readonly float Text = PanelLayout.Text.Small;
        }

        /// <summary>The keyboard focus ring: its sprite's glow reaches Inset outside the line, the line Gap outside the box.</summary>
        public static class Focus
        {
            public static readonly float Inset = 8, Gap = 2;
        }

        /// <summary>A window with nothing in it (Kind "empty"): its heading and its one line, Gap apart (0.8.5: the mirror pulled the line up into
        /// the heading with a negative margin).</summary>
        public static class Empty
        {
            public static readonly float Gap = 2;
        }

        /// <summary>The filter bar: a row's label column, the chips.</summary>
        public static class Filter
        {
            public static readonly float LabelW = 128, ChipH = 24, ChipGap = 4;
            public static readonly float Text = PanelLayout.Text.Floor;
        }

        /// <summary>The sort row.</summary>
        public static class Sort
        {
            public static readonly float Height = 28, Gap = 6;
        }

        /// <summary>Battle > Deaths' Where you fell (0.8.5, the tiles grow): a tile's foot holds its name, a dated fall is a line in it, the falls grow
        /// into the room above the foot, a count's line sits over the name; tiles at most TileMax wide, Gap apart, the sea and the falls with no place
        /// Apart further.</summary>
        public static class Fell
        {
            public static readonly float Foot = 36, Line = 24, Room = 132, CountLine = 22, TileMax = 150, Gap = 4, Apart = 12;
        }

        /// <summary>A link to another page (Battle > Overview's Blocks and parries): a compact entry (PanelUi.Entry under 34 px: body text).</summary>
        public static class Link
        {
            public static readonly float Height = 32;
        }

        /// <summary>Battle's feed: between an earlier session's line (its day and span) and the hairline after it; 0.8.5 between the Show older
        /// foes button and the words beside it (what is shown, what is left).</summary>
        public static class Feed
        {
            public static readonly float SessionGap = 10, MoreGap = 14;
        }

        /// <summary>0.8.5 a small sum (Voyages > Cargo: "[crate] 100 × [ship] 4.1 km = 410 item-km"). Under the bar: its pictures (Icon), the gap
        /// between its terms and signs (Gap), between a picture and its number, between the result and its unit, between the caption and the sum.
        /// Usually aboard as sums: a row per item RowH high, RowGap apart, RowPad in at each end, its item's picture (RowPic), its sum's pictures
        /// (RowIcon), RowTermGap between everything in the row; the total ("together 640 item-km") under a hairline, its number at Total.</summary>
        public static class Sum
        {
            public static readonly float Icon = 24, Gap = 10, IconGap = 7, UnitGap = 5, CaptionGap = 4;
            public static readonly float RowH = 36, RowGap = 6, RowPad = 10, RowPic = 26, RowIcon = 20, RowTermGap = 7;
            public static readonly float Text = PanelLayout.Text.Body, Caption = PanelLayout.Text.Floor, Name = PanelLayout.Text.Name, Total = PanelLayout.Text.Lead;
        }

        /// <summary>0.9.2 feat search (Chapters/FeatSearchUi.cs): the search field at the Feats pages' top right (its width, height, inner pad and key
        /// cap), and the page tag on a card of the search's grid (its height, its inset from the card's top, inner pad and the gap to the New mark).</summary>
        public static class FeatSearch
        {
            public static readonly float W = 200, H = 28, Pad = 8, Cap = 20, TagH = 16, TagTop = 3, TagPad = 6, TagGap = 6;
        }

        /// <summary>The kitchen ledger (Deeds > Cooking): a row's name and its source line under it, each in a box of its font's line (LineBox).</summary>
        public static class Ledger
        {
            public static float NameLine => LineBox(PanelLayout.Text.Name);
            public static float SourceLine => LineBox(PanelLayout.Text.Floor);
        }

        /// <summary>The height a one-line box needs for text of <paramref name="size"/>, the floor applied: TMP's ellipsis drops a whole line whose box
        /// is lower than the font's line (about 1.16 em in the game's serif), so a box never goes under 1.3 em (live-polish).</summary>
        public static float LineBox(float size) => (float)System.Math.Ceiling(System.Math.Max(size, Text.Floor) * 1.3);

        /// <summary>The Feats grid: a card, the gaps between its rows and its columns; the grid's least room is four whole rows.</summary>
        public static class Feats
        {
            public static readonly float Card = 52, RowGap = 6, ColumnGap = 8;
            /// <summary>0.9.1: a Titles card's least height: its name and a reason on two lines (Hallwright's "Raise your first piece with the hammer." was cut
            /// once the slimmer key line gave the grid the room to show it).</summary>
            public static readonly float TitleCard = 58;
            public static float GridRoom => 4 * Card + 3 * RowGap;
            /// <summary>0.9 New feats: the New mark on a card, top right: the gold dot's size, the gap to the word, and its inset from the card's top and right edge.</summary>
            public static readonly float NewDot = 9, NewGap = 5, NewInset = 6;
            /// <summary>0.9.3 the chosen card (FeatsUi.FeatChosen): its gold frame, the 2 px rim and 5 px corners of PanelLook.RoundedEdge drawn at its own scale.</summary>
            public static readonly float ChosenRim = 2, ChosenRound = 5;
            /// <summary>0.9: a feat's link to its page in the detail area (Open Armor sets): its inset from the area's right and bottom edge, and the
            /// room between its word and the opens mark after it.</summary>
            public static readonly float LinkInset = 16, LinkBottom = 12, LinkMarkGap = 6;
            /// <summary>0.9.2 the More below cue over the grid's foot (FeatsUi.FeatMoreCue): the band's height, and its words' line this far above the
            /// band's bottom edge.</summary>
            public static readonly float CueBand = 44, CueTextBottom = 4;
        }

        /// <summary>0.9 feat look (Chapters/FeatLookUi.cs): a card's least and most height; the large icon, its left inset, the gap to the text and the
        /// soft disc's reach around it; the text's right inset; the thin rim the band under the text keeps clear of; the tier bar (height, most width, gap
        /// between parts, room above) and family edge; the detail area's family line, name and tier line tops and its ground's zone (behind the emblem,
        /// left of the name, above the line under it). 0.9.2: look 1's small icon, badges, frames and knots went with it.</summary>
        public static class FeatLook
        {
            public static readonly float CardMin = 68, CardMax = 84, IconLarge = 56, IconLeftLarge = 8, IconGap = 12, Halo = 22, TextRight = 8;
            public static readonly float RimThin = 1;
            public static readonly float BarH = 6, BarW = 156, BarGap = 2, BarTop = 4, Edge = 4;
            public static readonly float DetailFamilyTop = 3, DetailNameTop = 17, DetailTierTop = 45;
            /// <summary>0.9.1 (Joost: "I really did like all the cards when the pattern was on the background of the full card"): the family's ground
            /// across the whole card again (board 9), strongest at the left and fading right (FeatLookShape.GroundStops over the card's width); in the
            /// detail area over GroundW from its left and fading down by GroundH (its left column's text under the emblem). Under the text a soft band of
            /// the card's own ground (Feats.TextBacking "Fade"): it rises over BackingIn px before the text column's left edge.</summary>
            public static readonly float DetailGroundW = 420, DetailGroundH = 96, BackingIn = 20;
        }

        /// <summary>
        /// 0.9.1 the feat detail area, compact (Joost: "the bottom one takes a little bit too much space ... taking away from the achievements list"): it is as
        /// high as its page's tallest detail needs (FeatsUi.FeatDetailPage; at least Least), no empty band kept for text that is not there. The left column:
        /// the emblem from EmblemTop, the name and tier line (NameTop, TierTop without a look; a look's from FeatLook), what the feat honours from HonoursTop
        /// (one to three lines of HonoursLine), the moment MomentGap under it (one or two lines of MomentLine). The right column: the rules RuleH apart
        /// from Top, ProgressGap more to the progress line (ProgressRow), Counted by (CountedRow), the caveat (one line CaveatOne, two CaveatTwo), the crew;
        /// a link chip at its foot takes LinkRow. Pad under the lowest line.
        /// </summary>
        public static class FeatDetail
        {
            public static readonly float Least = 116, Top = 8, Pad = 6, EmblemTop = 8, NameTop = 6, TierTop = 38;
            public static readonly float HonoursTop = 68, HonoursLine = 19, HonoursMost = 60, MomentGap = 3, MomentLine = 20, MomentMost = 40;
            public static readonly float RuleH = 19, ProgressGap = 2, ProgressRow = 22, CountedRow = 20, CaveatOne = 20, CaveatTwo = 38, LinkRow = 30;
            /// <summary>Where the caveat starts after <paramref name="rules"/> rules (the crew's top adds the caveat's lines).</summary>
            public static float CaveatTop(int rules) => Top + rules * RuleH + ProgressGap + ProgressRow + CountedRow;
            public static float CaveatRoom(int lines) => lines <= 0 ? 0 : lines == 1 ? CaveatOne : CaveatTwo;
            /// <summary>The right column's need without a crew: its lines (the last row's 2 px gap off), the link's row if any, the pad.</summary>
            public static float RightNeed(int rules, int caveatLines, bool link) => CaveatTop(rules) + CaveatRoom(caveatLines) - 2 + (link ? LinkRow : 0) + Pad;
            /// <summary>The left column's need for what the feat honours and its moment as measured (px, clamped to their lines).</summary>
            public static float LeftNeed(float honours, float moment) =>
                HonoursTop + Clamp(honours, HonoursLine, HonoursMost) + (moment > 0 ? MomentGap + Clamp(moment, MomentLine, MomentMost) : 0) + Pad;
            public static float Clamp(float v, float lo, float hi) => (float)System.Math.Ceiling(System.Math.Min(hi, System.Math.Max(lo, v)));
        }
    }
}
