using System.Numerics;
using Fleetwright.Designer;
using Fleetwright.Sim;
using Fleetwright.Sim.Combat;
using Fleetwright.Sim.Core;
using ImGuiNET;

namespace Fleetwright.Map;

/// <summary>The map's marks for the battle (docs/map-ui/battle-hud.md U5): the player's ships labelled at their left
/// (name; speed, or why she's falling out or going down; a flotation bar), where the screen has room for them; the
/// selected ship's range to her target on her fire line and her torpedoes' reach as rings; and a ship's tooltip under
/// the cursor (her name or identity as held, what she's doing or what's seen of her, how far). Written without garbage
/// (<see cref="U8"/>).</summary>
public sealed partial class MapScene
{
    bool[] shipLabelCells = [];

    /// <summary>The player's ships' labels, at their left: one a screen cell (a column's head gets it, not a pile).</summary>
    void ShipLabels(Snapshot s)
    {
        if (camera.MetresPerPixel > 60)
            return;
        var v = s.Ships;
        var view = View;
        var vp = ImGui.GetMainViewport();
        float sc = UiFonts.Scale, sx = vp.Size.X / (float)Math.Max(width, 1), sy = vp.Size.Y / (float)Math.Max(height, 1);
        float line = ImGui.GetTextLineHeight(), cell = line * 2 + 4;
        int gw = (int)(vp.Size.X / cell) + 2, gh = (int)(vp.Size.Y / cell) + 2;
        if (shipLabelCells.Length < gw * gh)
            shipLabelCells = new bool[gw * gh];
        Array.Clear(shipLabelCells, 0, gw * gh);
        var dl = ImGui.GetBackgroundDrawList();
        for (int k = 0; k < v.Count; k++)
        {
            if (s.PlayerSide >= 0 && v.Side[k] != s.PlayerSide || !view.Project(v.PoseAt(k, world.DrawMs).Position, out double x, out double y))
                continue;
            int cx = (int)(x * sx / cell), cy = (int)(y * sy / cell);
            if (cx < 1 || cy < 0 || cx >= gw || cy >= gh || shipLabelCells[cx + cy * gw] || shipLabelCells[cx - 1 + cy * gw])
                continue;
            shipLabelCells[cx + cy * gw] = shipLabelCells[cx - 1 + cy * gw] = true;
            int e = v.Entity[k], ci = s.Conditions.IndexOf(e);
            bool going = ci >= 0 && s.Conditions.Status[ci] == ShipStatus.GoingDown;
            var name = U8.W($"{ShipName(s, e)}");
            var sub = going ? "going down"u8 : v.Reason[k] != 0 ? U8.W($"{ReasonText(v.Reason[k])}") : U8.W($"{v.Speed[k] / Handling.MsPerKnot:0} kn");
            float right = vp.Pos.X + (float)(x * sx) - 12 * sc, top = vp.Pos.Y + (float)(y * sy) - line;
            var ns = U8.Size(name);
            ImGui.PushFont(UiFonts.SansSmall);
            var ss = U8.Size(sub);
            ImGui.PopFont();
            Shadowed(dl, new Vector2(right - ns.X, top), going ? Bad : new Vector4(0.9f, 0.94f, 0.97f, 1), name);
            ImGui.PushFont(UiFonts.SansSmall);
            Shadowed(dl, new Vector2(right - ss.X, top + line), going ? Bad : v.Reason[k] != 0 ? DesignerTheme.Amber : new Vector4(0.79f, 0.83f, 0.86f, 0.8f), sub);
            ImGui.PopFont();
            if (ci >= 0)
            {
                // her flotation, a short bar under her label
                double water = s.Conditions.WaterT[ci], reserve = s.Conditions.ReserveT[ci];
                float afloat = (float)(reserve / Math.Max(reserve + water, 1e-6)), bw = 32 * sc, by = top + line * 2 + 1;
                dl.AddRectFilled(new Vector2(right - bw, by), new Vector2(right, by + 3 * sc), ImGui.GetColorU32(DesignerTheme.Inset));
                dl.AddRectFilled(new Vector2(right - bw, by), new Vector2(right - bw + bw * afloat, by + 3 * sc),
                    ImGui.GetColorU32(afloat > 0.8f ? Ok : afloat > 0.6f ? DesignerTheme.Amber : Bad));
            }
        }
    }

    static void Shadowed(ImDrawListPtr dl, Vector2 at, Vector4 colour, ReadOnlySpan<byte> text)
    {
        U8.Draw(dl, at + new Vector2(1, 1), 0xC0000000, text);
        U8.Draw(dl, at, ImGui.GetColorU32(colour), text);
    }

