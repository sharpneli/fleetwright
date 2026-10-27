using Fleetwright.Shipgen;
using Fleetwright.Sim.Core;
using Fleetwright.Sim.Ships;

namespace Fleetwright.Sim.Combat;

/// <summary>What a fire step did: whether a part or space got worse (what the ship can do must be worked out again),
/// fires spread, flashed over, lit again from hot steel and put out or burnt out, the crew it cost, and whether it set off
/// a magazine.</summary>
public struct FireStep
{
    public bool Changed, Magazine;
    /// <summary>The magazine set off (a room of the class), when <see cref="Magazine"/>; else -1.</summary>
    public int MagazineRoom;
    public int Spread, Out, Flashed, Reflashed;
    /// <summary>Machinery spaces abandoned to smoke, and magazines her crew began to flood (B2).</summary>
    public int Abandoned, Flooded;
    /// <summary>The crew it cost, and the water it left in the cells (the hoses' and a magazine's flooding), m³.</summary>
    public double CrewLost, WaterM3;
}

/// <summary>Where a fire is in its life (damage/17 §3-4, §6.1): growing as t², burning locally (its load can't feed a
/// flashover), starved (its air can't), developed (flashed over, burning as fast as its air and fuel let it), decaying on
/// its last fuel, under control (knocked down, being mopped up), or out.</summary>
public enum BurnPhase : byte
{
    None,
    Grow,
    Local,
    Starved,
    Developed,
    Decay,
    /// <summary>Knocked down by water, dying while it's mopped up.</summary>
    Control,
    Out,
}

/// <summary>A fire space's fire (docs/battle/fire.md): its heat release Q (MW), the fuel left and at its first ignition
/// (MJ), the area involved (m²), the time since it was lit and in its phase (s; out, since it went out), the time it has
/// burned developed (s), the seat it was lit by (MW), a propellant seat's charges left (MJ) and the rate they burn at (MW),
/// the most area it involved (m²) and the water it has been mopped up with (L), the dose of heat what's in the space has
/// taken (s of developed fire over the whole space; it stays when the fire goes out) and the harms it has done, whether it
/// went out hot (its steel can light it again), and its phase.</summary>
public struct BurnState
{
    public double Q, E, E0, Involved, T, PhaseS, DevS, Seat, Charges, ChargesMW, InvolvedMax, Mop, HeatS;
    public byte Harmed;
    public bool Hot;
    /// <summary>The fire watch's hose on it (B4): 0 not yet, 1 on, 2 cut.</summary>
    public byte Watch;
    public BurnPhase Phase;
    public readonly bool Burning => Phase is > BurnPhase.None and < BurnPhase.Out;
}

/// <summary>What one space's fire did in a step (<see cref="Fires.Advance"/>): flashed over, went out, or lost its water
/// under control before it was out (the fire step draws whether it rekindles).</summary>
public enum BurnEvent : byte
{
    None,
    Flashover,
    Out,
    Dry,
}

/// <summary>A fire party (docs/battle/fire.md B4): its men, their skill (1, the gun crews turned to less), the section it's
/// in, the fire space it's sent to (-1: none), when it gets there and when a hit stopped it until (world ms), and whether
/// it has mustered.</summary>
public struct RepairParty
{
    public double Men, Skill;
    /// <summary>Where it is, and the fire space it fights (-1: none).</summary>
    public int Section, Target;
    public long ArriveMs, StopMs;
    public bool Mustered;
    /// <summary>Its water job (docs/battle/damage-control.md DC3), where (an edge of the flooding model, or a cell), and the
    /// man-minutes done on it.</summary>
    public RepairJob Job;
    public int JobAt;
    public double Done;
}

/// <summary>The water on a fire (docs/battle/fire.md B3): the flow as fog would do the work (L/min), the flow thrown
/// (L/min), and the most of it the fire can boil (the nozzle's share).</summary>
public readonly record struct FireWater(double FogLpm, double Lpm, double BoilShare);

