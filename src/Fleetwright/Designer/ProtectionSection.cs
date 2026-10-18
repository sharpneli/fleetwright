using System.Globalization;
using System.Numerics;
using Fleetwright.Shipgen;
using ImGuiNET;
using T = Fleetwright.Designer.DesignerTheme;

namespace Fleetwright.Designer;

/// <summary>
/// A half-section of the built ship at one station (docs/designer-ui/protection-layers.md, section 1), from its hitboxes:
/// the hull's side from the form's stations, the cells tinted by what their rooms hold, the longitudinal walls and
/// platform decks, and the armour as polygons, three times as thick as it is. Coordinates are (y out from the
/// centreline, z above the main deck), starboard. Built once per ship and station; drawn mirrored to either side, so the
/// Protection tab shows two stations side by side and the Hull tab can show the form.
/// </summary>
public sealed class ProtectionSection
{
    /// <summary>Plates are drawn this many times as thick as they are.</summary>
    public const float Stretch = 3;

    /// <summary>What an armour polygon is: the table row it belongs to (Row: a design deck's index for the deck kinds).</summary>
    public enum Kind { Belt, BeltZone, LowerBelt, Upper, EndFore, EndAft, Deck, DeckZone, Slope, Inner, Box, Barbette }

    public enum Label { None, Side, Under, Over }

    public sealed record ArmourPoly(Kind Kind, int Row, double Mm, Vector2[] Pts, Vector2 At, string Text, Label Place, bool Faint = false);

    public readonly record struct CellBox(float Y0, float Y1, float Z0, float Z1, RoomKind? Kind, string Text, string Tip);

    public readonly record struct Wall(float Y, float Z0, float Z1, string Kind, double PlateMm, string Tip);

    /// <summary>A slope's knee (where the flat turns down) and foot, for its handle.</summary>
    public readonly record struct Knee(int Row, float Y, float Z, float FootY, float FootZ, double Deg);

    public float X { get; private init; }
    public float Keel { get; private init; }
    public float Top { get; private init; }
    public float Wl { get; private init; }
    public float Ib { get; private init; }
    public float Beam { get; private init; }
    /// <summary>The hull's side, keel to deck: (half-breadth, z).</summary>
    public Vector2[] Side { get; private init; } = [];
    public CellBox[] Cells { get; private init; } = [];
    public Wall[] Walls { get; private init; } = [];
    /// <summary>The decks across at this station (z, its short name); platform decks apart.</summary>
    public (float Z, string Name)[] Decks { get; private init; } = [];
    public (float Z, string Tip)[] Platforms { get; private init; } = [];
    public ArmourPoly[] Armour { get; private init; } = [];
    public Knee[] Knees { get; private init; } = [];
    /// <summary>The belt here: its edges, the y of its outer face at its top, and its lean (null: no belt at this
    /// station).</summary>
    public (float Top, float Bottom, float TopY, float Lean, bool Inside)? Belt { get; private init; }
    /// <summary>The TDS's holding bulkhead (its y halfway up), the magazine wing's wall beside a magazine (and the
    /// magazine's floor and crown), and the inner bulkhead's own wall where it stands on its own.</summary>
    public (float Y, float Z0, float Z1)? Holding { get; private init; }
    public (float Y, float Z0, float Z1)? WingWall { get; private init; }
    public (float Y, float Z0, float Z1)? OwnInner { get; private init; }
    /// <summary>What the inner bulkhead rides on here: "tds", "wing", "own" or "".</summary>
    public string InnerOn { get; private init; } = "";
    /// <summary>Coal in the cells (the legend's swatch).</summary>
    public bool HasCoal { get; private init; }
    readonly HullFormReport form;

    ProtectionSection(HullFormReport form) => this.form = form;

    public float HalfB(float z) => (float)form.HalfWidth(X, z + 1e-4);

    // ------------------------------------------------------------------ the build

    static (string Name, string Short) KindNames(RoomKind? k) => k switch
    {
        null => ("void", ""),
        RoomKind.Magazine => ("magazine", "mag"), RoomKind.Handling => ("handling room", "handling"), RoomKind.Stores => ("stores", "stores"),
        RoomKind.Tds => ("torpedo protection", "TDS"), RoomKind.Bunker => ("coal", "coal"), RoomKind.EngineRoom => ("engine room", "engines"),
        RoomKind.BoilerRoom => ("boiler room", "boilers"), RoomKind.DoubleBottom => ("double bottom", ""), RoomKind.Accommodation => ("quarters", ""),
        RoomKind.Steering => ("steering gear", "steering"), RoomKind.TorpedoRoom => ("torpedo room", "torpedoes"), RoomKind.FuelTank => ("oil", "oil"),
        RoomKind.CargoTank => ("cargo tank", "cargo"), RoomKind.Hold => ("hold", "hold"), RoomKind.Hangar => ("hangar", ""),
        RoomKind.TransmittingStation => ("transmitting station", "TS"),
        _ => (k.ToString()!, ""),
    };

