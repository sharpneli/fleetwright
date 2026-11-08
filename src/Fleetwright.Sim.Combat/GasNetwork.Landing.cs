using Fleetwright.Shipgen;
using Fleetwright.Sim.Core;
using Fleetwright.Sim.Ships;

namespace Fleetwright.Sim.Combat;

/// <summary>What her gas does lands on her as it does it (docs/battle/workers.md ET3, rule 3): each time the world steps it, the
/// game's; once at its end, a bench's run at once. One place, so the two can't part.</summary>
public sealed partial class GasNetwork
{
    /// <summary>The cells whose felt rose in the landing under way.</summary>
    int[] rose = [];

    /// <summary>What her gas has done since it last landed, on her now: what it tore and blew open, the seams of what held once its
    /// load is over (decision 4), the magazines it lit (wrecked, their men flashed), the gunhouses its way up vented (wrecked and
    /// burning, the men up that train flashed) or threw, the men and gear in each space by the rise in what it felt, and the flame down
    /// a train from a handling room it reached hot (lighting what that reaches). What it came to, as a hit's effects: BreachM2 the new
    /// openings to the sea, Blast's the plate opened and sprung between spaces.</summary>
    public EffectsResult Land(long now, ref RandomStream r)
    {
        var fx = new EffectsResult { Part = -1, MagazineRoom = -1 };
        double hole0 = res.HoleM2, open0 = res.OpenM2, leak0 = res.LeakM2;
        CommitOpened();
        Seams(ref r, end: finished);
        fx.BreachM2 = res.HoleM2 - hole0;
        (fx.Blast.HoleM2, fx.Blast.OpenM2, fx.Blast.LeakM2) = (fx.BreachM2, res.OpenM2 - open0, res.LeakM2 - leak0);
        Casualties.Begin(k, now);
        LitLanded(ref fx);
        TrainsLanded();
        WaysUpLanded(ref fx);
        FeltLanded(ref fx);
        ThrownLanded(now);
        FlashLanded(now, ref fx, ref r);
        Casualties.Draw(c, k, now, s, ref fx, ref r);
        if (fx.Changed || fx.Killed + fx.Wounded > 0)
            k.Version++;
        return fx;
    }

    /// <summary>The magazines lit since: their rooms wrecked, the men in them flashed.</summary>
    void LitLanded(ref EffectsResult fx)
    {
        for (int t = stowsLanded; t < nStows; t++)
        {
            int room = stows[t].Room;
            Wreck(c.RoomNode(room), ref fx);
            foreach (int cell in c.Links(c.Rooms[room].Cells))
                Casualties.Flash(k, Casualties.Cell(c, cell), true, s);
            if (!fx.Magazine)
                (fx.Magazine, fx.MagazineRoom, fx.MagazineVia) = (true, room, MagazineVia.Gas);
        }
        stowsLanded = nStows;
    }

    /// <summary>A magazine fire no longer held in its magazine (a way up vented, her plating gave): the flash through every train of each
    /// magazine alight (16 §4.6), once; one lit after that, as it lights. Held, only the magazine's own men (<see cref="LitLanded"/>).</summary>
    void TrainsLanded()
    {
        if (res.Vented + res.Sides + res.WeatherDecks + res.Bottoms + res.Decks == 0)
            return;
        for (int t = 0; t < nStows; t++)
        {
            var st = stows[t];
            if (st.Trains)
                continue;
            st.Trains = true;
            for (int mt = 0; mt < c.Mounts.Length; mt++)
                if (c.Mounts[mt].Magazine == st.Room)
                    Casualties.TrainOf(c, k, mt, s);
        }
    }

    /// <summary>The mounts whose way up opened since: an enclosed gunhouse and its barbette wrecked and burning; one thrown off its seat
    /// wrecked.</summary>
    void WaysUpLanded(ref EffectsResult fx)
    {
        for (int mt = 0; mt < c.Mounts.Length; mt++)
        {
            ref readonly var mr = ref c.Mounts[mt];
            if (liftedMount[mt] && !liftLanded[mt])
            {
                liftLanded[mt] = true;
                Wreck(mr.Component, ref fx);
                Throw(mr.Component, liftMPa[mt], c.LiftKPa(mt) * 1e-3, NowMs);
            }
            if (!ventedMount[mt] || ventLanded[mt])
                continue;
            ventLanded[mt] = true;
            if (!c.Enclosed(mt))
                continue;
            Wreck(mr.Component, ref fx);
            if (mr.Barbette >= 0)
                Wreck(mr.Barbette, ref fx);
            fx.Fire |= Fires.Ignite(c, k, c.FireOfPart(mr.Component), s.ReadyAmmoSeatMW, s);
        }
    }