/// <summary>FIRE (docs/battle/fire.md; damage/17 §8): each fire space (a cell, a part with a space inside, a section's
/// weather deck) burns as one compartment fire. It grows as t² from its seat; flashes over at Thomas's threshold when its
/// fuel and air can feed it, else burns locally and dies or smoulders starved; developed it spreads through the space a
/// room at a time and burns at the lower of its air (the openings, 1.5·A√H MW) and its fuel (0.25 MW/m² over the
/// involved area, less for thin loads); it decays on its last fifth and goes out when the fuel's gone. A propellant seat
/// burns its charges on top, by its own oxygen.
/// <para>The openings are the ship's orders' doors (her readiness), two a part, a cell's holes to the sea and what bursts
/// opened or tore; a developed fire spreads along the class's fire links (open paths in a minute or two, steel up,
/// sideways and down after delays, the weather deck along), and a space that went out hot may light again (B1). Water
/// knocks a fire down, holds it or floods the ship (B3); her side's doctrine sends the water (B4: the fire watch's hose,
/// the parties, the main by section). What it does (B2): its heat harms what's in the space by a dose of time, a
/// flashover catches the room's men and the parties lose men to smoke, smoke takes the machinery under it (and steam and
/// the pumps with it), its heat cooks a magazine off on its boundary unless she floods it first, and a fire shows to the
/// enemy by its heat when it's developed or in the open. Pure and allocation-free; the draws come from the stream it's
/// given.</para></summary>
public static class Fires
{
    public static FireStep Step(ShipClass c, FloodModel f, ShipCondition k, double dt, SimSettings s, ref RandomStream r, long nowMs = 0)
    {
        var res = new FireStep { MagazineRoom = -1 };
        Fight(c, f, k, nowMs, s);
        double reflash = Chance(s.FireReflashPerMin * (k.DamageControl.ReflashWatch ? s.FireReflashWatchShare : 1), dt);
        bool caught = false, smoky = false;
        double show = 0;
        var spaces = c.FireSpaces;
        for (int i = 0; i < spaces.Length; i++)
        {
            ref var x = ref k.Fire[i];
            ref readonly var row = ref spaces[i];
            if (!x.Burning)
            {
                // hot steel after a developed fire: it may light again while it holds its heat
                if (x.Phase == BurnPhase.Out && x.Hot)
                {
                    x.PhaseS += dt;
                    if (x.PhaseS > s.FireReflashS)
                        x.Hot = false;
                    else if (r.NextDouble() < reflash && !Wet(f, k, i, s) && Ignite(c, k, i, s.FireReflashSeatMW, s))
                        res.Reflashed++;
                }
                continue;
            }
            bool cell = row.Kind == FireSpaceKind.Cell;
            if (Wet(f, k, i, s))
            {
                Douse(c, k, i, s);
                res.Out++;
                continue;
            }
            var e = Advance(row, ref x, Vent(c, k, i, s), Load(row, k), dt, s, k.Readiness.WetDecks, k.FireFlow[i], out double aboardL);
            if (cell && aboardL > 0)
            {
                // what the fire didn't boil stays in the cell, to the flooding (17 §6.5); on deck and in the upper works it runs off
                k.WaterM3[row.Index] = Math.Min(f.Capacity[row.Index], k.WaterM3[row.Index] + aboardL / 1000);
                res.WaterM3 += aboardL / 1000;
            }
            if (e == BurnEvent.Dry)
            {
                // the water stopped before it was out: it rekindles, or it was beaten after all
                if (r.NextDouble() < s.FireRekindleShare)
                    (x.Phase, x.Seat, x.Q, x.T, x.Involved) = (BurnPhase.Grow, s.FireReflashSeatMW, s.FireReflashSeatMW, 60, 0);
                else
                    e = BurnEvent.Out;
            }
            if (e == BurnEvent.Flashover)
            {
                res.Flashed++;
                caught |= Caught(c, k, i, s, caught);
            }
            else if (e == BurnEvent.Out)
            {
                if (x.Burning)
                    Douse(c, k, i, s);
                Mark(c, k, i);
                res.Out++;
                continue;
            }
            res.Changed |= Heat(c, k, i, dt, s);
            if (Shows(row, x))
                show += x.Q;
            smoky |= Smoky(x) || x.Charges > 0;
            if (x.Phase is BurnPhase.Developed or BurnPhase.Decay && x.Q > s.FireSpreadMinMW)
                res.Spread += Spread(c, f, k, i, dt, s, ref r);
        }
        k.FireShowMW = show;
        if (caught)
        {
            var men = new EffectsResult();
            Casualties.Draw(c, k, nowMs, s, ref men, ref r);
            res.CrewLost += men.CrewLost;
        }
        res.CrewLost += Hurt(c, k, nowMs, dt, s);
        if (k.Status != ShipStatus.Afloat)
            return res;
        if (smoky)
            Smoke(c, k, nowMs, dt, s, ref r, ref res);
        Magazines(c, f, k, dt, s, nowMs, ref r, ref res);
        return res;
    }

    // ------------------------------------------------------------------ what a fire does (B2)

    /// <summary>Whether a space's fire shows from outside (B2; 17 §8.5): developed, in the open, or its charges burning; a
    /// starved or growing fire inside the hull shows nothing.</summary>
    public static bool Shows(in FireSpaceRow row, in BurnState x) =>
        x.Burning && (row.OpenAir || x.Phase is BurnPhase.Developed or BurnPhase.Decay || x.Charges > 0);

    /// <summary>Whether a space's fire fills it with smoke and heat: developed, decaying or starved (fire_ref.py's smoky).</summary>
    static bool Smoky(in BurnState x) => x.Phase is BurnPhase.Developed or BurnPhase.Decay or BurnPhase.Starved;

    /// <summary>Whether a space's fire heats its boundaries enough to cook off a magazine: developed, or charges burning.</summary>
    static bool Heats(in BurnState x) => x.Phase is BurnPhase.Developed or BurnPhase.Decay || x.Charges > 0 && x.Burning;

    /// <summary>A fire's heat on what's in its space (B2): the dose (developed, by the share of the space involved; growing,
    /// local or starved, lightly), and each of <see cref="SimSettings.FireHarmS"/> it passes harms the cell's rooms (a part
    /// itself) once more. Whether a part got worse.</summary>
    static bool Heat(ShipClass c, ShipCondition k, int space, double dt, SimSettings s)
    {
        ref readonly var row = ref c.FireSpaces[space];
        if (row.Kind == FireSpaceKind.Deck)
            return false;
        ref var x = ref k.Fire[space];
        x.HeatS += dt * x.Phase switch
        {
            BurnPhase.Developed or BurnPhase.Decay => row.AreaM2 > 0 ? Math.Min(1, x.Involved / row.AreaM2) : 1,
            BurnPhase.Grow or BurnPhase.Local or BurnPhase.Starved => s.FireLocalHarmShare,
            _ => 0,
        };
        bool changed = false;
        while (x.Harmed < s.FireHarmS.Length && x.HeatS >= s.FireHarmS[x.Harmed])
        {
            x.Harmed++;
            if (row.Kind == FireSpaceKind.Cell)
                foreach (int room in c.RoomsIn(row.Index))
                    changed |= k.Harm(c.RoomNode(room));
            else
                changed |= k.Harm(row.Index);
        }
        return changed;
    }

