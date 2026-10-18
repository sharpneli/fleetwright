using Fleetwright.Shipgen;
using Fleetwright.Sim.Core;
using Fleetwright.Sim.Ships;

namespace Fleetwright.Sim.Combat;

/// <summary>What a fire step did: whether a part or space got worse (what the ship can do must be worked out again),
/// fires started, flashed over and put out or burnt out, the crew it cost, and whether it set off a magazine.</summary>
public struct FireStep
{
    public bool Changed, Magazine;
    /// <summary>The magazine set off (a room of the class), when <see cref="Magazine"/>; else -1.</summary>
    public int MagazineRoom;
    public int Spread, Out, Flashed;
    public double CrewLost;
}

/// <summary>Where a fire is in its life (damage/17 §3-4): growing as t², burning locally (its load can't feed a
/// flashover), starved (its air can't), developed (flashed over, burning as fast as its air and fuel let it), decaying on
/// its last fuel, or out.</summary>
public enum BurnPhase : byte
{
    None,
    Grow,
    Local,
    Starved,
    Developed,
    Decay,
    Out,
}

/// <summary>A fire space's fire (docs/battle/fire.md B0): its heat release Q (MW), the fuel left and at its first
/// ignition (MJ), the area involved (m²), the time since it was lit and in its phase (s), the time it has burned
/// developed (s: the hot steel's), the seat it was lit by (MW) and its phase.</summary>
public struct BurnState
{
    public double Q, E, E0, Involved, T, PhaseS, DevS, Seat;
    public BurnPhase Phase;
    public readonly bool Burning => Phase is > BurnPhase.None and < BurnPhase.Out;
}

/// <summary>What one space's fire did in a step (<see cref="Fires.Advance"/>).</summary>
public enum BurnEvent : byte
{
    None,
    Flashover,
    Out,
}