    static string F1(double v) => v.ToString("0.0", CultureInfo.InvariantCulture);
    static string Sx1(double v) => (v < 0 ? "−" : v > 0 ? "+" : "") + F1(Math.Abs(v));
    static string N0(double v) => v.ToString("N0", CultureInfo.InvariantCulture);

    /// <summary>The section at station x of the built ship. <paramref name="deckRow"/> finds the design's armour deck for
    /// a built one (its deck's name and extent; -1 for none).</summary>
    public static ProtectionSection Build(Ship ship, double x, Func<string, Extent, int> deckRow)
    {
        var hb = ship.Hitboxes;
        var d = ship.Design;
        var a = d.Armour ?? new ArmourInput();
        var st = hb.HullForm.Stations;
        float fx = (float)x;
        float keel = (float)Interp(st, x, s => s.Z[0]);
        float top = 0;
        foreach (var r in hb.Vertical.Raised ?? [])
            if (x >= r.X0 && x <= r.X1)
                top = MathF.Max(top, (float)r.Top);
        float wl = (float)hb.Vertical.Waterline;
        float ib = (float)(hb.Decks.FirstOrDefault(k => k.Kind == "inner_bottom")?.Z ?? keel + 1.1);
        var sec = new ProtectionSection(hb.HullForm) { X = fx, Keel = keel, Top = top, Wl = wl, Ib = ib, Beam = (float)hb.Beam };
        float Hb(double z) => (float)hb.HullForm.HalfWidth(x, z + 1e-4);

        var side = new List<Vector2>();
        for (int i = 0; i <= 40; i++)
        {
            float z = keel + (top - keel) * i / 40;
            side.Add(new Vector2(Hb(z), z));
        }

        // the cells and what holds them
        var kindOf = hb.Rooms.ToDictionary(r => r.Id, r => r.Kind, StringComparer.Ordinal);
        var deckZs = hb.Decks.Where(k => k.Kind is "deck" or "main" or "raised" or "inner_bottom").Select(k => k.Z).ToList();
        var cells = new List<CellBox>();
        foreach (var c in hb.Cells)
        {
            if (c.X0 > x || c.X1 <= x || c.Y1 <= 0.01)
                continue;
            RoomKind? kind = kindOf.TryGetValue(c.Room, out var k) ? k : null;
            var (name, shortName) = KindNames(kind);
            string text = kind == RoomKind.Magazine ? c.Room.Replace("Magazine ", "mag ", StringComparison.Ordinal) : shortName;
            string tip = $"{c.Room} · {name}\n{F1(Math.Max(0, c.Y0))}–{F1(c.Y1)} m out · {Sx1(c.Base - wl)} to {Sx1(c.Top - wl)} m on the WL";
            cells.Add(new CellBox((float)Math.Max(0, c.Y0), (float)c.Y1, (float)c.Base, (float)c.Top, kind, text, tip));
        }
        // a few words on why a space is there
        for (int i = 0; i < cells.Count; i++)
        {
            var c = cells[i];
            string extra = c.Kind switch
            {
                RoomKind.Stores when cells.Any(m => m.Kind == RoomKind.Magazine && Math.Abs(m.Y1 - c.Y0) < 0.05 && m.Z0 <= c.Z0 + 0.05 && m.Z1 >= c.Z1 - 0.05)
                    => "\nThe magazine's wing: stores between it and the side.",
                RoomKind.Handling => "\nOver the magazine, under the deck: the flash path's node.",
                RoomKind.Tds => (a.TdsLayers ?? 1) > 1 ? $"\nA space of the torpedo protection, one of {a.TdsLayers} layers." : "\nA space of the torpedo protection.",
                RoomKind.Magazine => $"\nCrown {F1(wl - c.Z1)} m under the waterline" +
                              (deckZs.Any(z => Math.Abs(z - c.Z1) < 0.05) ? "." : ", a platform deck over it."),
                _ => "",
            };
            if (extra.Length > 0)
                cells[i] = c with { Tip = c.Tip + extra };
        }

        // the decks across, the platform decks over magazines
        var decks = new List<(float, string)>();
        var plats = new List<(float, string)>();
        foreach (var k in hb.Decks)
        {
            bool over = k.Spans is not { Count: > 0 } || k.Spans.Any(s => x >= s[0] && x <= s[1]);
            if (!over || k.Kind == "keel")
                continue;
            if (k.Kind == "platform")
                plats.Add(((float)k.Z, $"Platform deck at the magazine's crown, {F1(wl - k.Z)} m under the waterline"));
            else
                decks.Add(((float)k.Z, k.Kind == "inner_bottom" ? "IB" : DeckShortName(k.Id)));
        }

        // the walls: wing and TDS bulkheads, starboard
        var walls = new List<Wall>();
        var armour = new List<ArmourPoly>();
        static float Tm(double mm) => (float)(mm * Stretch / 1000);
        var mags = cells.Where(c => c.Kind == RoomKind.Magazine).ToList();
        var mb = a.MagazineBox;
        foreach (var w in hb.Bulkheads)
        {
            if (w.Y is not { } wy || wy < 0 || w.X0 > x || w.X1 <= x)
                continue;
            string what = w.Kind switch { "tds" => "Torpedo bulkhead", "wing" => "Wing bulkhead", "centreline" => "Centreline bulkhead", _ => "Bulkhead" };
            walls.Add(new Wall((float)wy, (float)w.Base, (float)w.Top, w.Kind, w.PlateMm ?? 6, $"{what} · {F1(w.PlateMm ?? 6)} mm plating, {F1(wy)} m out"));
            if (w.ArmourMm is not { } amm || amm <= 0)
                continue;
            float z0 = (float)(w.ArmourBottom ?? w.Base), z1 = (float)(w.ArmourTop ?? w.Top), y = (float)wy;
            bool box = mb?.SideMm is double bs && Math.Abs(bs - amm) < 0.5 && mags.Count > 0 && mags.Any(m => Math.Abs(m.Y1 - y) < 0.05);
            if (box)
                armour.Add(new ArmourPoly(Kind.Box, 0, amm, [new(y, z0), new(y, z1), new(y + Tm(amm), z1), new(y + Tm(amm), z0)],
                    new Vector2(y + Tm(amm), (z0 + z1) / 2), N0(amm), Label.Side));
            else
                armour.Add(new ArmourPoly(Kind.Inner, 0, amm, [new(y, z0), new(y, z1), new(y - Tm(amm), z1), new(y - Tm(amm), z0)],
                    new Vector2(y - Tm(amm), (z0 + z1) / 2), N0(amm), Label.Side));
        }
        var tdsWalls = walls.Where(w => w.Kind == "tds").ToList();
        (float, float, float)? holding = tdsWalls.Count > 0 ? (tdsWalls.MinBy(w => w.Y).Y, tdsWalls.MinBy(w => w.Y).Z0, tdsWalls.MinBy(w => w.Y).Z1) : null;
        (float, float, float)? wing = null;
        if (mags.Count > 0)
        {
            float my = mags.Max(m => m.Y1);
            var ww = walls.Where(w => w.Kind == "wing" && Math.Abs(w.Y - my) < 0.05).ToList();
            if (ww.Count > 0)
                wing = (my, mags.Min(m => m.Z0), mags.Max(m => m.Z1));
        }
        string innerOn = tdsWalls.Count > 0 ? "tds" : cells.Any(c => c.Kind == RoomKind.Bunker) ? "bunkers" : walls.Any(w => w.Kind == "wing") ? "wing" : "";
        (float, float, float)? ownInner = null;
        if (armour.Any(p => p.Kind == Kind.Inner) && tdsWalls.Count == 0 && mags.Count == 0 && !cells.Any(c => c.Kind == RoomKind.Bunker))
        {
            var p = armour.First(q => q.Kind == Kind.Inner);
            ownInner = (p.Pts[0].X, p.Pts[0].Y, p.Pts[1].Y);
            innerOn = "own";
        }

        // the belt, its zone, an internal belt and its lower belt
        var belt = hb.Armour.Belt;
        (float, float, float, float, bool)? beltHere = null;
        var zones = (hb.Armour.Strakes ?? []).Where(s => s.Kind == "zone" && s.X0 <= x && x <= s.X1).ToList();
        bool inside = belt?.Position == "internal";
        if (belt != null && belt.X0 <= x && x <= belt.X1)
        {
            float bt = (float)belt.Top, bb = (float)belt.Bottom;
            float lean = (float)(belt.InclineDeg ?? 0);
            if (!inside)
            {
                var zone = zones.FirstOrDefault();
                double mm = zone?.ThicknessMm ?? belt.ThicknessMm, low = zone?.BottomMm ?? (belt.BottomMm ?? belt.ThicknessMm) * mm / Math.Max(1, belt.ThicknessMm);
                float tf = (float)(zone?.TaperFrom ?? belt.TaperFrom ?? bb);
                var kind = zone != null ? Kind.BeltZone : Kind.Belt;
                armour.Add(SidePlate(kind, 0, bb, bt, z => z >= tf ? mm : low + (mm - low) * (z - bb) / Math.Max(0.1, tf - bb), Hb, wl, N0(mm)));
            }
            beltHere = (bt, bb, inside ? Hb(bt) - (float)Shipgen.Armour.InternalBeltInsetM : Hb(bt), lean, inside);
        }
        foreach (var p in hb.Armour.Inclined ?? [])
        {
            if (p.X0 > x || p.X1 < x || p.Kind == "slope")
                continue;
            bool lower = p.Id == "Lower belt";
            float hs = Hb(p.SideZ), z0 = (float)p.Z0, z1 = (float)p.Z1;
            float Y(float z) => hs - (float)(p.U0 + (p.U1 - p.U0) * (z - z0) / Math.Max(1e-4, z1 - z0));
            double mm = p.ThicknessMm, low = p.BottomMm ?? mm;
            float tf = (float)(p.TaperFrom ?? z0);
            double Th(float z) => z >= tf ? mm : low + (mm - low) * (z - z0) / Math.Max(0.1, tf - z0);
            var pts = new Vector2[22];
            for (int i = 0; i <= 10; i++)
            {
                float z = z0 + (z1 - z0) * i / 10;
                pts[i] = new Vector2(Y(z), z);
                pts[21 - i] = new Vector2(Y(z) - Tm(Th(z)), z);
            }
            var kind = lower ? Kind.LowerBelt : p.Kind == "zone" ? Kind.BeltZone : Kind.Belt;
            float lz = lower ? (z0 + z1) / 2 : Math.Clamp(wl, z0, z1);
            string text = lower ? $"{N0(mm)}→{N0(low)}" : N0(mm);
            armour.Add(new ArmourPoly(kind, 0, mm, pts, new Vector2(Y(lz) - Tm(Th(lz)), lz), text, Label.Side, Faint: lower));
        }

        // the upper belt and the end belts on the side
        foreach (var s in hb.Armour.Strakes ?? [])
        {
            if (s.X0 > x || s.X1 < x || s.Kind is "zone" or "box")
                continue;
            var kind = s.Kind == "upper" ? Kind.Upper : s.Extent == Extent.Fore ? Kind.EndFore : Kind.EndAft;
            double mm = s.ThicknessMm;
            if (s.Kind == "end" && s.TipMm is { } tip)
            {
                double f = (x - s.X0) / Math.Max(0.1, s.X1 - s.X0);
                mm = s.Extent == Extent.Fore ? s.ThicknessMm + (tip - s.ThicknessMm) * f : tip + (s.ThicknessMm - tip) * f;
            }
            armour.Add(SidePlate(kind, 0, (float)s.Bottom, (float)s.Top, _ => mm, Hb, (float)((s.Bottom + s.Top) / 2), N0(mm)));
        }

        // armour decks: the flat (to the side, or to where the slope starts) and the box decks
        foreach (var k in hb.Armour.Decks ?? [])
        {
            if (k.X0 > x || k.X1 < x || k.Extent == Extent.Steering)
                continue;
            float z = (float)k.Z, t = Tm(k.ThicknessMm);
            if (k.Extent == Extent.Box)
            {
                float hw = (float)((k.W ?? 0) / 2);
                armour.Add(new ArmourPoly(Kind.Box, 0, k.ThicknessMm, [new(0, z), new(hw, z), new(hw, z + t), new(0, z + t)], new Vector2(hw * 0.5f, z + t),
                    N0(k.ThicknessMm), Label.Over));
                continue;
            }
            int row = deckRow(k.Deck, k.Extent);
            bool zone = row >= 0 && a.Decks?.ElementAtOrDefault(row) is { MagazinesMm: { } zm } dd && Math.Abs(zm - k.ThicknessMm) < 0.5 &&
                        Math.Abs((dd.Mm ?? 0) - zm) > 0.5;
            float y1 = k.SlopeReach is { } reach ? Hb(k.SlopeFoot ?? z) - (float)reach : Hb(z);
            if (y1 > 0.05f && k.ThicknessMm > 0)
                armour.Add(new ArmourPoly(zone ? Kind.DeckZone : Kind.Deck, row, k.ThicknessMm, [new(0, z), new(y1, z), new(y1, z - t), new(0, z - t)],
                    new Vector2(y1 * (k.SlopeReach != null ? 0.5f : 0.45f), z - t), N0(k.ThicknessMm), Label.Under));
        }
        // the slopes, from the flat's edge down to the belt's lower edge (a ridge where they meet in a fine end)
        var knees = new List<Knee>();
        foreach (var p in hb.Armour.Inclined ?? [])
        {
            if (p.X0 > x || p.X1 < x || p.Kind != "slope")
                continue;
            float hs = Hb(p.SideZ), zf = (float)p.Z0, zk = (float)p.Z1;
            float yf = hs - (float)p.U0, yk = hs - (float)p.U1;
            if (yk < 0 && yf > 0)
            {
                zk = zf + (zk - zf) * yf / (yf - yk);
                yk = 0;
            }
            if (yf <= 0)
                continue;
            var (deck, ext) = SlopeDeck(p.Id);
            int row = deckRow(deck, ext);
            double deg = Math.Atan2(p.Z1 - p.Z0, Math.Max(1e-3, p.U1 - p.U0)) * 180 / Math.PI;
            float t2 = Tm(p.ThicknessMm) / MathF.Cos((float)(deg * Math.PI / 180));
            armour.Add(new ArmourPoly(Kind.Slope, row, p.ThicknessMm, [new(yk, zk), new(yf, zf), new(yf, zf - t2), new(yk, zk - t2)],
                new Vector2((yk + yf) / 2, (zk + zf) / 2 - t2), N0(p.ThicknessMm), Label.Under));
            knees.Add(new Knee(row, yk, zk, yf, zf, deg));
        }
        // the barbettes the cut passes through, down to their deck
        foreach (var c in hb.Components)
        {
            if (c.Kind != ComponentKind.Barbette || c.X is not { } cx || c.R is not { } r || c.ArmourMm is not { } mm || mm <= 0 || (c.Y ?? 0) < 0)
                continue;
            double dx = x - cx;
            if (Math.Abs(dx) >= r)
                continue;
            float half = (float)Math.Sqrt(r * r - dx * dx), cy = (float)(c.Y ?? 0);
            float b0 = (float)c.Base, b1 = MathF.Min((float)c.Top, top + 1.4f), t = Tm(mm);
            foreach (var y in cy > 0 ? new[] { cy - half, cy + half } : [half])
                armour.Add(new ArmourPoly(Kind.Barbette, 0, mm, [new(y, b0), new(y, b1), new(y - t, b1), new(y - t, b0)], default, "", Label.None));
        }

        return new ProtectionSection(hb.HullForm)
        {
            X = fx, Keel = keel, Top = top, Wl = wl, Ib = ib, Beam = (float)hb.Beam, Side = side.ToArray(), Cells = cells.ToArray(),
            Walls = walls.ToArray(), Decks = decks.ToArray(), Platforms = plats.ToArray(), Armour = armour.ToArray(), Knees = knees.ToArray(),
            Belt = beltHere, Holding = holding, WingWall = wing, OwnInner = ownInner, InnerOn = innerOn, HasCoal = cells.Any(c => c.Kind == RoomKind.Bunker),
        };
    }