    /// <summary>The men in the room that flashed over (B2): the room's share of the space's men killed and hurt by
    /// <see cref="SimSettings.FireFlashCaught"/>, a risk on their group drawn with the step's others (16's draw). A
    /// propellant seat's men are its flash's (16 §4.6, at the hit). Whether it added a risk.</summary>
    static bool Caught(ShipClass c, ShipCondition k, int space, SimSettings s, bool begun)
    {
        ref readonly var row = ref c.FireSpaces[space];
        if (row.Kind == FireSpaceKind.Deck || k.Fire[space].Charges > 0 || row.AreaM2 <= 0)
            return false;
        int g = row.Kind == FireSpaceKind.Cell ? Casualties.Cell(c, row.Index) : Casualties.Part(row.Index);
        if (Casualties.Men(c, k, g) <= 0)
            return false;
        if (!begun)
            Casualties.Begin(k);
        double share = Room(row, s) / row.AreaM2;
        Casualties.Add(k, g, share * s.FireFlashCaught[0], share * s.FireFlashCaught[1]);
        return true;
    }

    /// <summary>The men the parties lose working developed fires below decks (B2): smoke and burns, fewer with breathing
    /// sets; off the party, into her killed and wounded.</summary>
    static double Hurt(ShipClass c, ShipCondition k, long now, double dt, SimSettings s)
    {
        double rate = s.RepairPartyHurtPerMin[k.DamageControl.BreathingSets ? 1 : 0] * dt / 60, lost = 0;
        for (int p = 0; p < k.PartyCount; p++)
        {
            ref var party = ref k.Parties[p];
            if (party.Target < 0 || now < party.ArriveMs || c.FireSpaces[party.Target].Kind != FireSpaceKind.Cell ||
                k.Fire[party.Target].Phase is not (BurnPhase.Developed or BurnPhase.Decay))
                continue;
            double men = party.Men * rate;
            party.Men -= men;
            lost += men;
        }
        if (lost > 0)
            (k.Killed, k.Wounded, k.CrewLost) = (k.Killed + lost * s.RepairPartyKilledShare, k.Wounded + lost * (1 - s.RepairPartyKilledShare), k.CrewLost + lost);
        return lost;
    }

    /// <summary>Smoke into the machinery (B2; 17 §5.4, fire_ref.py): a boiler or engine room burning, or under a space whose
    /// fire is smoky, abandoned with the chance a minute her fans give (running or secured), and out on E4's clock while
    /// the smoke goes on and <see cref="SimSettings.FireSmokeClearS"/> after; its steam lost, her pumps and the fire main go
    /// with it (<see cref="SectionWater"/> by her power).</summary>
    static void Smoke(ShipClass c, ShipCondition k, long now, double dt, SimSettings s, ref RandomStream r, ref FireStep res)
    {
        double p = Chance(k.Readiness.FansSecured ? s.FireSmokeSecuredPerMin : s.FireSmokeFansPerMin, dt);
        long clear = (long)(s.FireSmokeClearS * 1000);
        var cells = c.Cells;
        for (int i = 0; i < cells.Length; i++)
        {
            int room = cells[i].Room;
            if (room < 0 || c.Rooms[room].Kind is not (RoomKind.BoilerRoom or RoomKind.EngineRoom))
                continue;
            bool smoke = Smoky(k.Fire[i]);
            foreach (ref readonly var l in c.FireLinks(c.FireSpaces[i].Links))
                smoke |= l.Way == FireWay.Up && Smoky(k.Fire[l.Space]);
            if (!smoke)
                continue;
            int node = c.RoomNode(room);
            if (k.Smoked[room] && !k.Has(node, PartFlags.Down))
                k.Smoked[room] = false;   // its last outage ran out: manned again
            if (k.Smoked[room])
                k.Down(node, now, clear, DownCause.Smoke);   // still smoke-filled
            else if (r.NextDouble() < p)
            {
                k.Down(node, now, clear, DownCause.Smoke);
                k.Smoked[room] = true;
                res.Changed = true;
                res.Abandoned++;
            }
        }
    }

