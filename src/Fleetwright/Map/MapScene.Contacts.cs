using System.Numerics;
using Fleetwright.Sim;
using Fleetwright.Sim.Core;
using ImGuiNET;

namespace Fleetwright.Map;

/// <summary>The player's picture on the map (docs/battle/plan.md step 3.2e; docs/map-ui/README.md "Uncertainty on the
/// map"): each contact where their navy reckons it, a marker and its course line, inside its error ellipse (2 sigma),
/// in its side's colour once its type is made out; solid while in sight, fading once it's out of sight and dead reckoned.
/// Its label says what it's taken for: a ship, its type, its class (with a "?": it may be wrong), its name.</summary>
public sealed partial class MapScene
{
    static readonly Vector4 UnknownContact = new(0.95f, 0.9f, 0.7f, 1f);
    const double ContactFadeMs = 30 * 60_000;   // out of sight, it fades to its faintest over this
    const double ContactCourseS = 300;          // the course line: five minutes' steaming
    const float ContactMarkerPx = 5;
    bool[] labelCells = [];

    static Vector4 ContactColour(ContactsView c, int i, long now)
    {
        var col = c.Side[i] == 255 ? UnknownContact : ShipDrawList.SideTints[c.Side[i] < ShipDrawList.SideTints.Length ? c.Side[i] : 0];
        float a = c.InSight[i] ? 0.95f : (float)Math.Max(0.3, 0.85 - 0.55 * (now - c.SeenMs[i]) / ContactFadeMs);
        return col with { W = a };
    }

    void DrawContacts(in MapView view, Snapshot s)
    {
        var c = s.Contacts;
        long now = world.DrawMs;
        for (int i = 0; i < c.Count; i++)
        {
            var p = c.Position[i];
            var col = ContactColour(c, i, now);
            // the ellipse, 2 sigma, at least a few pixels across
            double min = 6 * view.MetresPerPixel;
            double major = Math.Max(2 * c.MajorM[i], min), minor = Math.Max(2 * c.MinorM[i], min);
            var ax = Earth.Forward(p, c.AxisDeg[i]);
            var up = p.Normalized();
            var across = Vec3d.Cross(up, ax);
            overlay.BeginStrip(col with { W = col.W * 0.6f }, 1f);
            for (int k = 0; k <= 32; k++)
            {
                double a = k * Math.PI * 2 / 32;
                Point(view, (p + ax * (Math.Cos(a) * major) + across * (Math.Sin(a) * minor)).Normalized() * Earth.Radius);
            }
            overlay.End();
            if (!view.Project(p, out double x, out double y))
                continue;
            // the marker: a diamond, and the course line ahead
            overlay.BeginStrip(col, 1.5f);
            overlay.Point(x, y - ContactMarkerPx);
            overlay.Point(x + ContactMarkerPx, y);
            overlay.Point(x, y + ContactMarkerPx);
            overlay.Point(x - ContactMarkerPx, y);
            overlay.Point(x, y - ContactMarkerPx);
            overlay.End();
            var ahead = (p + c.Velocity[i] * ContactCourseS).Normalized() * Earth.Radius;
            if (view.Project(ahead, out double ax2, out double ay2) && Math.Abs(ax2 - x) + Math.Abs(ay2 - y) > 2)
            {
                overlay.BeginStrip(col, 1.2f);
                overlay.Point(x, y);
                overlay.Point(ax2, ay2);
                overlay.End();
            }
        }
    }

    /// <summary>What a contact is taken for, as its label says it.</summary>
    string ContactText(ContactsView c, int i, long now)
    {
        string what = c.Level[i] switch
        {
            ContactLevel.Name => world.NameOf(c.Entity[i]) ?? ClassName(c.TakenFor[i]),
            ContactLevel.Class => ClassName(c.TakenFor[i]) + " class?",
            ContactLevel.Type => c.Type[i] switch
            {
                ShipType.Capital => "heavy ship",
                ShipType.Cruiser => "cruiser",
                ShipType.Destroyer => "destroyer",
                ShipType.Small => "small craft",
                ShipType.Merchant => "merchant",
                _ => "ship",
            },
            _ => "ship",
        };
        what += SeenText(c.Seen[i]);
        return c.InSight[i] ? what : $"{what} · last seen {new WorldTime(c.SeenMs[i]).ToUtc():HH:mm}";
    }

    string ClassName(int id) => id >= 0 && world.Classes.TryGet(id) is { } cls ? cls.Name : "?";

    void ContactLabels(Snapshot s)
    {
        var c = s.Contacts;
        if (c.Count == 0)
            return;
        var view = View;
        var vp = ImGui.GetMainViewport();
        float sx = vp.Size.X / (float)Math.Max(width, 1), sy = vp.Size.Y / (float)Math.Max(height, 1);
        var dl = ImGui.GetBackgroundDrawList();
        long now = world.DrawMs;
        // a label where none is yet: a coarse grid of the screen, a cell a label's height (a column of contacts gets one
        // label, its head's, not a pile)
        float cellPx = ImGui.GetTextLineHeight() + 2;
        int gw = (int)(vp.Size.X / cellPx) + 2, gh = (int)(vp.Size.Y / cellPx) + 2;
        if (labelCells.Length < gw * gh)
            labelCells = new bool[gw * gh];
        Array.Clear(labelCells, 0, gw * gh);
        for (int i = 0; i < c.Count; i++)
        {
            if (!view.Project(c.Position[i], out double x, out double y))
                continue;
            int cx = (int)(x * sx / cellPx), cy = (int)(y * sy / cellPx);
            if (cx < 0 || cy < 0 || cx >= gw - 1 || cy >= gh || labelCells[cx + cy * gw] || labelCells[cx + 1 + cy * gw])
                continue;
            labelCells[cx + cy * gw] = labelCells[cx + 1 + cy * gw] = true;
            string text = ContactText(c, i, now);
            var at = new Vector2(vp.Pos.X + (float)(x * sx) + 9, vp.Pos.Y + (float)(y * sy) - 7);
            dl.AddText(at + new Vector2(1, 1), 0xC0000000, text);
            dl.AddText(at, ImGui.GetColorU32(ContactColour(c, i, now)), text);
        }
    }
}
