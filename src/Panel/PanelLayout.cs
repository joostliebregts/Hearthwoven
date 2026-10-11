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
            public static readonly float HeadTop = 144, ListTop = 180, Foot = 72;
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
        }

        /// <summary>An island: a plain rounded rectangle on the plate around one group of blocks.</summary>
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
            /// <summary>Pad: each side; Icon: its picture, IconLeft in from the left, the label IconRoom in; WindowPad: the window chips' tighter sides.</summary>
            public static readonly float Height = 26, Pad = 11, Icon = 18, IconLeft = 9, IconRoom = 32, WindowPad = 8;
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

        /// <summary>The key line at the frame's foot.</summary>
        public static class Keys
        {
            public static readonly float Side = 64, Bottom = 22, Height = 30;
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

        /// <summary>Battle's feed: between an earlier session's line (its day and span) and the hairline after it.</summary>
        public static class Feed
        {
            public static readonly float SessionGap = 10;
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
            public static float GridRoom => 4 * Card + 3 * RowGap;
        }
    }
}