    /// <summary>The magazines' heat (B2; doc 10 §2.3, §7.2 rule 12): each fire on a magazine's boundary (and its enclosed
    /// mounts' trunks: a shield's or casemate's fire reaches it only through the decks, docs/battle/magazines.md decision 4) doses it by the way it comes (<see cref="SimSettings.FireMagazineS"/>), and it goes up past its drawn
    /// limit; the dose cools without a fire. Her doctrine floods one threatened for its time unless smoke over it cuts the
    /// controls off; the water (the sea's under the waterline, else her main's) fills it, and the
    /// flooding takes it from there (a flooded magazine's mounts lose their ammunition, a drowned one can't go up).</summary>
    static void Magazines(ShipClass c, FloodModel f, ShipCondition k, double dt, SimSettings s, long now, ref RandomStream r,
        ref FireStep res)
    {
        var rooms = c.Rooms;
        ref readonly var doc = ref k.DamageControl;
        for (int m = 0; m < rooms.Length; m++)
        {
            if (rooms[m].Kind != RoomKind.Magazine)
                continue;
            if (k.State[c.RoomNode(m)] == PartState.Destroyed)
            {
                k.MagazineThreatS[m] = 0;   // spent: lit in a race she lived through, nothing left in it to go up
                continue;
            }
            double held = 0, cap = 0, rate = 0;
            bool blocked = false;
            foreach (int cell in c.Links(rooms[m].Cells))
            {
                (held, cap) = (held + k.WaterM3[cell], cap + f.Capacity[cell]);
                rate += Exposure(c, k, cell, s, ref blocked);
            }
            for (int t = 0; t < c.Mounts.Length; t++)
                if (c.Mounts[t].Magazine == m && c.Enclosed(t) && c.FireOfPart(c.Mounts[t].Component) is int g and >= 0 && Heats(k.Fire[g]))
                    rate += 1 / s.FireMagazineS[k.Fire[g].Charges > 0 ? 0 : 1];   // down its turret's trunk
            if (cap > 0 && held >= s.FireDrownShare * cap || k.MagazineWet[m] >= s.MagazineSprinkleShare)
            {
                k.MagazineThreatS[m] = 0;   // drowned or sprinkled through: it can't go up
                continue;
            }
            if (rate <= 0)
            {
                (k.MagazineHeat[m], k.MagazineThreatS[m]) = (Math.Max(0, k.MagazineHeat[m] - dt / s.FireMagazineCoolS), 0);
                continue;
            }
            if (k.MagazineLimit[m] <= 0)
                k.MagazineLimit[m] = Math.Exp(s.FireMagazineSigma * r.NextGaussian());
            k.MagazineHeat[m] += rate * dt;
            k.MagazineThreatS[m] += dt;
            if (k.MagazineHeat[m] >= k.MagazineLimit[m])
            {
                (res.Magazine, res.MagazineRoom) = (true, m);
                return;
            }
            if (!k.MagazineFlood[m] && k.MagazineOrderMs[m] == 0 && !blocked && doc.Name is not null && doc.FloodMagazineS >= 0 &&
                k.MagazineThreatS[m] >= doc.FloodMagazineS)
            {
                Combat.Magazines.Order(k, m, now);   // the threat's own clock was the decision: the valves at once
                res.Flooded++;
            }
        }
    }

    /// <summary>The rate a magazine's cell is heated by the fires on it, per second: a fire in it, a handling room's or
    /// burning charges beside it, an open path, under it, beside it, over it (<see cref="SimSettings.FireMagazineS"/>);
    /// and whether a smoky space over it has its flood controls cut off.</summary>
    static double Exposure(ShipClass c, ShipCondition k, int cell, SimSettings s, ref bool blocked)
    {
        double rate = Heats(k.Fire[cell]) ? 1 / s.FireMagazineS[0] : 0;
        foreach (ref readonly var l in c.FireLinks(c.FireSpaces[cell].Links))
        {
            ref readonly var y = ref k.Fire[l.Space];
            blocked |= l.Way == FireWay.Up && Smoky(y);
            if (!Heats(y))
                continue;
            ref readonly var n = ref c.FireSpaces[l.Space];
            bool handling = n.Kind == FireSpaceKind.Cell && c.Cells[n.Index].Room is int room and >= 0 && c.Rooms[room].Kind == RoomKind.Handling;
            int way = y.Charges > 0 || handling ? 0
                : l.Way == FireWay.Open || l.Pool >= 0 && k.JoinOpenM2[l.Pool] >= s.FireOpenPathM2 ? 1
                : l.Way == FireWay.Down ? 2
                : l.Way == FireWay.Side ? 3
                : 4;
            rate += 1 / s.FireMagazineS[way];
        }
        return rate;
    }

    /// <summary>A developed fire reaching the spaces it links to (17 §5; fire.md B1): the fires it lit.</summary>
    static int Spread(ShipClass c, FloodModel f, ShipCondition k, int space, double dt, SimSettings s, ref RandomStream r)
    {
        ref readonly var row = ref c.FireSpaces[space];
        ref readonly var x = ref k.Fire[space];
        double vent = Vent(c, k, space, s), q = Load(row, k);
        bool flamesOut = !row.OpenAir && Fuel(x.Involved, q, s) > s.FireFlamesOutRatio * s.FireVentMW * vent;
        double openPath = s.FireOpenPathPerMin * Math.Min(1, x.Q / s.FireOpenPathMW) * (flamesOut ? s.FireFlamesOutK : 1);
        // boundary cooling (17 §5.2), where the doctrine has it and the fire's water meets its need
        bool cool = k.DamageControl.CoolBoundaries && k.FireFlow[space].FogLpm >= Math.Max(s.FireWaterMinLpm, s.FireWaterLm2 * x.Involved);
        int lit = 0;
        foreach (ref readonly var l in c.FireLinks(row.Links))
        {
            ref readonly var n = ref c.FireSpaces[l.Space];
            if (k.Fire[l.Space].Burning || Wet(f, k, l.Space, s))
                continue;
            double qn = Load(n, k), perMin = 0;
            switch (l.Way)
            {
                case FireWay.Open:
                case FireWay.Out:
                    perMin = openPath;
                    break;
                case FireWay.Deck:
                    perMin = s.FireDeckPerMin * Math.Min(s.FireDeckLoadMax, qn / s.FireDeckLoadMJm2) * (k.Readiness.WetDecks ? s.FireWetSpread : 1);
                    break;
                default:
                    if (row.Kind == FireSpaceKind.Cell && Opened(c, k, row.Index, l, s))
                        perMin = openPath;
                    int way = l.Way == FireWay.Up ? 0 : l.Way == FireWay.Side ? 1 : 2;
                    if (!row.OpenAir && x.DevS >= s.FireSteelDelayS[way])
                        perMin += s.FireSteelPerMin[way] * Math.Clamp(qn / s.FireSteelLoadMJm2, s.FireSteelLoadMin, s.FireSteelLoadMax) *
                                  (n.PaintMJm2 >= s.FireHeavyPaintMJm2 ? s.FireHeavyPaintK : 1) * (cool ? s.FireCoolShare : 1);
                    break;
            }
            if (perMin > 0 && r.NextDouble() < Chance(perMin, dt) && Ignite(c, k, l.Space, s.FireSpreadSeatMW, s))
                lit++;
        }
        return lit;
    }