    /// <summary>A plate on the side from z0 up to z1, thickness by height, drawn in from the hull's surface.</summary>
    static ArmourPoly SidePlate(Kind kind, int row, float z0, float z1, Func<float, double> mm, Func<double, float> hb, float labelZ, string text)
    {
        var pts = new Vector2[22];
        for (int i = 0; i <= 10; i++)
        {
            float z = z0 + (z1 - z0) * i / 10;
            float y = hb(z);
            pts[i] = new Vector2(y, z);
            pts[21 - i] = new Vector2(y - (float)(mm(z) * Stretch / 1000), z);
        }
        float lz = Math.Clamp(labelZ, z0, z1);
        return new ArmourPoly(kind, row, mm(lz), pts, new Vector2(hb(lz) - (float)(mm(lz) * Stretch / 1000), lz), text, Label.Side);
    }

    /// <summary>"Slopes (third deck, fore) 2" → ("Third deck", "fore").</summary>
    static (string Deck, Extent Extent) SlopeDeck(string id)
    {
        int a = id.IndexOf('('), b = id.IndexOf(')');
        if (a < 0 || b < a)
            return ("", Extent.Citadel);
        var parts = id[(a + 1)..b].Split(", ");
        string deck = parts[0].Length > 0 ? char.ToUpperInvariant(parts[0][0]) + parts[0][1..] : "";
        return (deck, parts.Length > 1 ? SnakeEnum<Extent>.Parse(parts[1]) : Extent.Citadel);
    }

