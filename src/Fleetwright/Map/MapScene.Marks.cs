using System.Numerics;
using Fleetwright.Designer;
using Fleetwright.Sim;
using Fleetwright.Sim.Combat;
using Fleetwright.Sim.Core;
using Fleetwright.Sim.Ships;
using ImGuiNET;
using Fleetwright.Ui;

namespace Fleetwright.Map;

/// <summary>The map's marks for the battle (docs/map-ui/battle-hud.md U5): the player's ships labelled (name; speed, or
/// why she's falling out or going down; a flotation bar); the selected ship's range to her target on her fire line and
/// her torpedoes' reach as rings; and a ship's tooltip under the cursor (her name or identity as held, what she's doing
/// or what's seen of her, how far). Every label on the map goes through one placer (<see cref="MapLabels"/>,
/// docs/map-ui/symbols.md Y5). Written without garbage (<see cref="U8"/>).</summary>
public sealed partial class MapScene
{
    static readonly Vector4 ShipNameColour = new(0.9f, 0.94f, 0.97f, 1), ShipSubColour = new(0.79f, 0.83f, 0.86f, 0.8f),
        TorpedoLabelColour = new(0.63f, 0.84f, 1f, 0.75f);

    /// <summary>The player's ships' labels, asked of the placer: at her left first, else her right, below, above; the
    /// selected before all others.</summary>
    void ShipLabels(Snapshot s)
    {
        if (camera.MetresPerPixel > 60)
            return;
        var v = s.Ships;
        var view = View;
        var vp = ImGui.GetMainViewport();
        float sc = UiFonts.Scale, sx = vp.Size.X / (float)Math.Max(width, 1), sy = vp.Size.Y / (float)Math.Max(height, 1);
        float line = ImGui.GetTextLineHeight();
        for (int k = 0; k < v.Count; k++)
        {
            if (s.PlayerSide >= 0 && v.Side[k] != s.PlayerSide || !view.Project(v.PoseAt(k, world.DrawMs).Position, out double x, out double y))
                continue;
            int e = v.Entity[k];
            var ns = U8.Size(U8.W($"{ShipName(s, e)}"));
            ImGui.PushFont(UiFonts.SansSmall);
            var ss = U8.Size(ShipSub(s, k, out _));
            ImGui.PopFont();
            float w = Math.Max(Math.Max(ns.X, ss.X), 32 * sc), h = line * 2 + (s.Conditions.IndexOf(e) >= 0 ? 1 + 3 * sc : 0);
            // clear of her icon at any heading, and of the selection's ring round her
            var pose = v.PoseAt(k, world.DrawMs).Position;
            double half = IconLengthAt(view, v.Class[k], pose) / 2 + (e == selected ? 9 * Rp : 0);
            float clear = (float)(half * sx);
            mapLabels.Add(LabelKind.Ship, e, k, e == selected ? 0 : 1, vp.Pos.X + (float)(x * sx), vp.Pos.Y + (float)(y * sy), w, h,
                clear + 4 * sc, e == selected ? clear : 6 * sc, LabelPlace.Left, LabelPlace.Right, LabelPlace.Below, LabelPlace.Above);
        }
    }

    /// <summary>A ship label's second line: why she's falling out or going down, else her speed.</summary>
    ReadOnlySpan<byte> ShipSub(Snapshot s, int k, out bool going)
    {
        var v = s.Ships;
        int ci = s.Conditions.IndexOf(v.Entity[k]);
        going = ci >= 0 && s.Conditions.Status[ci] == ShipStatus.GoingDown;
        return going ? "going down"u8 : v.Reason[k] != 0 ? U8.W($"{ReasonText(v.Reason[k])}") : U8.W($"{v.Speed[k] / Handling.MsPerKnot:0} kn");
    }