    /// <summary>Whether a way between two cells is an open path: a burst left an opening of
    /// <see cref="SimSettings.FireOpenPathM2"/> or more in it, or it's a bulkhead over the waterline with her doors open.</summary>
    static bool Opened(ShipClass c, ShipCondition k, int cell, in FireLink l, SimSettings s)
    {
        if (l.Pool < 0)
            return false;
        if (k.JoinOpenM2[l.Pool] >= s.FireOpenPathM2)
            return true;
        return l.Way == FireWay.Side && k.Readiness.CellDoors > 0 && !c.Cells[cell].BelowWaterline &&
               !c.Cells[c.FireSpaces[l.Space].Index].BelowWaterline;
    }

    /// <summary>A fire lit in a space by a seat of <paramref name="seatMW"/>, with a propellant seat's charges (MJ) on top:
    /// true when it's a new fire. Nothing that burns from a shell (a load under <see cref="SimSettings.FireMinLoadMJm2"/>)
    /// or a space burnt out lights, unless it's charges; a space already burning takes the seat's heat and charges into its
    /// own.</summary>
    public static bool Ignite(ShipClass c, ShipCondition k, int space, double seatMW, SimSettings s, double chargesMJ = 0)
    {
        if (space < 0)
            return false;
        ref readonly var row = ref c.FireSpaces[space];
        double q = Load(row, k);
        bool burns = row.Use != FireUse.None && q >= s.FireMinLoadMJm2 && row.AreaM2 > 0;
        if (!burns && !(chargesMJ > 0 && row.AreaM2 > 0))
            return false;
        ref var x = ref k.Fire[space];
        if (chargesMJ > 0)
            (x.Charges, x.ChargesMW) = (x.Charges + chargesMJ, (x.Charges + chargesMJ) / s.FirePropellantBurnS);
        if (x.Burning)
        {
            x.Seat += seatMW;
            return false;
        }
        if (x.E0 <= 0)
            x.E0 = x.E = q * row.AreaM2;
        if (x.E <= s.FireBurntShare * x.E0 && !(chargesMJ > 0))
            return false;
        (x.Phase, x.Q, x.Seat, x.T, x.PhaseS, x.DevS, x.Involved, x.Hot, x.Watch, x.InvolvedMax, x.Mop) =
            (BurnPhase.Grow, seatMW, seatMW, 0, 0, 0, 0, false, 0, 0, 0);
        Mark(c, k, space);
        return true;
    }

    /// <summary>One space's fire over dt seconds unfought (the bench's free burns).</summary>
    public static BurnEvent Advance(in FireSpaceRow row, ref BurnState x, double aoh, double q, double dt, SimSettings s, bool wet = false) =>
        Advance(row, ref x, aoh, q, dt, s, wet, default, out _);

