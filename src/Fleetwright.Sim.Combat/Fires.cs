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
    /// <summary>The crew it cost, and the firefighting water it left in the cells, m³.</summary>
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
/// the most area it involved (m²) and the water it has been mopped up with (L), whether it went out hot (its steel can
/// light it again), and its phase.</summary>
public struct BurnState
{
    public double Q, E, E0, Involved, T, PhaseS, DevS, Seat, Charges, ChargesMW, InvolvedMax, Mop;
    public bool Hot;
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

/// <summary>The water on a fire (docs/battle/fire.md B3): the flow as fog would do the work (L/min), the flow thrown
/// (L/min), and the most of it the fire can boil (the nozzle's share).</summary>
public readonly record struct FireWater(double FogLpm, double Lpm, double BoilShare);

/// <summary>FIRE (docs/battle/fire.md; damage/17 §8): each fire space (a cell, a part with a space inside, a section's
/// weather deck) burns as one compartment fire. It grows as t² from its seat; flashes over at Thomas's threshold when its
/// fuel and air can feed it, else burns locally and dies or smoulders starved; developed it spreads through the space a
/// room at a time and burns at the lower of its air (the openings, 1.5·A√H MW) and its fuel (0.25 MW/m² over the
/// involved area, less for thin loads); it decays on its last fifth and goes out when the fuel's gone. A propellant seat
/// burns its charges on top, by its own oxygen.
/// <para>B1: the openings are the ship's orders' doors (her readiness), two a part, a cell's holes to the sea and what
/// bursts opened or tore; a developed fire spreads along the class's fire links (open paths in a minute or two, steel up,
/// sideways and down after delays, the weather deck along), and a space that went out hot may light again. Fire v0's
/// stand-ins stay until B2 and B4: a burning space harms its rooms and costs crew by the minute, is put out by its crew
/// with a chance a minute, and may set off a magazine in it or next to it. Pure and allocation-free; the draws come from
/// the stream it's given.</para></summary>
public static class Fires
{
    public static FireStep Step(ShipClass c, FloodModel f, ShipCondition k, double dt, SimSettings s, double propellant, ref RandomStream r)
    {
        var res = new FireStep { MagazineRoom = -1 };
        double crew = Math.Clamp(1 - k.CrewLost / f.Complement, 0, 1);
        double harm = Chance(s.FireHarmPerMin, dt), put = Chance(s.FireOutPerMin * crew, dt), reflash = Chance(s.FireReflashPerMin, dt);
        double magIn = Chance(s.FireMagazineInPerMin * propellant, dt), magNext = Chance(s.FireMagazineNextPerMin * propellant, dt);
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
            if (Wet(f, k, i, s) || r.NextDouble() < put)
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
                res.Flashed++;
            else if (e == BurnEvent.Out)
            {
                if (x.Burning)
                    Douse(c, k, i, s);
                Mark(c, k, i);
                res.Out++;
                continue;
            }
            res.CrewLost += s.FireCrewPerMin * dt / 60;
            if (r.NextDouble() < harm)
            {
                if (cell)
                    foreach (int room in c.RoomsIn(row.Index))
                        res.Changed |= k.Harm(c.RoomNode(room));
                else if (row.Kind == FireSpaceKind.Part)
                    res.Changed |= k.Harm(row.Index);
            }
            if (cell && !res.Magazine)
            {
                if (Magazine(c, f, k, row.Index, s) is int mi and >= 0 && r.NextDouble() < magIn)
                    (res.Magazine, res.MagazineRoom) = (true, mi);
                foreach (var n in c.Neighbours(c.Cells[row.Index].Neighbours))
                    if (!res.Magazine && Magazine(c, f, k, n.Cell, s) is int mn and >= 0 && r.NextDouble() < magNext)
                        (res.Magazine, res.MagazineRoom) = (true, mn);
            }
            if (x.Phase is BurnPhase.Developed or BurnPhase.Decay && x.Q > s.FireSpreadMinMW)
                res.Spread += Spread(c, f, k, i, dt, s, ref r);
        }
        k.CrewLost += res.CrewLost;
        return res;
    }

    /// <summary>A developed fire reaching the spaces it links to (17 §5; fire.md B1): the fires it lit.</summary>
    static int Spread(ShipClass c, FloodModel f, ShipCondition k, int space, double dt, SimSettings s, ref RandomStream r)
    {
        ref readonly var row = ref c.FireSpaces[space];
        ref readonly var x = ref k.Fire[space];
        double vent = Vent(c, k, space, s), q = Load(row, k);
        bool flamesOut = !row.OpenAir && Fuel(x.Involved, q, s) > s.FireFlamesOutRatio * s.FireVentMW * vent;
        double openPath = s.FireOpenPathPerMin * Math.Min(1, x.Q / s.FireOpenPathMW) * (flamesOut ? s.FireFlamesOutK : 1);
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
                                  (n.PaintMJm2 >= s.FireHeavyPaintMJm2 ? s.FireHeavyPaintK : 1);
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
        (x.Phase, x.Q, x.Seat, x.T, x.PhaseS, x.DevS, x.Involved, x.Hot) = (BurnPhase.Grow, seatMW, seatMW, 0, 0, 0, 0, false);
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

    /// <summary>A fire put out (by its crew, or the water over it): hot when it had burned developed long enough.</summary>
    static void Douse(ShipClass c, ShipCondition k, int space, SimSettings s)
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

    /// <summary>The space's fire load now, MJ/m²: as fitted, or stripped by her orders (<see cref="ShipCondition.Readiness"/>).</summary>
    public static double Load(in FireSpaceRow row, ShipCondition k) => k.Readiness.Stripped ? row.StrippedMJm2 : row.LoadMJm2;

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

    /// <summary>The magazine (a room) a cell holds, dry enough to go up; else -1.</summary>
    static int Magazine(ShipClass c, FloodModel f, ShipCondition k, int cell, SimSettings s)
    {
        if (Drowned(f, k, cell, s))
            return -1;
        foreach (int room in c.RoomsIn(cell))
            if (c.Rooms[room].Kind == RoomKind.Magazine)
                return room;
        return -1;
    }
}