    /// <summary>The men and gear in every space whose gas rose since (<see cref="Effects.GasFelt"/>'s rule, by the rise): the men by
    /// the risk the higher peak adds, their rooms' gear and the parts in them by their pressure class, a part behind armour the gas
    /// can't burst keeping it out.</summary>
    void FeltLanded(ref EffectsResult fx)
    {
        int nr = 0;
        for (int a = 0; a < n; a++)
        {
            int i = region[a];
            double f = felt[i];
            if (i == partNode || !(f > feltLanded[i]))
                continue;
            Casualties.GasRise(k, Casualties.Cell(c, i), feltLanded[i], f, s);
            foreach (int room in c.RoomsIn(i))
                Effects.Pressed(c, k, c.RoomNode(room), s.RoomBlastMPa[Math.Min((int)c.Rooms[room].Kind, s.RoomBlastMPa.Length - 1)], f, s, ref fx);
            feltLanded[i] = f;
            rose[nr++] = i;
        }
        if (nr == 0)
            return;
        var comps = c.Components;
        for (int j = 0; j < comps.Length; j++)
        {
            double most = 0;
            for (int a = 0; a < nr; a++)
                if (felt[rose[a]] > most && Effects.Overlaps(comps[j].Bounds, c.Cells[rose[a]].Box))
                    most = felt[rose[a]];
            if (most > 0 && most >= s.RuptureMPaMm * Effects.Armour(c, j))
                Effects.Pressed(c, k, j, s.PartBlastMPa[Math.Min((int)comps[j].Kind, s.PartBlastMPa.Length - 1)], most, s, ref fx);
        }
    }

    /// <summary>The parts standing on a weather deck her gas tore open (docs/visuals/destruction.md decision 2): one its gas wrecked
    /// goes when the most its cell felt beats its weight over its footprint (the gunhouses' rule, <see cref="ShipClass.LiftKPa"/>),
    /// flagged thrown with its speed (<see cref="Throw"/>). A record only: what wrecks a part is the gas's pressure classes
    /// (<see cref="FeltLanded"/>), as before. An enclosed gunhouse goes by its own way up (<see cref="WaysUpLanded"/>).</summary>
    void ThrownLanded(long now)
    {
        var comps = c.Components;
        for (int a = 0; a < n; a++)
        {
            int i = region[a];
            if (i == partNode || i >= c.Cells.Length || !(k.FaceTornM2[i * Faces + 2] > 0) || !(felt[i] > 0))
                continue;
            var box = c.Cells[i].Box;
            for (int j = 0; j < comps.Length; j++)
            {
                if (k.ThrownMs[j] != 0 || k.State[j] != PartState.Destroyed || !Throwable(j))
                    continue;
                var b = comps[j].Bounds;
                // standing on this deck (its foot within half a deck of the cell's top) over this cell
                if (Math.Abs(b.Base - box.Top) > 1.5 || b.X1 <= box.X0 || b.X0 >= box.X1 || b.Y1 <= box.Y0 || b.Y0 >= box.Y1)
                    continue;
                double weight = WeightKPa(j);
                if (felt[i] * 1e3 < weight)
                    continue;
                Throw(j, felt[i], weight * 1e-3, now);
            }
        }
    }

    /// <summary>A part that can be thrown off the deck: what stands on it, but an enclosed gunhouse (its own rule) and its barbette.</summary>
    bool Throwable(int j)
    {
        var kind = c.Components[j].Kind;
        if (kind is ComponentKind.Main or ComponentKind.Secondary)
            return c.MountOf(j) is int mt && mt >= 0 && !c.Enclosed(mt);
        return kind is ComponentKind.Superstructure or ComponentKind.ConningTower or ComponentKind.Funnel or ComponentKind.Mast
            or ComponentKind.Top or ComponentKind.Sponson or ComponentKind.Aa or ComponentKind.Torpedo or ComponentKind.TorpedoTube;
    }