    /// <summary>One space's fire over dt seconds, from its openings' A√H (m^2.5; infinite in the open), its fire load
    /// (MJ/m²), its weather deck wetted or not, and the water on it: the physics alone, for the ship's step and the bench
    /// (damage/17 §8.2, fire_ref.py's Space and attack). A propellant seat's charges burn on top at their own rate, water
    /// or not, and the fire isn't out while they last. <paramref name="aboardL"/>: the water left in the space, L.</summary>
    public static BurnEvent Advance(in FireSpaceRow row, ref BurnState x, double aoh, double q, double dt, SimSettings s, bool wet,
        in FireWater water, out double aboardL)
    {
        aboardL = 0;
        if (!x.Burning || dt <= 0)
            return BurnEvent.None;
        var ev = BurnEvent.None;
        bool open = row.OpenAir;
        double area = row.AreaM2, room = Room(row, s);
        double qvent = open ? double.PositiveInfinity : s.FireVentMW * aoh;
        double qfo = open ? double.PositiveInfinity : Flashover(room, row.HeightM, aoh, s);
        double fuelRoom = Fuel(room, q, s), lim = Math.Min(fuelRoom, qvent);
        double charges = x.Charges > 0 ? Math.Min(x.Charges, x.ChargesMW * dt) / dt : 0;
        double need = Math.Max(s.FireWaterMinLpm, s.FireWaterLm2 * (x.Phase == BurnPhase.Control ? x.InvolvedMax : x.Involved));
        double held = water.FogLpm > 0 ? Math.Min(1, water.FogLpm / need) : 0;
        x.T += dt;
        if (x.Phase == BurnPhase.Control)
        {
            if (water.Lpm <= 0)
                return BurnEvent.Dry;
            x.Q *= Math.Exp(-dt / s.FireControlS);
            x.Mop += water.Lpm * dt / 60;
            if (x.Mop >= s.FireMopLm2 * x.InvolvedMax && charges <= 0)
                ev = BurnEvent.Out;
        }
        else if (held >= 1)
        {
            // knocked down (Shadwell's fog attack: control in ~2 min)
            x.Q *= Math.Exp(-dt / (s.FireKnockS * need / water.FogLpm));
            if (x.Q < s.FireControlMW + s.FireControlMWm2 * x.Involved)
                (x.Phase, x.Mop, x.PhaseS) = (BurnPhase.Control, 0, 0);
        }
        else
            switch (x.Phase)
            {
                case BurnPhase.Grow:
                {
                    double a = s.FireAlphaMWs2 * row.Growth * s.FireUseGrowth[Math.Min((int)row.Use, s.FireUseGrowth.Length - 1)];
                    x.Q = x.Seat + a * x.T * x.T * (1 - held) * (1 - held);
                    if (!open && x.Q + charges >= qfo && fuelRoom >= qfo && qvent >= qfo)
                    {
                        (x.Phase, x.PhaseS, x.Involved) = (BurnPhase.Developed, 0, room);
                        ev = BurnEvent.Flashover;
                    }
                    else if (x.Q >= lim)
                    {
                        x.Q = lim;
                        x.PhaseS = 0;
                        x.Phase = open ? q * (wet ? s.FireWetSustain : 1) >= s.FireOpenSustainMJm2 ? BurnPhase.Developed : BurnPhase.Local
                            : fuelRoom < qfo ? BurnPhase.Local : BurnPhase.Starved;
                        if (x.Phase == BurnPhase.Developed)
                            x.Involved = area;
                    }
                    else
                        x.Involved = open ? Math.Min(area, x.Q / s.FireOpenMWm2) : room * Math.Clamp(x.Q / qfo, 0.1, 1);
                    break;
                }
                case BurnPhase.Local:
                    x.Q = lim * (1 - held);
                    x.PhaseS += dt;
                    if (charges <= 0 && (x.PhaseS > s.FireLocalS || x.E0 - x.E >= s.FireLocalShare * room * q))
                        ev = BurnEvent.Out;
                    break;
                case BurnPhase.Starved:
                    x.PhaseS += dt;
                    x.Q = Math.Max(s.FireSmoulderMW, lim) * (1 - held);
                    if (qvent >= qfo && fuelRoom >= qfo)
                    {
                        // a hole or an opened door lets the air in: it flashes (backdraft)
                        (x.Phase, x.PhaseS, x.Involved) = (BurnPhase.Developed, 0, room);
                        ev = BurnEvent.Flashover;
                    }
                    else if (charges <= 0 && x.PhaseS > s.FireStarvedS)
                        ev = BurnEvent.Out;
                    break;
                case BurnPhase.Developed:
                case BurnPhase.Decay:
                    x.DevS += dt;
                    x.Involved = open ? area : Math.Min(area, x.Involved + room * dt / s.FireRoomToRoomS);
                    x.Q = Math.Min(qvent, Fuel(x.Involved, q, s)) * (1 - s.FireHeldDeveloped * held);
                    if (x.Phase == BurnPhase.Developed && x.E <= s.FireDecayShare * x.E0)
                        x.Phase = BurnPhase.Decay;
                    if (x.Phase == BurnPhase.Decay)
                        x.Q *= Math.Max(0.25, x.E / (s.FireDecayShare * x.E0));
                    break;
            }
        x.InvolvedMax = Math.Max(x.InvolvedMax, x.Involved);
        if (water.Lpm > 0)
        {
            // the water a fire doesn't boil stays in the space (17 §6.5)
            double thrown = water.Lpm * dt / 60;
            aboardL = thrown - Math.Min(thrown * water.BoilShare, x.Q * dt / s.FireWaterMJkg);
        }
        x.E -= x.Q * dt;
        x.Charges = Math.Max(0, x.Charges - charges * dt);
        x.Q += charges;
        if (ev == BurnEvent.Out)
            return Out(ref x, x.Phase == BurnPhase.Starved || x.DevS > s.FireHotS);
        if (charges <= 0 && (x.E <= 0 || x.Phase == BurnPhase.Decay && x.Q < s.FireSmoulderMW))
            return Out(ref x, x.DevS > s.FireHotS);
        return ev;
    }

    static BurnEvent Out(ref BurnState x, bool hot)
    {
        (x.Phase, x.Q, x.Involved, x.PhaseS, x.Hot, x.Charges) = (BurnPhase.Out, 0, 0, 0, hot, 0);
        if (x.E < 0)
            x.E = 0;
        return BurnEvent.Out;
    }

    /// <summary>A fire put out (the water over it; the tests'): hot when it had burned developed long enough.</summary>
    internal static void Douse(ShipClass c, ShipCondition k, int space, SimSettings s)
    {
        Out(ref k.Fire[space], k.Fire[space].DevS > s.FireHotS);
        Mark(c, k, space);
    }

    /// <summary>Whether any space has hot steel that may light again (the fire step must still run).</summary>
    public static bool Hot(ShipCondition k)
    {
        foreach (ref readonly var x in k.Fire.AsSpan())
            if (x.Phase == BurnPhase.Out && x.Hot)
                return true;
        return false;
    }

    /// <summary>The space's burning shown where the rest of the sim reads it: a cell's flag, a part's.</summary>
    static void Mark(ShipClass c, ShipCondition k, int space)
    {
        ref readonly var row = ref c.FireSpaces[space];
        bool burning = k.Fire[space].Burning;
        if (row.Kind == FireSpaceKind.Cell)
            k.CellFire[row.Index] = burning;
        else if (row.Kind == FireSpaceKind.Part)
            k.Flags[row.Index] = burning ? k.Flags[row.Index] | PartFlags.Burning : k.Flags[row.Index] & ~PartFlags.Burning;
    }