    static string DeckShortName(string id) => id switch
    {
        "Main deck" => "Main",
        "Second deck" => "2nd",
        "Third deck" => "3rd",
        "Fourth deck" => "4th",
        "Fifth deck" => "5th",
        "Sixth deck" => "6th",
        "Seventh deck" => "7th",
        "Raised deck 1" => "F'c'sle",
        _ => id,
    };

    static double Interp(List<StationReport> st, double x, Func<StationReport, double> f)
    {
        if (st.Count == 0)
            return -5;
        if (x <= st[0].X)
            return f(st[0]);
        for (int i = 1; i < st.Count; i++)
            if (x <= st[i].X)
            {
                double t = (x - st[i - 1].X) / Math.Max(1e-6, st[i].X - st[i - 1].X);
                return f(st[i - 1]) + (f(st[i]) - f(st[i - 1])) * t;
            }
        return f(st[^1]);
    }

    // ------------------------------------------------------------------ drawing

    /// <summary>Metres to pixels for one half: <see cref="Side"/> -1 draws to port (left), +1 to starboard; Gap is the
    /// gutter between two different stations (0 for one whole section).</summary>
    public readonly record struct Map(float Cx, float Side, float Gap, float K, float Top, float ZTop)
    {
        public Vector2 P(float y, float z) => new(Cx + Side * (Gap / 2 + y * K), Top + (ZTop - z) * K);
        public Vector2 P(Vector2 yz) => P(yz.X, yz.Y);
        public float Y(float z) => Top + (ZTop - z) * K;
        /// <summary>The pixel back to (y, z), y outboard of the centreline on this half.</summary>
        public Vector2 Inv(Vector2 px) => new(MathF.Max(0, (px.X - Cx) * Side - Gap / 2) / K, ZTop - (px.Y - Top) / K);
    }

