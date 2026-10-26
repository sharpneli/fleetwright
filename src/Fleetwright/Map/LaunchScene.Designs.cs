using System.Collections.Concurrent;
using System.Globalization;
using System.Numerics;
using System.Text.Json;
using System.Text.RegularExpressions;
using Fleetwright.Shipgen;
using Fleetwright.Sim.Combat;
using Fleetwright.Sim.Ships;
using ImGuiNET;
using static Fleetwright.Ui.Pieces;
using T = Fleetwright.Ui.Theme;

namespace Fleetwright.Map;

/// <summary>The launch screen's designs (docs/map-ui/launch.md L3): every design a scenario can name, found as
/// <see cref="Sim.Missions.ScenarioLoader"/> finds them (the scenario's own Designs/, the shipped, the player's: the
/// first of a name wins), read for their names, types and years on a thread; each class's figures (displacement, speed,
/// guns, belt, the main guns' reach: <see cref="MatchupProfile"/>, the matchup's own view of her) worked out on a
/// thread once it's compiled; and a class drawn small, to scale, from its art (hull and turrets) or, until that's in,
/// as an outline of her length and beam.</summary>
public sealed unsafe partial class LaunchScene
{
    /// <summary>A design file, as the picker lists it.</summary>
    sealed class DesignInfo
    {
        public required string Stem, Path;
        public string Title = "", Sub = "", Type = "";
        public int Year;   // the latest year its file names (its tech's, its shells'): the picker's filter, 0 if none
        public bool Yours;
        public DateTime Modified;
        public Design? Design;   // read when its class is first asked for
    }

    readonly ShipArtLibrary art;

    // the folders read, by path: the shipped on a thread at the start, the player's again whenever the screen is entered
    readonly ConcurrentDictionary<string, DesignInfo[]> folders = new(StringComparer.OrdinalIgnoreCase);
    Task? folderScan;

    static string ShippedDesigns => WorldSession.DesignFolders[0];

    void ScanShipped()
    {
        var look = new List<string> { ShippedDesigns, Path.Combine(ShippedFolder, "Designs") };
        folderScan = Task.Run(() =>
        {
            foreach (var f in look)
                folders[f] = Scan(f, yours: false);
        });
        RescanYours();
    }

    void RescanYours() => folders[Designer.DesignDoc.UserFolder] = Scan(Designer.DesignDoc.UserFolder, yours: true);

    static readonly Regex YearRx = new(@"(?<![0-9])(18[5-9][0-9]|19[0-9][0-9]|20[0-4][0-9])(?![0-9])", RegexOptions.Compiled);