    // ------------------------------------------------------------------ firefighting (B4)

    /// <summary>The water on each fire now (docs/battle/fire.md B4; damage/17 §6, §8.3): her parties made at her first fire
    /// and sent every <see cref="SimSettings.FirePlanS"/> to the worst fires, the gun crews turning to after the action,
    /// each fire's own watch hose after the doctrine's delay, every flow at the water its section's main gives. Nothing
    /// when her side's doctrine is none.</summary>
    static void Fight(ShipClass c, FloodModel f, ShipCondition k, long now, SimSettings s)
    {
        ref readonly var doc = ref k.DamageControl;
        if (doc.Name is null)
            return;   // nobody fights her fires (the bench's and the tests' water is their own)
        Array.Clear(k.FireFlow);
        if (k.Status != ShipStatus.Afloat)
            return;
        var nozzle = Nozzle(s, doc.Nozzle);
        // her repair parties (docs/battle/damage-control.md DC3: one pool for fire and water), planned when due
        Repairs.Make(c, f, k, now, s);
        Repairs.PlanIfDue(c, f, k, now, s);
        var spaces = c.FireSpaces;
        for (int p = 0; p < k.PartyCount; p++)
        {
            ref var party = ref k.Parties[p];
            if (party.Target < 0 || now < party.ArriveMs || now < party.StopMs || party.Men < 2)
                continue;
            ref readonly var row = ref spaces[party.Target];
            ref readonly var x = ref k.Fire[party.Target];
            double lpm = party.Men * doc.ManLpm * party.Skill * s.FireRotation * SectionWater(c, k, row.Section, now, s);
            // from the doorway of a smoke-filled space below, without breathing sets (17 §8.2)
            bool smoke = !doc.BreathingSets && row.Kind == FireSpaceKind.Cell && x.Phase is BurnPhase.Developed or BurnPhase.Decay;
            Add(k, party.Target, lpm * nozzle.FogShare * (smoke ? s.FireSmokeShare : 1), lpm, nozzle.BoilShare);
        }
        if (doc.WatchS < 0)
            return;
        for (int i = 0; i < spaces.Length; i++)
        {
            ref var x = ref k.Fire[i];
            if (!x.Burning || spaces[i].Kind == FireSpaceKind.Deck)
                continue;
            // the fire watch: the hose laid out at the locker, the men on the spot (a cell with none has no watch)
            if (x.Watch == 0 && x.T >= doc.WatchS && (spaces[i].Kind == FireSpaceKind.Part || c.Cells[spaces[i].Index].BattleCrew + c.Cells[spaces[i].Index].Crew > 0))
                x.Watch = 1;
            if (x.Watch == 1)
            {
                double lpm = doc.HoseLpm * SectionWater(c, k, spaces[i].Section, now, s);
                Add(k, i, lpm * nozzle.FogShare, lpm, nozzle.BoilShare);
            }
        }
    }

    static void Add(ShipCondition k, int space, double fog, double lpm, double boil)
    {
        ref var w = ref k.FireFlow[space];
        w = new FireWater(w.FogLpm + fog, w.Lpm + lpm, boil);
    }

    static FireNozzle Nozzle(SimSettings s, string name)
    {
        foreach (var n in s.FireNozzles)
            if (n.Name == name)
                return n;
        return s.FireNozzles[0];
    }

    /// <summary>Whether a fire space holds or adjoins a magazine (its parties' first care).</summary>
    public static bool NextToMagazine(ShipClass c, int space)
    {
        ref readonly var row = ref c.FireSpaces[space];
        if (row.Kind != FireSpaceKind.Cell)
            return false;
        foreach (int room in c.RoomsIn(row.Index))
            if (c.Rooms[room].Kind == RoomKind.Magazine)
                return true;
        foreach (var n in c.Neighbours(c.Cells[row.Index].Neighbours))
            foreach (int room in c.RoomsIn(n.Cell))
                if (c.Rooms[room].Kind == RoomKind.Magazine)
                    return true;
        return false;
    }

    /// <summary>The share of the water a section's fire main gives (17 §6.4): the pumps' (her plant's power, at least the
    /// doctrine's diesel floor), dry with buckets and extinguishers at a cut, and unlooped everywhere past it from the pumps
    /// (her machinery), until it's isolated and rigged past (the doctrine's time): then the cut section on a jumper.</summary>
    public static double SectionWater(ShipClass c, ShipCondition k, int section, long now, SimSettings s)
    {
        ref readonly var doc = ref k.DamageControl;
        double share = 1;
        int pump = c.PumpSection, step = section >= pump ? 1 : -1;
        for (int j = pump; ; j += step)
        {
            long cut = k.MainCutMs[j];
            if (cut > 0)
            {
                bool rigged = doc.RestoreS >= 0 && now - cut >= doc.RestoreS * 1000;
                if (j == section)
                    share = Math.Min(share, rigged ? s.FireJumperShare : s.FireDryShare);
                else if (!rigged && !doc.Looped)
                    share = s.FireDryShare;
            }
            if (j == section)
                break;
        }
        return share * Math.Max(k.PowerCap, doc.PumpFloor);
    }

