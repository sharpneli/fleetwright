using System.Diagnostics;
using Fleetwright.Sim;
using Fleetwright.Sim.Core;
using ImGuiNET;

namespace Fleetwright.Map;

/// <summary>The map symbols' motion (docs/map-ui/symbols.md Y7): short eases on the display's clock, never the sim's
/// (the same at any compression), none of them changing what the picture says: the selection's ring settles in, the
/// ship or contact under the cursor is ringed, an encounter's ring fades in when it opens and out when it closes, and
/// what asks for the player's attention pulses slowly. <see cref="SymbolLook.Motion"/> 0 stills them all (captures).</summary>
public sealed partial class MapScene
{
    static double RealS => Stopwatch.GetTimestamp() / (double)Stopwatch.Frequency;

    int easedSelected = -1, hovered = -1;
    double selectedAtS = -1e9, hoveredAtS = -1e9;

    struct EncounterFade
    {
        public Vec3d Centre;
        public double R, BornS, GoneS;
        public bool Own;
        public long Seen;
    }

    readonly Dictionary<int, EncounterFade> encounterFades = [];
    readonly List<int> encountersGone = [];
    const double EncounterFadeS = 0.4;

    /// <summary>0 to 1 over <paramref name="seconds"/> since a start, eased out (cubic); 1 with motion off.</summary>
    static float Ease(double since, double seconds)
    {
        if (SymbolLook.Motion <= 0)
            return 1;
        double t = Math.Clamp(since / seconds, 0, 1);
        return (float)(1 - (1 - t) * (1 - t) * (1 - t));
    }

    /// <summary>A slow pulse for what asks for the player's attention (an asked group's "!"): 0.55 to 1, once a second.</summary>
    static float Pulse => SymbolLook.Motion <= 0 ? 1 : (float)(0.775 + 0.225 * Math.Cos(RealS * Math.PI * 2));

    /// <summary>The selection's ring as it settles in: from half again its size, fading in, over 0.18 s.</summary>
    (double Scale, float Alpha) SelectionEase()
    {
        if (selected != easedSelected)
            (easedSelected, selectedAtS) = (selected, RealS);
        float e = Ease(RealS - selectedAtS, 0.18);
        return (1 + 0.5 * (1 - e), e);
    }

    /// <summary>A faint ring round the ship or contact under the cursor (not the selection), easing in.</summary>
    void DrawHover(in MapView view, Snapshot s)
    {
        int hit = -1;
        Vec3d at = default;
        double r = 0;
        var tint = MapPalette.Unit;
        if (mouseIn && !dragging && !placeDrag && !ImGui.GetIO().WantCaptureMouse)
        {
            if ((hit = PickShip(view, cursorX, cursorY)) >= 0 && s.Ships.IndexOf(hit) is int k and >= 0)
            {
                at = s.Ships.PoseAt(k, world.DrawMs).Position;
                r = IconLengthAt(view, s.Ships.Class[k], at) / 2 + 5 * Rp;
            }
            else if ((hit = PickShip(view, cursorX, cursorY, s.Sighted)) >= 0 && s.Sighted.IndexOf(hit) is int j and >= 0)
            {
                at = s.Sighted.PoseAt(j, world.DrawMs).Position;
                (r, tint) = (IconLengthAt(view, s.Sighted.Class[j], at) / 2 + 5 * Rp, ShipDrawList.EnemyTint);
            }
            else if ((hit = PickContact(view, s, cursorX, cursorY)) >= 0 && ContactIndex(s.Contacts, hit) is int i and >= 0)
                (at, r, tint) = (s.Contacts.Position[i], (ContactMarkerPx + 4) * Rp, ShipDrawList.EnemyTint);
            else
                hit = -1;
        }
        if (hit != hovered)
            (hovered, hoveredAtS) = (hit, RealS);
        if (hit < 0 || hit == selected)
            return;
        float e = Ease(RealS - hoveredAtS, 0.12);
        Ring(view, at, r * (1 + 0.25 * (1 - e)), tint with { W = 0.55f * e }, 1.2f);
    }

    /// <summary>The encounters' rings, each fading in as it opens and out where it was as it closes.</summary>
    void DrawEncounters(in MapView view, Snapshot s)
    {
        var v = s.Encounters;
        double now = RealS;
        for (int i = 0; i < v.Count; i++)
        {
            if (!Ring(s, i, out var centre, out double r))
                continue;
            ref var f = ref System.Runtime.InteropServices.CollectionsMarshal.GetValueRefOrAddDefault(encounterFades, v.Id[i], out bool known);
            if (!known || f.GoneS > 0)
                (f.BornS, f.GoneS) = (known ? now - EncounterFadeS * (1 - FadeOut(f, now)) : now, 0);
            (f.Centre, f.R, f.Own, f.Seen) = (centre, r, v.Own[i], frame);
        }
        encountersGone.Clear();
        foreach (var (id, f) in encounterFades)
        {
            float a;
            if (f.Seen == frame)
                a = Ease(now - f.BornS, EncounterFadeS);
            else
            {
                double gone = f.GoneS > 0 ? f.GoneS : now;
                if (f.GoneS <= 0)
                    System.Runtime.InteropServices.CollectionsMarshal.GetValueRefOrNullRef(encounterFades, id).GoneS = now;   // no new key: safe mid-loop
                a = 1 - Ease(now - gone, EncounterFadeS);
                if (a <= 0)
                {
                    encountersGone.Add(id);
                    continue;
                }
            }
            var col = f.Own ? MapPalette.OwnEncounter : MapPalette.OthersEncounter;
            RingM(view, f.Centre, f.R, col with { W = col.W * a }, f.Own ? 1.6f : 1.2f, Dash.Solid, true);
        }
        foreach (int id in encountersGone)
            encounterFades.Remove(id);
    }

    /// <summary>How much of a closing ring's fade-out is left (1: none of it gone), so one that opens again picks up.</summary>
    static float FadeOut(in EncounterFade f, double now) => f.GoneS > 0 ? 1 - Ease(now - f.GoneS, EncounterFadeS) : 1;
}