    static DesignInfo[] Scan(string folder, bool yours)
    {
        if (!Directory.Exists(folder))
            return [];
        var list = new List<DesignInfo>();
        foreach (var path in Directory.GetFiles(folder, "*.json").Order(StringComparer.Ordinal))
        {
            try
            {
                string text = File.ReadAllText(path);
                using var doc = JsonDocument.Parse(text, new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true });
                var r = doc.RootElement;
                if (r.ValueKind != JsonValueKind.Object || r.TryGetProperty("scenario", out _))
                    continue;
                string name = r.TryGetProperty("name", out var n) && n.ValueKind == JsonValueKind.String ? n.GetString()! : Path.GetFileNameWithoutExtension(path);
                var d = new DesignInfo
                {
                    Stem = Path.GetFileNameWithoutExtension(path), Path = path, Yours = yours, Modified = File.GetLastWriteTime(path),
                    Type = r.TryGetProperty("type", out var t) && t.ValueKind == JsonValueKind.String ? t.GetString()! : "",
                };
                foreach (Match m in YearRx.Matches(text))
                    d.Year = Math.Max(d.Year, int.Parse(m.Value, CultureInfo.InvariantCulture));
                // "Borodino-like (Knyaz Suvorov, Tsushima 1905)": the name, and what's in its brackets under it; the
                // player's own as they named it, with when it was saved
                int open = name.IndexOf(" (", StringComparison.Ordinal);
                if (!yours && open > 0 && name.EndsWith(')'))
                    (d.Title, d.Sub) = (name[..open], name[(open + 2)..^1]);
                else
                    d.Title = name;
                if (yours)
                    d.Sub = "saved " + (d.Modified.Date == DateTime.Today ? "today " + d.Modified.ToString("HH:mm", CultureInfo.InvariantCulture)
                        : d.Modified.ToString("d MMM yyyy", CultureInfo.InvariantCulture));
                list.Add(d);
            }
            catch (Exception x) when (x is IOException or JsonException or UnauthorizedAccessException)
            {
            }
        }
        return [.. list];
    }

    /// <summary>The folders a scenario finds its designs in, in order: its own Designs/, the shipped, the player's.</summary>
    string[] LookIn() => [.. (written?.Folder is { } f ? [Path.Combine(f, "Designs")] : Array.Empty<string>()), .. WorldSession.DesignFolders];

    /// <summary>Every design the pick can name, the first of each name (as a scenario finds it); empty until the shipped
    /// are read.</summary>
    List<DesignInfo> Designs()
    {
        var list = new List<DesignInfo>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var f in LookIn())
            if (folders.TryGetValue(f, out var ds))
                foreach (var d in ds)
                    if (seen.Add(d.Stem))
                        list.Add(d);
        return list;
    }

    bool DesignsRead => folderScan is { IsCompleted: true };

    DesignInfo? InfoOf(string stem)
    {
        foreach (var f in LookIn())
            if (folders.TryGetValue(f, out var ds))
                foreach (var d in ds)
                    if (d.Stem.Equals(stem, StringComparison.OrdinalIgnoreCase))
                        return d;
        return null;
    }

    /// <summary>A design's name as the screen shows it: its title, else its file's.</summary>
    string TitleOf(string stem) => InfoOf(stem)?.Title ?? stem;

    // ------------------------------------------------------------------ the figures

    /// <summary>A class as the rows and the picker show it.</summary>
    sealed class Figures
    {
        public required MatchupProfile Profile;
        public double BeltMm;
        public string Main = "", Second = "", Line = "", Displacement = "", Speed = "", Belt = "", Reach = "", Length = "", Tonnes = "", Knots = "";
    }

    readonly Dictionary<int, Figures?> figures = [];   // by class id; null while it's being worked out
    int figuredCount;   // the figures taken in so far: what's made from them is made again when it moves
    readonly ConcurrentQueue<(int Id, Figures? F)> figured = new();
    readonly List<int> toFigure = [];
    Task? figuring;
    readonly Dictionary<BallisticKey, BallisticTable> tables = [];   // the figuring thread's

    /// <summary>A class's figures once it's compiled and they're worked out (on a thread: its guns' range tables), else
    /// null for now.</summary>
    Figures? FiguresOf(int id)
    {
        if (id < 0)
            return null;
        if (figures.TryGetValue(id, out var f))
            return f;
        if (!world.Classes.IsReady(id))
            return null;
        figures[id] = null;
        toFigure.Add(id);
        return null;
    }

    /// <summary>Each frame: the figures worked out taken in, and the next batch started.</summary>
    void Figure()
    {
        while (figured.TryDequeue(out var r))
            if (r.F != null)
            {
                figures[r.Id] = r.F;
                figuredCount++;
            }
        if (figuring is { IsCompleted: false } || toFigure.Count == 0)
            return;
        int[] batch = [.. toFigure];
        toFigure.Clear();
        var settings = world.Settings;
        var classes = world.Classes;
        figuring = Task.Run(() =>
        {
            foreach (int id in batch)
            {
                Figures? f = null;
                try
                {
                    if (classes.TryGet(id) is { } c)
                        f = Make(c, new MatchupProfile(c, k => tables.TryGetValue(k, out var t) ? t : tables[k] = new BallisticTable(k), settings));
                }
                catch (Exception x)
                {
                    Console.Error.WriteLine($"launch: the figures of class {id}: {x.Message}");
                }
                figured.Enqueue((id, f));
            }
        });
    }

    static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    static Figures Make(ShipClass c, MatchupProfile p)
    {
        // the guns by calibre: the chief battery's, then the heaviest other; the tubes if she has them
        var byCal = new List<(int Mm, int Guns, bool Chief)>();
        foreach (ref readonly var b in c.Batteries)
        {
            if (b.Kind is not (Battery.Main or Battery.Secondary) || b.Guns == 0)
                continue;
            int mm = (int)Math.Round(b.CalibreMm), at = byCal.FindIndex(x => x.Mm == mm);
            if (at >= 0)
                byCal[at] = (mm, byCal[at].Guns + b.Guns, byCal[at].Chief || b.Chief);
            else
                byCal.Add((mm, b.Guns, b.Chief));
        }
        byCal.Sort((a, b) => a.Chief != b.Chief ? b.Chief.CompareTo(a.Chief) : b.Mm.CompareTo(a.Mm));
        int tubes = 0;
        foreach (ref readonly var l in c.Launchers)
            tubes += l.Tubes;
        var parts = byCal.Take(2).Select(x => $"{x.Guns} × {x.Mm}").ToList();
        string main = parts.Count > 0 ? parts[0] : "—";
        // the second column: the next calibre's guns, else the tubes
        string second = parts.Count > 1 ? parts[1] : tubes > 0 ? $"{tubes} tube{(tubes == 1 ? "" : "s")}" : "—";
        if (tubes > 0)
            parts.Add($"{tubes} tube{(tubes == 1 ? "" : "s")}");
        // the belt: the thickest side plate at the waterline
        double belt = 0, wl = c.Vertical.Waterline;
        foreach (ref readonly var s in c.SidePlates)
            if (s.Bottom <= wl + 0.1 && s.Top >= wl - 0.1)
                belt = Math.Max(belt, s.ThicknessMm);
        var f = new Figures
        {
            Profile = p, BeltMm = belt, Main = main, Second = second,
            Displacement = p.DisplacementT.ToString("N0", Inv), Speed = p.SpeedKn.ToString("0", Inv), Length = p.LengthM.ToString("0", Inv),
            Belt = belt > 0 ? belt.ToString("0", Inv) : "—", Reach = p.MainReachM > 0 ? (p.MainReachM / 1000).ToString("0.0", Inv) : "—",
        };
        (f.Tonnes, f.Knots) = (f.Displacement + " t", f.Speed + " kn");
        f.Line = $"{f.Displacement} t · {f.Speed} kn · {(parts.Count > 0 ? string.Join(" + ", parts) : "no guns")} · belt {(belt > 0 ? f.Belt + " mm" : "—")} · reach {(p.MainReachM > 0 ? f.Reach + " km" : "—")}";
        return f;
    }

    // ------------------------------------------------------------------ a class drawn small

    /// <summary>A class drawn to scale at <paramref name="pxPerM"/>, bow right, centred on <paramref name="centre"/>:
    /// its art (the hull and its turrets at rest, unlit), else her length and beam outlined; nothing before she's
    /// compiled.</summary>
    void Thumb(int id, Vector2 centre, float pxPerM, float alpha = 1)
    {
        if (id >= 0 && art.Get(id) is { } a)
        {
            var meta = a.Meta;
            float k = pxPerM;
            float cw = (float)a.CanvasX * k, ch = (float)a.CanvasY * k;
            var o = centre - new Vector2(cw, ch) / 2;   // the canvas is centred on the ship's origin
            uint tint = ImGui.ColorConvertFloat4ToU32(new Vector4(0.86f, 0.86f, 0.86f, alpha));
            dl.AddImage(a.Hull, o, o + new Vector2(cw, ch), Vector2.Zero, Vector2.One, tint);
            foreach (int i in a.DrawOrder)
            {
                var m = meta.Mounts[i];
                int t = a.MountType[i];
                if (t < 0)
                    continue;
                var tm = meta.TurretTypes[a.TurretTypes[t]];
                float e = (float)(tm.SizePx[0] / a.Scale) * k;   // the turret sprite's side, as the map's
                float ang = (float)(m.RestDeg * Math.PI / 180), ca = MathF.Cos(ang), sa = MathF.Sin(ang);
                var ax = new Vector2(ca, sa) * e;
                var ay = new Vector2(-sa, ca) * e;
                var p = centre + new Vector2((float)m.PosM[0], (float)m.PosM[1]) * k - ax / 2 - ay / 2;
                dl.AddImageQuad(a.Turrets[t], p, p + ax, p + ax + ay, p + ay, new Vector2(0, 0), new Vector2(1, 0), new Vector2(1, 1), new Vector2(0, 1), tint);
            }
            return;
        }
        if (id < 0 || world.Classes.TryGet(id) is not { } c)
            return;
        // until the art is in: her waterplane, roughly (a fine bow, a full stern)
        float l = (float)c.Motion.LengthM * pxPerM / 2, w = (float)c.Motion.BeamM * pxPerM / 2;
        Span<Vector2> pts = stackalloc Vector2[8];
        pts[0] = centre + new Vector2(l, 0);
        pts[1] = centre + new Vector2(l * 0.45f, w);
        pts[2] = centre + new Vector2(-l * 0.55f, w);
        pts[3] = centre + new Vector2(-l, w * 0.6f);
        pts[4] = centre + new Vector2(-l, -w * 0.6f);
        pts[5] = centre + new Vector2(-l * 0.55f, -w);
        pts[6] = centre + new Vector2(l * 0.45f, -w);
        pts[7] = pts[0];
        dl.AddConvexPolyFilled(ref pts[0], 7, U(T.PlanLine with { W = 0.18f * alpha }));
        dl.AddPolyline(ref pts[0], 8, U(T.PlanLine with { W = 0.6f * alpha }), ImDrawFlags.None, 1);
    }
}