    /// <summary>A ship's label where the placer put it: name, the second line, her flotation as a short bar, aligned to
    /// her side (right-aligned at her left, centred above or below).</summary>
    void DrawShipLabel(Snapshot s, in MapLabels.Request r, Vector2 at, LabelPlace place, float alpha)
    {
        int k = r.Index;
        if (k >= s.Ships.Count || s.Ships.Entity[k] != (int)(r.Key & 0xFFFFFFFF))
            return;
        var dl = ImGui.GetBackgroundDrawList();
        float sc = UiFonts.Scale, line = ImGui.GetTextLineHeight();
        int e = s.Ships.Entity[k], ci = s.Conditions.IndexOf(e);
        float w = r.W;
        float Align(float rowW) => place == LabelPlace.Left ? at.X + w - rowW : place == LabelPlace.Right ? at.X : at.X + (w - rowW) / 2;
        var name = U8.W($"{ShipName(s, e)}");
        var sub = ShipSub(s, k, out bool going);
        MapLabels.Text(dl, new Vector2(Align(U8.Size(name).X), at.Y), going ? Bad : ShipNameColour, name, alpha);
        ImGui.PushFont(UiFonts.SansSmall);
        MapLabels.Text(dl, new Vector2(Align(U8.Size(sub).X), at.Y + line), going ? Bad : s.Ships.Reason[k] != 0 ? Theme.Amber : ShipSubColour, sub, alpha);
        ImGui.PopFont();
        if (ci >= 0)
        {
            // her flotation, a short bar under her label
            double water = s.Conditions.WaterT[ci], reserve = s.Conditions.ReserveT[ci];
            float afloat = (float)(reserve / Math.Max(reserve + water, 1e-6)), bw = 32 * sc, bx = Align(bw), by = at.Y + line * 2 + 1;
            dl.AddRectFilled(new Vector2(bx, by), new Vector2(bx + bw, by + 3 * sc), ImGui.GetColorU32(Theme.Inset with { W = Theme.Inset.W * alpha }));
            dl.AddRectFilled(new Vector2(bx, by), new Vector2(bx + bw * afloat, by + 3 * sc),
                ImGui.GetColorU32((afloat > 0.8f ? Ok : afloat > 0.6f ? Theme.Amber : Bad) with { W = alpha }));
        }
    }

    /// <summary>The selected ship's marks over the map, asked of the placer before all else: the range on her main
    /// battery's fire line (at its middle, in its pill), and her torpedoes' reach labelled at the top of each ring,
    /// when the map is close enough to show them.</summary>
    void SelectionMarks(Snapshot s)
    {
        int k = s.Ships.IndexOf(selected);
        if (k < 0)
            return;
        var view = View;
        var vp = ImGui.GetMainViewport();
        float sc = UiFonts.Scale, sx = vp.Size.X / (float)Math.Max(width, 1), sy = vp.Size.Y / (float)Math.Max(height, 1);
        var (p, _) = s.Ships.PoseAt(k, world.DrawMs);
        int b = MainRow(s, selected);
        if (b >= 0 && TargetAt(s, s.Batteries.Target[b], world.DrawMs, out var to))
        {
            var mid = (p + to).Normalized() * Earth.Radius;
            if (view.Project(mid, out double mx, out double my))
            {
                ImGui.PushFont(UiFonts.Mono);
                var size = U8.Size(U8.W($"{Earth.Distance(p, to) / 1000:0.0} km"));
                ImGui.PopFont();
                mapLabels.Add(LabelKind.Range, 0, b, 0, vp.Pos.X + (float)(mx * sx), vp.Pos.Y + (float)(my * sy), size.X + 8 * sc, size.Y + 2 * sc,
                    3 * sc, 0, LabelPlace.Above, LabelPlace.Below);
            }
        }
        if (world.Classes.TryGet(s.Ships.Class[k]) is { Launchers.Length: > 0 } cls && s.Ships.TorpedoesLeft[k] > 0)
        {
            var t = cls.Torpedoes[cls.Launchers[0].Torpedo];
            ImGui.PushFont(UiFonts.SansSmall);
            for (int r = 0; r < 2; r++)
            {
                double reach = r == 0 ? t.FastRangeM : t.SlowRangeM;
                if (reach / camera.MetresPerPixel < 30)
                    continue;
                var top = p;
                var north = Earth.Forward(top, 0);
                Earth.Advance(ref top, ref north, reach);
                if (!view.Project(top, out double tx, out double ty))
                    continue;
                var size = U8.Size(TorpedoText(t, r));
                mapLabels.Add(LabelKind.Torpedo, r, r, 1, vp.Pos.X + (float)(tx * sx), vp.Pos.Y + (float)(ty * sy), size.X, size.Y,
                    2 * sc, 0, LabelPlace.Above, LabelPlace.Below);
            }
            ImGui.PopFont();
        }
    }