    /// <summary>The selected ship's marks over the map: the range on her main battery's fire line (at its middle), and
    /// her torpedoes' reach (fast and slow settings) as dashed rings, when the map is close enough to show them.</summary>
    void SelectionMarks(Snapshot s)
    {
        int k = s.Ships.IndexOf(selected);
        if (k < 0)
            return;
        var view = View;
        var vp = ImGui.GetMainViewport();
        float sc = UiFonts.Scale, sx = vp.Size.X / (float)Math.Max(width, 1), sy = vp.Size.Y / (float)Math.Max(height, 1);
        var dl = ImGui.GetBackgroundDrawList();
        var (p, _) = s.Ships.PoseAt(k, world.DrawMs);
        int b = MainRow(s, selected);
        if (b >= 0 && TargetAt(s, s.Batteries.Target[b], world.DrawMs, out var to))
        {
            var mid = (p + to).Normalized() * Earth.Radius;
            if (view.Project(mid, out double mx, out double my))
            {
                var text = U8.W($"{Earth.Distance(p, to) / 1000:0.0} km");
                ImGui.PushFont(UiFonts.Mono);
                var size = U8.Size(text);
                var at = new Vector2(vp.Pos.X + (float)(mx * sx) - size.X / 2, vp.Pos.Y + (float)(my * sy) - size.Y - 3 * sc);
                dl.AddRectFilled(at - new Vector2(4, 1) * sc, at + size + new Vector2(4, 1) * sc, 0xD0110E0A, 2 * sc);
                U8.Draw(dl, at, ImGui.GetColorU32(DesignerTheme.Brass), text);
                ImGui.PopFont();
            }
        }
        // her torpedoes' reach: labelled at the ring's top
        if (world.Classes.TryGet(s.Ships.Class[k]) is { Launchers.Length: > 0 } cls && s.Ships.TorpedoesLeft[k] > 0)
        {
            var t = cls.Torpedoes[cls.Launchers[0].Torpedo];
            ImGui.PushFont(UiFonts.SansSmall);
            for (int r = 0; r < 2; r++)
            {
                double reach = r == 0 ? t.FastRangeM : t.SlowRangeM, kn = r == 0 ? t.FastKn : t.SlowKn;
                if (reach / camera.MetresPerPixel < 30)
                    continue;
                var top = p;
                var north = Earth.Forward(top, 0);
                Earth.Advance(ref top, ref north, reach);
                if (!view.Project(top, out double tx, out double ty))
                    continue;
                var text = U8.W($"torpedoes {kn:0} kn · {reach / 1000:0.0} km");
                var size = U8.Size(text);
                Shadowed(dl, new Vector2(vp.Pos.X + (float)(tx * sx) - size.X / 2, vp.Pos.Y + (float)(ty * sy) - size.Y - 2), new Vector4(0.63f, 0.84f, 1f, 0.75f), text);
            }
            ImGui.PopFont();
        }
    }

    /// <summary>A ship's tooltip under the cursor (not while dragging or with a pick armed): one of hers, or an enemy as
    /// held, and how far from the player's ship.</summary>
    void HoverTip(Snapshot s)
    {
        if (!mouseIn || ImGui.GetIO().WantCaptureMouse || dragging || placing || armedPick != ClickPick.None)
            return;
        var vp = ImGui.GetMainViewport();
        double x = mouse.X * width / Math.Max(vp.Size.X, 1), y = mouse.Y * height / Math.Max(vp.Size.Y, 1);
        var view = View;
        int own = PickShip(view, x, y);
        int from = s.Ships.IndexOf(lastOwn);
        if (own >= 0 && own != selected)
        {
            int k = s.Ships.IndexOf(own), ci = s.Conditions.IndexOf(own);
            ImGui.BeginTooltip();
            ImGui.PushFont(UiFonts.SansBold);
            U8.Text(U8.W($"{ShipName(s, own)}"), DesignerTheme.Bright);
            ImGui.PopFont();
            ImGui.SameLine();
            if (world.Classes.TryGet(s.Ships.Class[k]) is { } cls)
                U8.Text(U8.W($"{cls.Name}"), DesignerTheme.Muted);
            U8.Text(U8.W($"{s.Ships.Speed[k] / Handling.MsPerKnot:0} kn · {(ci >= 0 ? s.Conditions.Hits[ci] : 0)} hits{(ci >= 0 && s.Conditions.Fires[ci] > 0 ? " · burning" : "")}"));
            if (s.Ships.Reason[k] != 0)
                U8.Text(U8.W($"{ReasonText(s.Ships.Reason[k])}"), DesignerTheme.Amber);
            ImGui.TextDisabled("click: her card · right-click with one of yours: follow her");
            ImGui.EndTooltip();
            return;
        }
        int enemy = PickShip(view, x, y, s.Sighted);
        if (enemy < 0)
            enemy = PickContact(view, s, x, y);
        int i = enemy >= 0 ? ContactIndex(s.Contacts, enemy) : -1;
        if (i < 0 || enemy == selected)
            return;
        var c = s.Contacts;
        ImGui.BeginTooltip();
        ImGui.PushFont(UiFonts.SansBold);
        U8.Text(U8.W($"{Identity(c, i)}"), new Vector4(0.95f, 0.6f, 0.55f, 1));
        ImGui.PopFont();
        if (c.Seen[i] != SeenFlags.None)
            U8.Text(U8.W($"{SeenWords(c.Seen[i])}"), new Vector4(0.94f, 0.7f, 0.66f, 1));
        if (from >= 0)
            U8.Text(U8.W($"{Earth.Distance(s.Ships.PoseAt(from, world.DrawMs).Position, c.Position[i]) / 1000:0.0} km from {ShipName(s, lastOwn)}"));
        ImGui.TextDisabled("click: what we hold of her · right-click with one of yours: engage");
        ImGui.EndTooltip();
    }
}