    static readonly Vector4 VoidFill = Rgb(0x000000, 0.2f);

    static readonly Dictionary<RoomKind, Vector4> Fill = new()
    {
        [RoomKind.Magazine] = Rgb(0xB24E36, 0.62f), [RoomKind.Handling] = Rgb(0xCC6F43, 0.24f), [RoomKind.Stores] = Rgb(0xD7C79A, 0.16f),
        [RoomKind.Tds] = Rgb(0x4FA3B8, 0.30f), [RoomKind.Bunker] = Rgb(0x05070A, 0.62f), [RoomKind.EngineRoom] = Rgb(0x6F92B5, 0.34f),
        [RoomKind.BoilerRoom] = Rgb(0x6F92B5, 0.34f), [RoomKind.DoubleBottom] = Rgb(0x2E4A66, 0), [RoomKind.Accommodation] = Rgb(0x9CC2E2, 0.04f),
        [RoomKind.Steering] = Rgb(0x7F8B95, 0.2f), [RoomKind.TorpedoRoom] = Rgb(0x8E9AA4, 0.22f), [RoomKind.FuelTank] = Rgb(0x5B4A2A, 0.4f),
        [RoomKind.TransmittingStation] = Rgb(0x8E9AA4, 0.3f),
    };

    /// <summary>The legend's swatches: (kind, label).</summary>
    public static readonly (RoomKind Kind, string Label)[] Legend =
    [
        (RoomKind.Magazine, "magazine"), (RoomKind.Handling, "handling"), (RoomKind.Stores, "stores · wing"), (RoomKind.Tds, "TDS"),
        (RoomKind.EngineRoom, "machinery"), (RoomKind.Bunker, "coal"),
    ];