    static ReadOnlySpan<byte> TorpedoText(in TorpedoRow t, int r) =>
        U8.W($"torpedoes {(r == 0 ? t.FastKn : t.SlowKn):0} kn · {(r == 0 ? t.FastRangeM : t.SlowRangeM) / 1000:0.0} km");

    void DrawRangeLabel(Snapshot s, in MapLabels.Request r, Vector2 at, float alpha)
    {
        int k = s.Ships.IndexOf(selected), b = r.Index;
        if (k < 0 || b >= s.Batteries.Count || !TargetAt(s, s.Batteries.Target[b], world.DrawMs, out var to))
            return;
        var dl = ImGui.GetBackgroundDrawList();
        float sc = UiFonts.Scale;
        ImGui.PushFont(UiFonts.Mono);
        dl.AddRectFilled(at, at + new Vector2(r.W, r.H), ImGui.GetColorU32(new Vector4(0.067f, 0.055f, 0.039f, 0.82f * alpha)), 2 * sc);
        U8.Draw(dl, at + new Vector2(4, 1) * sc, ImGui.GetColorU32(Theme.Brass with { W = alpha }),
            U8.W($"{Earth.Distance(s.Ships.PoseAt(k, world.DrawMs).Position, to) / 1000:0.0} km"));
        ImGui.PopFont();
    }

    void DrawTorpedoLabel(Snapshot s, in MapLabels.Request r, Vector2 at, float alpha)
    {
        int k = s.Ships.IndexOf(selected);
        if (k < 0 || world.Classes.TryGet(s.Ships.Class[k]) is not { Launchers.Length: > 0 } cls)
            return;
        ImGui.PushFont(UiFonts.SansSmall);
        MapLabels.Text(ImGui.GetBackgroundDrawList(), at, TorpedoLabelColour, TorpedoText(cls.Torpedoes[cls.Launchers[0].Torpedo], r.Index), alpha);
        ImGui.PopFont();
    }

    /// <summary>Every label the placer kept, each drawn by its own kind's way.</summary>
    void DrawLabels(Snapshot s)
    {
        var dl = ImGui.GetBackgroundDrawList();
        for (int i = 0; i < mapLabels.Count; i++)
        {
            var (at, place, alpha) = mapLabels.Result(i);
            if (alpha <= 0)
                continue;
            ref readonly var r = ref mapLabels[i];
            switch (r.Kind)
            {
                case LabelKind.Ship: DrawShipLabel(s, r, at, place, alpha); break;
                case LabelKind.Group: DrawGroupLabel(s, r, at, alpha); break;
                case LabelKind.Contact: DrawContactLabel(s, r, at, alpha); break;
                case LabelKind.Encounter: DrawEncounterLabel(s, r, at, alpha); break;
                case LabelKind.Wreck: DrawWreckLabel(r, at, alpha); break;
                case LabelKind.Range: DrawRangeLabel(s, r, at, alpha); break;
                case LabelKind.Torpedo: DrawTorpedoLabel(s, r, at, alpha); break;
            }
        }
    }

    /// <summary>A ship's tooltip under the cursor (not while dragging or with a pick armed): one of hers, or an enemy as
    /// held, and how far from the player's ship.</summary>
    void HoverTip(Snapshot s)
    {
        if (!mouseIn || ImGui.GetIO().WantCaptureMouse || dragging || boxing || placing || armedPick != ClickPick.None)
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
            U8.Text(U8.W($"{ShipName(s, own)}"), Theme.Bright);
            ImGui.PopFont();
            ImGui.SameLine();
            if (world.Classes.TryGet(s.Ships.Class[k]) is { } cls)
                U8.Text(U8.W($"{cls.Name}"), Theme.Muted);
            U8.Text(U8.W($"{s.Ships.Speed[k] / Handling.MsPerKnot:0} kn · {(ci >= 0 ? s.Conditions.Hits[ci] : 0)} hits{(ci >= 0 && s.Conditions.Fires[ci] > 0 ? " · burning" : "")}"));
            if (s.Ships.Reason[k] != 0)
                U8.Text(U8.W($"{ReasonText(s.Ships.Reason[k])}"), Theme.Amber);
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
