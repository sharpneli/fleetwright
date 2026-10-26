using System.Numerics;
using System.Runtime.InteropServices;
using Fleetwright.Sim;
using Fleetwright.Sim.Combat;
using Fleetwright.Sim.Core;

namespace Fleetwright.Map;

/// <summary>Who fires at whom, without a web of lines (docs/map-ui/fire-marks.md): pips on a target's ring, one a ship
/// firing on her (friend on an enemy we fire at, hostile red on one of ours under fire); a short bearing tick at each ship's main battery, toward her target, that pulses at a salvo; a faint
/// dotted ring on an enemy in our fight that none of ours fires at. Full lines only on intent: the selection's (every
/// battery, the main brass), the hovered ship's (her main battery's, and the main batteries on her), and every main
/// battery's while Tab is held. Secondary batteries draw nothing else.</summary>
public sealed partial class MapScene
{
    struct FireOn
    {
        public int Friend, FriendFiring, Hostile, HostileFiring;
    }

    readonly Dictionary<int, FireOn> fireOn = [];        // target entity: the ships on her, this frame
    readonly Dictionary<int, int> mainRowOf = [];        // shooter entity: her main battery's row (the lowest battery)
    readonly HashSet<long> firePairs = [];               // a shooter and a target counted, and counted firing
    readonly Dictionary<long, (int Salvos, double At)> salvoSeen = [];   // a battery's salvos, and when it last fired (real s)

    bool tabHeld;            // Tab is down: past a tap, every main battery's line is drawn
    double tabDownAt;        // when it went down, real s
    const double TabTapS = 0.25;   // a release sooner is a tap: the next ship

    bool RevealFire => tabHeld && RealS - tabDownAt >= TabTapS;

    const double SalvoPulseS = 0.5;   // a tick's pulse at a salvo, real s
    const int MaxPips = 5;

    /// <summary>Tab down (true) or up: held, the fire lines; let go sooner than a tap's time, the next ship (shift: the
    /// one before), as Tab ever did.</summary>
    void TabKey(bool down, bool shift)
    {
        if (down)
        {
            if (!tabHeld)
                (tabHeld, tabDownAt) = (true, RealS);
            return;
        }
        if (tabHeld && RealS - tabDownAt < TabTapS)
            NextShip(shift);
        tabHeld = false;
    }

    void DrawTargets(in MapView view, Snapshot s)
    {
        var b = s.Batteries;
        fireOn.Clear();
        if (b.Count == 0)
            return;
        shipIndex.Clear();
        for (int k = 0; k < s.Ships.Count; k++)
            shipIndex[s.Ships.Entity[k]] = k;
        mainRowOf.Clear();
        firePairs.Clear();
        for (int i = 0; i < b.Count; i++)
            if (!mainRowOf.TryGetValue(b.Shooter[i], out int m) || b.Battery[i] < b.Battery[m])
                mainRowOf[b.Shooter[i]] = i;
        long now = world.DrawMs;
        double real = RealS;
        bool reveal = RevealFire;
        for (int i = 0; i < b.Count; i++)
        {
            int shooter = b.Shooter[i], target = b.Target[i];
            long key = (long)shooter * 64 + b.Battery[i];
            ref var seen = ref CollectionsMarshal.GetValueRefOrAddDefault(salvoSeen, key, out bool known);
            if (!known)
                seen = (b.Salvos[i], double.NegativeInfinity);
            else if (seen.Salvos != b.Salvos[i])
                seen = (b.Salvos[i], real);
            if (target == 0 || !shipIndex.TryGetValue(shooter, out int k) || !TargetAt(s, target, now, out var to))
                continue;
            bool ours = s.PlayerSide < 0 ? s.Ships.Side[k] == 0 : s.Ships.Side[k] == s.PlayerSide;
            bool firing = b.State[i] == BatteryState.Firing, main = mainRowOf[shooter] == i, mine = shooter == selected;
            // each ship counts once on her target, and once more if any of her batteries on her is firing
            long pair = (long)shooter << 32 | (uint)target;
            ref var on = ref CollectionsMarshal.GetValueRefOrAddDefault(fireOn, target, out _);
            if (firePairs.Add(pair))
            {
                if (ours)
                    on.Friend++;
                else
                    on.Hostile++;
            }
            if (firing && firePairs.Add(pair | long.MinValue))
            {
                if (ours)
                    on.FriendFiring++;
                else
                    on.HostileFiring++;
            }
            var (from, _) = s.Ships.PoseAt(k, now);
            var side = ours ? MapPalette.FriendFire : MapPalette.HostileFire;
            if (mine)
                FireLine(view, from, to, main ? MapPalette.SelectedFire : MapPalette.SelectedFire with { W = 0.45f }, main ? 1.6f : 1f, firing);
            else if (main && (reveal || hovered > 0 && (shooter == hovered || target == hovered)))
                FireLine(view, from, to, side with { W = 0.4f }, 1f, firing);
            if (main && !mine)
                BearingTick(view, s.Ships.Class[k], from, to, side, firing, real - seen.At);
        }
        foreach (var (target, on) in fireOn)
            Pips(view, s, target, now, on);
        Unengaged(view, s);
    }