    public static Vector4 FillOf(RoomKind? kind) => kind is { } k && Fill.TryGetValue(k, out var f) ? f : VoidFill;

    public static Vector4 Rgb(int rgb, float a) => new(((rgb >> 16) & 255) / 255f, ((rgb >> 8) & 255) / 255f, (rgb & 255) / 255f, a);

    static readonly Vector4 HullFill = new(0.10f, 0.176f, 0.267f, 1), HullLine = new(0.435f, 0.576f, 0.71f, 1);
    static readonly Vector4 LabelInk = Rgb(0xF3E3A1, 1), LabelHalo = Rgb(0x14243A, 0.9f);

    /// <summary>The half drawn: the hull, its cells and walls, the decks, the armour (lit: selected or hovered plates in
    /// brass), the hull's outline and the waterline. Clipped to the hull by drawing the cells inside it.</summary>
    public void Draw(ImDrawListPtr dl, Map m, Func<ArmourPoly, bool> lit, Func<ArmourPoly, bool> dim, ImFontPtr font, ImFontPtr small)
    {
        // the hull: strips from the centreline out to the side
        for (int i = 0; i + 1 < Side.Length; i++)
        {
            var (a, b) = (Side[i], Side[i + 1]);
            dl.AddQuadFilled(m.P(0, a.Y), m.P(a), m.P(b), m.P(0, b.Y), T.U32(HullFill));
        }
        // the cells, clipped to the hull's side strip by strip
        foreach (var c in Cells)
            DrawCell(dl, m, c, small);
        foreach (var (z, _) in Decks)
            dl.AddLine(m.P(0, z), m.P(HalfB(z), z), T.U32(new Vector4(0.61f, 0.76f, 0.886f, 0.32f)));
        foreach (var (z, _) in Platforms)
            dl.AddLine(m.P(0, z), m.P(HalfB(z), z), T.U32(Rgb(0xD7C79A, 1)), 1.4f);
        foreach (var w in Walls)
        {
            bool tds = w.Kind == "tds";
            float th = MathF.Max(1, (float)(w.PlateMm * Stretch / 1000) * m.K);
            dl.AddLine(m.P(w.Y, w.Z1), m.P(w.Y, w.Z0), T.U32(tds ? Rgb(0x8FC3D6, 1) : new Vector4(0.78f, 0.86f, 0.925f, 0.75f)), tds ? MathF.Max(1.5f, th) : 1);
        }
        // the armour, lit plates last so their outline sits on top
        for (int pass = 0; pass < 2; pass++)
            foreach (var p in Armour)
            {
                bool on = lit(p);
                if (on != (pass == 1))
                    continue;
                var col = T.Armour(p.Mm);
                if (p.Faint)
                    col = col with { W = 0.75f };
                if (dim(p))
                    col = col with { W = col.W * 0.6f };
                FillPoly(dl, m, p.Pts, T.U32(col));
                OutlinePoly(dl, m, p.Pts, T.U32(on ? new Vector4(1, 0.906f, 0.659f, 0.95f) : new Vector4(0, 0, 0, 0.6f)), on ? 1.6f : 1);
            }
        // the hull's outline on top, and the waterline
        for (int i = 0; i + 1 < Side.Length; i++)
            dl.AddLine(m.P(Side[i]), m.P(Side[i + 1]), T.U32(HullLine));
        dl.AddLine(m.P(0, Keel), m.P(Side[0]), T.U32(HullLine));
        dl.AddLine(m.P(0, Top), m.P(Side[^1]), T.U32(HullLine));
        dl.AddLine(m.P(0, Wl), m.P(HalfB(Wl) + 0.6f, Wl), T.U32(T.Blue));
        // the armour's labels
        foreach (var p in Armour)
        {
            if (p.Place == Label.None || p.Text.Length == 0)
                continue;
            var f = p.Kind == Kind.LowerBelt ? small : font;
            float w = DesignerSceneText.Width(f, p.Text);
            var at = m.P(p.At);
            Vector2 tp = p.Place switch
            {
                Label.Under => new Vector2(at.X - w / 2, at.Y + 1),
                Label.Over => new Vector2(at.X - w / 2, at.Y - f.FontSize - 1),
                _ when p.Kind == Kind.Box => m.Side > 0 ? new Vector2(at.X + 3, at.Y - f.FontSize / 2) : new Vector2(at.X - 3 - w, at.Y - f.FontSize / 2),
                _ => m.Side > 0 ? new Vector2(at.X - 3 - w, at.Y - f.FontSize / 2) : new Vector2(at.X + 3, at.Y - f.FontSize / 2),
            };
            DesignerSceneText.Halo(dl, f, tp, LabelInk, LabelHalo, p.Text);
        }
    }