    /// <summary>A hit in a section (17 §6.3-6.4, fire_ref.py's hit): its fire main broken with the doctrine's chance (more
    /// on a small ship: fewer risers, less ship between them), a party working there stopped for a while (hoses cut, men
    /// down) and maybe thinned, a watch hose there cut.</summary>
    public static void Hit(ShipClass c, FloodModel f, ShipCondition k, int section, long now, SimSettings s, ref RandomStream r)
    {
        k.LastHitMs = now;
        ref readonly var doc = ref k.DamageControl;
        if (doc.Name is null || section < 0 || section >= k.MainCutMs.Length)
            return;
        if (k.MainCutMs[section] == 0 && r.NextDouble() < doc.MainBreak * Math.Pow(s.FireMainRefT / Math.Max(f.DisplacementT, 1), s.FireMainExp))
            k.MainCutMs[section] = Math.Max(now, 1);
        for (int p = 0; p < k.PartyCount; p++)
        {
            ref var party = ref k.Parties[p];
            bool working = party.Target >= 0 && k.Fire[party.Target].Burning || party.Job != RepairJob.None;
            if (!working || party.Section != section || now < party.ArriveMs)
                continue;   // only a party at work (one whose fire went out waits for the next plan to be freed)
            if (r.NextDouble() < doc.HoseCut)
                party.StopMs = now + (long)(s.FireHitStopS * 1000);
            if (r.NextDouble() < s.FireHitMenChance)
                party.Men *= s.FireHitMenKeep;
        }
        var spaces = c.FireSpaces;
        for (int i = 0; i < spaces.Length; i++)
            if (k.Fire[i].Watch == 1 && spaces[i].Section == section && r.NextDouble() < 2 * doc.HoseCut)
                k.Fire[i].Watch = 2;
    }

    /// <summary>The space's fire load now, MJ/m²: as fitted, or stripped by her orders (<see cref="ShipCondition.Readiness"/>);
    /// a weather deck with what's piled on it besides (her orders' deck load: B5's clutter).</summary>
    public static double Load(in FireSpaceRow row, ShipCondition k) =>
        (k.Readiness.Stripped ? row.StrippedMJm2 : row.LoadMJm2) + (row.Kind == FireSpaceKind.Deck ? k.Readiness.DeckLoadMJm2 : 0);

    /// <summary>A space's openings' A√H, m^2.5: leakage, its doors (a cell's by her orders, two a part's), and a cell's holes
    /// to the sea and between spaces (what bursts opened in its decks and bulkheads, and tore), each lot as a square
    /// (A·A^0.25).</summary>
    public static double Vent(ShipClass c, ShipCondition k, int space, SimSettings s)
    {
        ref readonly var row = ref c.FireSpaces[space];
        if (row.Kind == FireSpaceKind.Deck)
            return double.PositiveInfinity;
        if (row.Kind == FireSpaceKind.Part)
            return s.FireLeakAoh + s.FirePartDoors * s.FireDoorAoh;
        double inside = k.CellOpened[row.Index] ? s.FireTornM2 : 0;
        foreach (ref readonly var l in c.FireLinks(row.Links))
            if (l.Pool >= 0)
                inside += k.JoinOpenM2[l.Pool];
        return s.FireLeakAoh + k.Readiness.CellDoors * s.FireDoorAoh + Math.Pow(k.BreachM2[row.Index], 1.25) + Math.Pow(inside, 1.25);
    }

    /// <summary>The room a fire flashes over in: the space's use's typical room (a cabin, a messdeck), at most the space.</summary>
    public static double Room(in FireSpaceRow row, SimSettings s)
    {
        double m2 = s.FireRoomM2[Math.Min((int)row.Use, s.FireRoomM2.Length - 1)];
        return m2 > 0 ? Math.Min(row.AreaM2, m2) : row.AreaM2;
    }

    /// <summary>Thomas's flashover threshold, MW, for a room of a deck area and height with openings of A√H, times the
    /// share fitted to the NBS berthing room (17 §3).</summary>
    public static double Flashover(double areaM2, double heightM, double aoh, SimSettings s)
    {
        double surface = 2 * areaM2 + 4 * Math.Sqrt(areaM2) * heightM;
        return s.FireThomasShare * (0.0078 * surface + 0.378 * aoh);
    }

    /// <summary>The heat a burning area can release on its fuel, MW: 0.25 MW/m² furnished, ramped down to nothing at the
    /// load that can't sustain a flashover (17 §3; DTIC ADA274791's bands).</summary>
    public static double Fuel(double areaM2, double q, SimSettings s) =>
        areaM2 * s.FireFuelMWm2 * Math.Clamp((q - s.FireFuelFloorMJm2) / s.FireFuelSpanMJm2, 0.01, 1);

    /// <summary>The chance of something that happens with a chance a minute, over dt seconds.</summary>
    static double Chance(double perMin, double dt) => perMin <= 0 ? 0 : 1 - Math.Pow(1 - Math.Min(perMin, 0.999999), dt / 60);

    /// <summary>A cell flooded past <see cref="SimSettings.FireDrownShare"/>: no fire there.</summary>
    static bool Wet(FloodModel f, ShipCondition k, int space, SimSettings s)
    {
        ref readonly var row = ref k.Class.FireSpaces[space];
        return row.Kind == FireSpaceKind.Cell && Drowned(f, k, row.Index, s);
    }

    static bool Drowned(FloodModel f, ShipCondition k, int cell, SimSettings s) => k.WaterM3[cell] >= s.FireDrownShare * f.Capacity[cell];
}