    /// <summary>A part's weight over its footprint, kPa: its box's surface at its plate (or armour), steel, times what's in and
    /// on it (<see cref="SimSettings.ThrownWeightK"/>).</summary>
    double WeightKPa(int j)
    {
        ref readonly var p = ref c.Components[j];
        var b = p.Bounds;
        double w = Math.Max(b.X1 - b.X0, 0.3), l = Math.Max(b.Y1 - b.Y0, 0.3), h = Math.Max(b.Top - b.Base, 0.3);
        double mm = Math.Max(Math.Max(p.PlateMm, p.ArmourMm), 6);
        double tonnes = 7.85 * mm / 1000 * (2 * w * l + 2 * h * (w + l)) * s.ThrownWeightK;
        return tonnes * 9.81 / (w * l);
    }

    /// <summary>A part thrown at a moment: the gas at <paramref name="mpa"/> under it against its weight of
    /// <paramref name="weightMPa"/> over its footprint drives it up through its own height, at most
    /// <see cref="SimSettings.ThrownMostMs"/>.</summary>
    void Throw(int j, double mpa, double weightMPa, long now)
    {
        if (k.ThrownMs[j] != 0)
            return;
        var b = c.Components[j].Bounds;
        double excess = Math.Max(mpa / Math.Max(weightMPa, 1e-6) - 1, 0) * 9.81;   // its rise's acceleration, m/s²
        double v = Math.Min(Math.Sqrt(2 * excess * Math.Max(b.Top - b.Base, 1)), s.ThrownMostMs);
        k.Thrown(j, now, v);
    }

    /// <summary>A handling room or passage its gas reached hot enough to light charges since (<see cref="Hot"/>): a flash source for
    /// that mount's train (E5's Savannah), by how its charges lie there; the flame down the train's open doors may reach the magazine,
    /// which lights in this run.</summary>
    void FlashLanded(long now, ref EffectsResult fx, ref RandomStream r)
    {
        for (int a = 0; a < n; a++)
        {
            int i = region[a];
            if (i == partNode || hotLanded[i] || !Hot(i))
                continue;
            hotLanded[i] = true;
            foreach (int room in c.RoomsIn(i))
            {
                if (c.Rooms[room].Kind is not (RoomKind.Handling or RoomKind.Passage) || c.Rooms[room].Mount < 0)
                    continue;
                if (c.MountOf(c.Rooms[room].Mount) is not (int hm and >= 0) || c.StationOfRoom(hm, room) is not (var hs and >= 0))
                    continue;
                if (r.NextDouble() >= Magazines.Exposed(c, room) * Magazines.LightChance(c, k, hm, hs, s))
                    continue;
                int reached = Magazines.Light(c, k, hm, hs, burning: false, s, ref r, ref fx, now);
                if (reached >= 0 && AddStow(reached, default) && !fx.Magazine)
                    (fx.Magazine, fx.MagazineRoom, fx.MagazineVia) = (true, reached, MagazineVia.Gas);
            }
        }
    }

    /// <summary>Her magazine lit in still air and its gas stepped to its end, what it did landed on her as the game lands it, into fx (a
    /// bench's, a test's: <see cref="Magazines.SetOff"/>): at once, or with sliceMs a slice at a time as the world steps it, her liquid
    /// and waterline read again each time (docs/battle/workers.md ET4: whether the slices move what it does). What its gas did.</summary>
    public GasResult BurnOut(ShipCondition cond, int room, ReadOnlySpan<MountState> mounts, SimSettings settings, long now, ref RandomStream r, ref EffectsResult fx,
        int sliceMs = 0, Func<GasNetwork, bool>? landed = null)
    {
        Begin(cond, settings);
        startMs = now;
        if (!AddStow(room, mounts))
            return res;
        do
        {
            if (sliceMs > 0)
                AdvanceTo(NowMs + sliceMs);
            else
                Advance(double.PositiveInfinity);
            var x = Land(NowMs, ref r);
            (fx.Killed, fx.Wounded, fx.Destroyed, fx.Disabled, fx.Damaged) = (fx.Killed + x.Killed, fx.Wounded + x.Wounded, fx.Destroyed + x.Destroyed, fx.Disabled + x.Disabled, fx.Damaged + x.Damaged);
            (fx.BreachM2, fx.Fire, fx.CrewLost) = (fx.BreachM2 + x.BreachM2, fx.Fire || x.Fire, fx.CrewLost + x.CrewLost);
            if (landed?.Invoke(this) == true)
                break;   // the caller's done: she broke
        }
        while (!finished);   // a handling room its gas reached may have lit another
        return Report();
    }

    void Wreck(int node, ref EffectsResult fx) =>
        Effects.Count(ref fx, k, node, k.Worsen(node, PartState.Destroyed) ? PartState.Destroyed : PartState.Ok);
}