    void DrawCell(ImDrawListPtr dl, Map m, CellBox c, ImFontPtr small)
    {
        var fill = FillOf(c.Kind);
        uint edge = T.U32(new Vector4(0.61f, 0.76f, 0.886f, 0.16f));
        // band by band up the cell, each band clipped to the hull's half-breadth
        const int n = 6;
        for (int i = 0; i < n; i++)
        {
            float za = c.Z0 + (c.Z1 - c.Z0) * i / n, zb = c.Z0 + (c.Z1 - c.Z0) * (i + 1) / n;
            float ya = MathF.Min(c.Y1, HalfB(za)), yb = MathF.Min(c.Y1, HalfB(zb));
            if (ya <= c.Y0 && yb <= c.Y0)
                continue;
            Vector2 q0 = m.P(c.Y0, za), q1 = m.P(MathF.Max(c.Y0, ya), za), q2 = m.P(MathF.Max(c.Y0, yb), zb), q3 = m.P(c.Y0, zb);
            if (fill.W > 0)
                dl.AddQuadFilled(q0, q1, q2, q3, T.U32(fill));
            if (c.Kind is RoomKind.Tds or RoomKind.DoubleBottom or RoomKind.Bunker)
                Hatch(dl, Vector2.Min(Vector2.Min(q0, q1), Vector2.Min(q2, q3)), Vector2.Max(Vector2.Max(q0, q1), Vector2.Max(q2, q3)), c.Kind);
        }
        // its outline, kept inside the hull: the top and floor to the side, the inner wall, the outer one where it stands
        float yTop = MathF.Max(c.Y0, MathF.Min(c.Y1, HalfB(c.Z1))), yFloor = MathF.Max(c.Y0, MathF.Min(c.Y1, HalfB(c.Z0)));
        dl.AddLine(m.P(c.Y0, c.Z1), m.P(yTop, c.Z1), edge);
        dl.AddLine(m.P(c.Y0, c.Z0), m.P(yFloor, c.Z0), edge);
        if (c.Y0 > 0.01f)
            dl.AddLine(m.P(c.Y0, c.Z0), m.P(c.Y0, c.Z1), edge);
        if (c.Y1 < HalfB(c.Z0) && c.Y1 < HalfB(c.Z1))
            dl.AddLine(m.P(c.Y1, c.Z0), m.P(c.Y1, c.Z1), edge);
        var a = m.P(c.Y0, c.Z1);
        var b = m.P(MathF.Min(c.Y1, HalfB((c.Z0 + c.Z1) / 2)), c.Z0);
        var lo = Vector2.Min(a, b);
        var hi = Vector2.Max(a, b);
        if (c.Text.Length > 0 && hi.X - lo.X > DesignerSceneText.Width(small, c.Text) + 4 && hi.Y - lo.Y > small.FontSize)
            DesignerSceneText.Draw(dl, small, (lo + hi) / 2 - new Vector2(DesignerSceneText.Width(small, c.Text) / 2, small.FontSize / 2),
                c.Kind == RoomKind.Magazine ? Rgb(0xFFE3D6, 0.9f) : Rgb(0xB9CBDB, 0.9f), c.Text);
    }