/// <summary>FIRE (docs/battle/fire.md; damage/17 §8): each fire space (a cell, a part with a space inside, a section's
/// weather deck) burns as one compartment fire. It grows as t² from its seat; flashes over at Thomas's threshold when its
/// fuel and air can feed it, else burns locally and dies or smoulders starved; developed it spreads through the space a
/// room at a time and burns at the lower of its air (the openings, 1.5·A√H MW) and its fuel (0.25 MW/m² over the
/// involved area, less for thin loads); it decays on its last fifth and goes out when the fuel's gone.
/// <para>B0: the openings are one door a cell, two a part, and the holes to the sea; the rest is Fire v0's stand-in
/// (fighting.md 4.3b): a burning space harms its rooms and costs crew by the minute, a burning cell spreads to a
/// neighbour and is put out by its crew with a chance a minute, and may set off a magazine in it or next to it. Pure and
/// allocation-free; the draws come from the stream it's given.</para></summary>
public static class Fires
{
    public static FireStep Step(ShipClass c, FloodModel f, ShipCondition k, double dt, SimSettings s, double propellant, ref RandomStream r)
    {
        var res = new FireStep { MagazineRoom = -1 };
        double crew = Math.Clamp(1 - k.CrewLost / f.Complement, 0, 1);
        double harm = Chance(s.FireHarmPerMin, dt), spread = Chance(s.FireSpreadPerMin, dt), put = Chance(s.FireOutPerMin * crew, dt);
        double magIn = Chance(s.FireMagazineInPerMin * propellant, dt), magNext = Chance(s.FireMagazineNextPerMin * propellant, dt);
        var spaces = c.FireSpaces;
        for (int i = 0; i < spaces.Length; i++)
        {
            ref var x = ref k.Fire[i];
            if (!x.Burning)
                continue;
            ref readonly var row = ref spaces[i];
            bool cell = row.Kind == FireSpaceKind.Cell;
            if (cell && Drowned(f, k, row.Index, s) || r.NextDouble() < put)
            {
                Douse(c, k, i, s);
                res.Out++;
                continue;
            }
            var e = Advance(row, ref x, Vent(c, k, i, s), Load(row), dt, s);
            if (e == BurnEvent.Flashover)
                res.Flashed++;
            else if (e == BurnEvent.Out)
            {
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
            if (!cell)
                continue;
            if (!res.Magazine && Magazine(c, f, k, row.Index, s) is int mi and >= 0 && r.NextDouble() < magIn)
                (res.Magazine, res.MagazineRoom) = (true, mi);
            foreach (var n in c.Neighbours(c.Cells[row.Index].Neighbours))
            {
                if (!res.Magazine && Magazine(c, f, k, n.Cell, s) is int mn and >= 0 && r.NextDouble() < magNext)
                    (res.Magazine, res.MagazineRoom) = (true, mn);
                if (!k.Fire[n.Cell].Burning && !Drowned(f, k, n.Cell, s) && r.NextDouble() < spread && Ignite(c, k, n.Cell, s.FireSpreadSeatMW, s))
                    res.Spread++;
            }
        }
        k.CrewLost += res.CrewLost;
        return res;
    }

    /// <summary>A fire lit in a space by a seat of <paramref name="seatMW"/>: true when it's a new fire. Nothing that burns
    /// from a shell (a load under <see cref="SimSettings.FireMinLoadMJm2"/>) or a space burnt out lights; a space already
    /// burning takes the seat's heat into its own.</summary>
    public static bool Ignite(ShipClass c, ShipCondition k, int space, double seatMW, SimSettings s)
    {
        if (space < 0)
            return false;
        ref readonly var row = ref c.FireSpaces[space];
        double q = Load(row);
        if (row.Use == FireUse.None || q < s.FireMinLoadMJm2 || !(row.AreaM2 > 0))
            return false;
        ref var x = ref k.Fire[space];
        if (x.Burning)
        {
            x.Q += seatMW;
            return false;
        }
        if (x.E0 <= 0)
            x.E0 = x.E = q * row.AreaM2;
        if (x.E <= s.FireBurntShare * x.E0)
            return false;
        (x.Phase, x.Q, x.Seat, x.T, x.PhaseS, x.DevS, x.Involved) = (BurnPhase.Grow, seatMW, seatMW, 0, 0, 0, 0);
        Mark(c, k, space);
        return true;
    }

    /// <summary>One space's fire over dt seconds, from its openings' A√H (m^2.5; infinite in the open) and its fire load
    /// (MJ/m²): the physics alone, for the ship's step and the bench (damage/17 §8.2, fire_ref.py's Space).</summary>
    public static BurnEvent Advance(in FireSpaceRow row, ref BurnState x, double aoh, double q, double dt, SimSettings s)
    {
        if (!x.Burning || dt <= 0)
            return BurnEvent.None;
        var ev = BurnEvent.None;
        bool open = row.OpenAir;
        double area = row.AreaM2, room = Room(row, s);
        double qvent = open ? double.PositiveInfinity : s.FireVentMW * aoh;
        double qfo = open ? double.PositiveInfinity : Flashover(room, row.HeightM, aoh, s);
        double fuelRoom = Fuel(room, q, s), lim = Math.Min(fuelRoom, qvent);
        x.T += dt;
        switch (x.Phase)
        {
            case BurnPhase.Grow:
            {
                double a = s.FireAlphaMWs2 * row.Growth * s.FireUseGrowth[Math.Min((int)row.Use, s.FireUseGrowth.Length - 1)];
                x.Q = x.Seat + a * x.T * x.T;
                if (!open && x.Q >= qfo && fuelRoom >= qfo && qvent >= qfo)
                {
                    (x.Phase, x.PhaseS, x.Involved) = (BurnPhase.Developed, 0, room);
                    ev = BurnEvent.Flashover;
                }
                else if (x.Q >= lim)
                {
                    x.Q = lim;
                    x.PhaseS = 0;
                    x.Phase = open ? q >= s.FireOpenSustainMJm2 ? BurnPhase.Developed : BurnPhase.Local
                        : fuelRoom < qfo ? BurnPhase.Local : BurnPhase.Starved;
                    if (x.Phase == BurnPhase.Developed)
                        x.Involved = area;
                }
                else
                    x.Involved = open ? Math.Min(area, x.Q / s.FireOpenMWm2) : room * Math.Clamp(x.Q / qfo, 0.1, 1);
                break;
            }
            case BurnPhase.Local:
                x.Q = lim;
                x.PhaseS += dt;
                if (x.PhaseS > s.FireLocalS || x.E0 - x.E >= s.FireLocalShare * room * q)
                    return Out(ref x, false);
                break;
            case BurnPhase.Starved:
                x.PhaseS += dt;
                x.Q = Math.Max(s.FireSmoulderMW, lim);
                if (qvent >= qfo && fuelRoom >= qfo)
                {
                    // a hole or an opened door lets the air in: it flashes (backdraft)
                    (x.Phase, x.PhaseS, x.Involved) = (BurnPhase.Developed, 0, room);
                    ev = BurnEvent.Flashover;
                }
                else if (x.PhaseS > s.FireStarvedS)
                    return Out(ref x, true);
                break;
            case BurnPhase.Developed:
            case BurnPhase.Decay:
                x.DevS += dt;
                x.Involved = open ? area : Math.Min(area, x.Involved + room * dt / s.FireRoomToRoomS);
                x.Q = Math.Min(qvent, Fuel(x.Involved, q, s));
                if (x.Phase == BurnPhase.Developed && x.E <= s.FireDecayShare * x.E0)
                    x.Phase = BurnPhase.Decay;
                if (x.Phase == BurnPhase.Decay)
                    x.Q *= Math.Max(0.25, x.E / (s.FireDecayShare * x.E0));
                break;
        }
        x.E -= x.Q * dt;
        if (x.E <= 0 || x.Phase == BurnPhase.Decay && x.Q < s.FireSmoulderMW)
            return Out(ref x, x.DevS > s.FireHotS);
        return ev;
    }

    static BurnEvent Out(ref BurnState x, bool hot)
    {
        (x.Phase, x.Q, x.Involved, x.PhaseS) = (BurnPhase.Out, 0, 0, 0);
        if (x.E < 0)
            x.E = 0;
        if (!hot)
            x.DevS = 0;
        return BurnEvent.Out;
    }

    /// <summary>A fire put out (by its crew, or the water over it).</summary>
    static void Douse(ShipClass c, ShipCondition k, int space, SimSettings s)
    {
        Out(ref k.Fire[space], k.Fire[space].DevS > s.FireHotS);
        Mark(c, k, space);
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

    /// <summary>The space's fire load now, MJ/m² (as fitted until the orders before action can strip her, B1).</summary>
    public static double Load(in FireSpaceRow row) => row.LoadMJm2;

    /// <summary>A space's openings' A√H, m^2.5: leakage, its doors (one a cell, two a part's: B1 gives the material
    /// condition) and a cell's holes to the sea (each as a square, A·A^0.25).</summary>
    public static double Vent(ShipClass c, ShipCondition k, int space, SimSettings s)
    {
        ref readonly var row = ref c.FireSpaces[space];
        return row.Kind switch
        {
            FireSpaceKind.Deck => double.PositiveInfinity,
            FireSpaceKind.Part => s.FireLeakAoh + s.FirePartDoors * s.FireDoorAoh,
            _ => s.FireLeakAoh + s.FireCellDoors * s.FireDoorAoh + Math.Pow(k.BreachM2[row.Index], 1.25),
        };
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