    /// <summary>A fire line, fading from the shooter toward the target, fainter while she isn't firing.</summary>
    void FireLine(in MapView view, Vec3d from, Vec3d to, Vector4 colour, float widthPx, bool firing)
    {
        if (!firing)
            colour.W *= 0.4f;
        overlay.BeginStrip(colour, widthPx);
        for (int j = 0; j <= 8; j++)
        {
            var p = (from + (to - from) * (j / 8.0)).Normalized() * Earth.Radius;
            if (view.Project(p, out double x, out double y))
                overlay.Point(x, y, colour with { W = colour.W * (1 - 0.7f * j / 8f) }, widthPx);
            else
                overlay.End();
        }
        overlay.End();
    }

    /// <summary>A short tick off the ship toward her main battery's target, in her side's colour, bright while firing,
    /// longer and brighter for a moment at each salvo.</summary>
    void BearingTick(in MapView view, int cls, Vec3d from, Vec3d to, Vector4 colour, bool firing, double sinceSalvoS)
    {
        if (!view.Project(from, out double x, out double y) || !view.Project(to, out double tx, out double ty))
            return;
        double dx = tx - x, dy = ty - y, len = Math.Sqrt(dx * dx + dy * dy);
        if (len < 1)
            return;
        (dx, dy) = (dx / len, dy / len);
        double pulse = sinceSalvoS < SalvoPulseS ? 1 - sinceSalvoS / SalvoPulseS : 0;
        double r0 = IconLengthAt(view, cls, from) / 2 + 2 * Rp, r1 = r0 + (8 + 5 * pulse) * Rp;
        if (r1 > len)
            return;
        colour.W = firing ? (float)(0.7 + 0.3 * pulse) : 0.3f;
        overlay.BeginStrip(colour, 1.5f + (float)pulse);
        overlay.Point(x + dx * r0, y + dy * r0);
        overlay.Point(x + dx * r1, y + dy * r1);
        overlay.End();
    }

    /// <summary>A target's pips: one a ship firing on her, on an arc over her ring, ours (friend) then theirs (hostile);
    /// at most <see cref="MaxPips"/>. A ship laid on her but silent has no pip: her tick says it, faint.</summary>
    void Pips(in MapView view, Snapshot s, int target, long now, FireOn on)
    {
        if (!TargetAt(s, target, now, out var at) || !view.Project(at, out double x, out double y))
            return;
        double r = shipIndex.TryGetValue(target, out int k) ? IconLengthAt(view, s.Ships.Class[k], at) / 2 + 5 * Rp
            : s.Sighted.IndexOf(target) is int j and >= 0 ? IconLengthAt(view, s.Sighted.Class[j], at) / 2 + 5 * Rp
            : (ContactMarkerPx + 5) * Rp;
        int n = Math.Min(on.FriendFiring + on.HostileFiring, MaxPips);
        double step = 4.2 * Rp / r, a0 = -Math.PI / 2 - step * (n - 1) / 2;
        for (int p = 0; p < n; p++)
        {
            var c = p < on.FriendFiring ? MapPalette.Friend : MapPalette.Hostile;
            double a = a0 + step * p;
            symbols.Add(SymbolShape.Circle, x + Math.Cos(a) * r, y + Math.Sin(a) * r, 1.4 * Rp, c with { W = 0.85f }, c with { W = 0.85f }, 0.5f);
        }
    }

    /// <summary>An enemy in sight inside one of our open encounters that none of ours fires at, nor lays on: a faint
    /// dotted ring, the unengaged ship (Dogger Bank's mistake) said quietly.</summary>
    void Unengaged(in MapView view, Snapshot s)
    {
        var e = s.Encounters;
        var v = s.Sighted;
        long now = world.DrawMs;
        for (int j = 0; j < v.Count; j++)
        {
            if (v.Loss[j] != LossKind.None || fireOn.TryGetValue(v.Entity[j], out var on) && on.Friend > 0)
                continue;
            var (p, _) = v.PoseAt(j, now);
            bool inFight = false;
            for (int i = 0; i < e.Count && !inFight; i++)
                inFight = e.Own[i] && Earth.Distance(e.Centre[i], p) <= e.RadiusM[i] + EncounterRingPadM;   // its farthest ship, as last reckoned
            if (!inFight || !view.Project(p, out double x, out double y))
                continue;
            overlay.Circle(x, y, IconLengthAt(view, v.Class[j], p) / 2 + 8 * Rp, MapPalette.Hostile with { W = 0.35f }, 1f, Dash.Dotted);
        }
    }
}
