using Fleetwright.Shipgen;
using Fleetwright.Sim.Core;
using Fleetwright.Sim.Ships;

namespace Fleetwright.Sim.Combat;

/// <summary>What the sea over her deck and the air at her screws do under way, once she's down by the head (the Åland
/// card, 2026-10-26): pure and allocation-free, from her attitude and her speed through the water.
/// <list type="bullet">
/// <item><b>Her screws</b> drive by the share of their discs under the sea (a disc's area under the local surface: a
/// screw coming out of the water loses its thrust and the engine races, so the power it can turn into way goes with it;
/// ventilation, which costs more near the surface, is left out [EST]).</item>
/// <item><b>Her weather deck under the sea</b> at speed is a flat plate at her trim to the flow: its normal force
/// (Helmbold's low-aspect-ratio lift plus Hoerner's cross-flow, 2·sin²α) presses her down where it's under, by the head
/// when she's trimmed by the head and going ahead (she drives herself under), with <see cref="SimSettings.PloughLiftShare"/>
/// of it near the free surface [EST]; its drag is that force's share along the flow.</item>
/// <item><b>Her upper works under the sea</b> (gunhouses, barbettes, deckhouses, the conning tower, funnels) stand
/// across the water running over the deck: bluff faces at <see cref="SimSettings.UpperWorksCd"/> (Hoerner: a block face
/// on ~1) [EST], their faces no wider together than her beam (the ones behind are in the lee).</item>
/// </list>
/// The forces come back per unit dynamic pressure (m², times ½ρu²), so the caller scales them by its speed.</summary>
public static class Ploughing
{
    /// <summary>The deck's and the upper works' force per unit dynamic pressure (m²): the press down (negative: up), where
    /// it acts along the ship (ship frame x) and the drag.</summary>
    public readonly record struct Forces(double DownM2, double X, double DragM2)
    {
        public static readonly Forces None = new(0, 0, 0);
    }

    /// <summary>Her screws' share of their discs under the sea, the mean over her shaft lines (1 without any).</summary>
    public static double Screws(ShipClass c, in Waterplane water)
    {
        var lines = c.ShaftLines;
        if (lines.Length == 0)
            return 1;
        double sum = 0;
        foreach (ref readonly var row in lines)
        {
            double r = row.PropellerDiameterM / 2;
            if (row.Propeller < 0 || !(r > 0))
            {
                sum += 1;
                continue;
            }
            ref readonly var p = ref c.Components[row.Propeller];
            sum += DiscUnder((water.At(p.X, p.Y) - p.Z) / r);
        }
        return sum / lines.Length;
    }

    /// <summary>A disc's share under a surface u radii over its centre: the circular segment's area over the disc's.</summary>
    public static double DiscUnder(double u)
    {
        u = Math.Clamp(u, -1, 1);
        return (Math.Asin(u) + u * Math.Sqrt(1 - u * u) + Math.PI / 2) / Math.PI;
    }

    /// <summary>The weather deck's and the upper works' forces under the sea at an attitude, going ahead (astern, the
    /// trim's sense to the flow turns over).</summary>
    public static Forces Deck(ShipClass c, FloodModel f, in Attitude a, bool ahead, SimSettings s)
    {
        if (a == Attitude.Upright)
            return Forces.None;
        var water = HitQuery.WaterOf(c, a);
        var cells = c.Cells;
        double area = 0, ax = 0, x0 = double.MaxValue, x1 = double.MinValue;
        for (int i = 0; i < cells.Length; i++)
        {
            if (!f.Weather[i])
                continue;
            var b = cells[i].Box;
            double d = water.At(f.Cx[i], f.Cy[i]) - b.Top;
            if (!(d > 0))
                continue;
            area += f.DeckArea[i];
            ax += f.DeckArea[i] * f.Cx[i];
            (x0, x1) = (Math.Min(x0, b.X0), Math.Max(x1, b.X1));
        }
        if (!(area > 0))
            return Forces.None;
        double alpha = double.DegreesToRadians(a.TrimDeg) * (ahead ? 1 : -1);
        double chord = Math.Max(x1 - x0, 0.1), ar = area / (chord * chord);
        double sin = Math.Sin(alpha), lift = 2 * Math.PI * ar / (2 + Math.Sqrt(ar * ar + 4)) * sin * Math.Cos(alpha);
        double cn = s.PloughLiftShare * (lift + 2 * sin * Math.Abs(sin));
        double down = area * cn * Math.Cos(alpha), drag = area * cn * sin;

        // the upper works' faces under the sea, no wider together than her beam
        double faces = 0, width = 0;
        foreach (ref readonly var p in c.Components)
        {
            if (p.Kind is not (ComponentKind.Main or ComponentKind.Secondary or ComponentKind.Aa or ComponentKind.Torpedo or ComponentKind.Barbette
                or ComponentKind.Superstructure or ComponentKind.ConningTower or ComponentKind.Funnel or ComponentKind.Casing))
                continue;
            var b = p.Bounds;
            double sea = water.At(ahead ? b.X1 : b.X0, 0.5 * (b.Y0 + b.Y1)), under = Math.Min(sea, b.Top) - b.Base;
            if (!(under > 0))
                continue;
            faces += under * (b.Y1 - b.Y0);
            width += b.Y1 - b.Y0;
        }
        double beam = c.Beam;
        if (width > beam)
            faces *= beam / width;
        return new Forces(down, ax / area, drag + s.UpperWorksCd * faces);
    }
}