    static void Hatch(ImDrawListPtr dl, Vector2 lo, Vector2 hi, RoomKind? kind)
    {
        dl.PushClipRect(lo, hi, true);
        float h = hi.Y - lo.Y;
        if (kind == RoomKind.Bunker)
        {
            for (float y = lo.Y + 1; y < hi.Y; y += 4)
                for (float x = lo.X + 1; x < hi.X; x += 4)
                    dl.AddCircleFilled(new Vector2(x, y), 0.7f, T.U32(new Vector4(0.61f, 0.76f, 0.886f, 0.25f)));
        }
        else
        {
            uint col = T.U32(kind == RoomKind.Tds ? Rgb(0x4FA3B8, 0.55f) : new Vector4(0.61f, 0.76f, 0.886f, 0.35f));
            float step = kind == RoomKind.Tds ? 4 : 5, dir = kind == RoomKind.Tds ? -1 : 1;
            for (float x = lo.X - h; x < hi.X + h; x += step)
                dl.AddLine(new Vector2(x, hi.Y), new Vector2(x + dir * h, lo.Y), col);
        }
        dl.PopClipRect();
    }

    static void FillPoly(ImDrawListPtr dl, Map m, Vector2[] pts, uint col)
    {
        // the polygons are strips: point i and point n-1-i face each other across the plate
        int n = pts.Length;
        for (int i = 0; i + 1 < n / 2; i++)
            dl.AddQuadFilled(m.P(pts[i]), m.P(pts[i + 1]), m.P(pts[n - 2 - i]), m.P(pts[n - 1 - i]), col);
    }

    static void OutlinePoly(ImDrawListPtr dl, Map m, Vector2[] pts, uint col, float th)
    {
        for (int i = 0; i < pts.Length; i++)
            dl.AddLine(m.P(pts[i]), m.P(pts[(i + 1) % pts.Length]), col, th);
    }

    // ------------------------------------------------------------------ hit testing

    /// <summary>The armour plate under the pixel (topmost last), the plate's hit area grown to at least
    /// <paramref name="minPx"/> pixels each way so a thin plate can be picked.</summary>
    public ArmourPoly? HitArmour(Map m, Vector2 px, float minPx)
    {
        var p = m.Inv(px);
        float tol = minPx / m.K;
        for (int i = Armour.Length - 1; i >= 0; i--)
            if (Near(Armour[i].Pts, p, tol))
                return Armour[i];
        return null;
    }

    /// <summary>The cell under the pixel.</summary>
    public CellBox? HitCell(Map m, Vector2 px)
    {
        var p = m.Inv(px);
        if (p.X > HalfB(p.Y))
            return null;
        foreach (var c in Cells)
            if (p.X >= c.Y0 && p.X <= c.Y1 && p.Y >= c.Z0 && p.Y <= c.Z1)
                return c;
        return null;
    }

    /// <summary>The wall under the pixel, within a few pixels.</summary>
    public Wall? HitWall(Map m, Vector2 px, float tolPx)
    {
        var p = m.Inv(px);
        foreach (var w in Walls)
            if (MathF.Abs(p.X - w.Y) * m.K <= tolPx && p.Y >= w.Z0 && p.Y <= w.Z1)
                return w;
        return null;
    }

    static bool Near(Vector2[] poly, Vector2 p, float tol)
    {
        bool inside = false;
        for (int i = 0, j = poly.Length - 1; i < poly.Length; j = i++)
        {
            var (a, b) = (poly[i], poly[j]);
            if ((a.Y > p.Y) != (b.Y > p.Y) && p.X < (b.X - a.X) * (p.Y - a.Y) / (b.Y - a.Y) + a.X)
                inside = !inside;
        }
        if (inside)
            return true;
        for (int i = 0, j = poly.Length - 1; i < poly.Length; j = i++)
            if (SegDist(p, poly[j], poly[i]) <= tol)
                return true;
        return false;
    }

    static float SegDist(Vector2 p, Vector2 a, Vector2 b)
    {
        var ab = b - a;
        float t = ab.LengthSquared() > 0 ? Math.Clamp(Vector2.Dot(p - a, ab) / ab.LengthSquared(), 0, 1) : 0;
        return Vector2.Distance(p, a + t * ab);
    }
}

/// <summary>Text on a draw list, for the designer's drawings outside the scene (the section).</summary>
static class DesignerSceneText
{
    public static void Draw(ImDrawListPtr dl, ImFontPtr font, Vector2 at, Vector4 colour, string text) =>
        dl.AddText(font, font.FontSize, at, T.U32(colour), text);

    public static float Width(ImFontPtr font, string text)
    {
        ImGui.PushFont(font);
        float w = ImGui.CalcTextSize(text).X;
        ImGui.PopFont();
        return w;
    }

    /// <summary>Text with a dark halo, legible over plates and cells.</summary>
    public static void Halo(ImDrawListPtr dl, ImFontPtr font, Vector2 at, Vector4 ink, Vector4 halo, string text)
    {
        uint h = T.U32(halo);
        for (int dx = -1; dx <= 1; dx++)
            for (int dy = -1; dy <= 1; dy++)
                if (dx != 0 || dy != 0)
                    dl.AddText(font, font.FontSize, at + new Vector2(dx, dy), h, text);
        dl.AddText(font, font.FontSize, at, T.U32(ink), text);
    }
}
