using Fleetwright.Shipgen;
using Fleetwright.Sim.Ships;

namespace Fleetwright.Sim.Combat;

/// <summary>The kinds of battle station a ship's men stand at (Shipgen's <c>Crew.BattleStations</c>, docs/battle/crew.md
/// N0): the gun crews in the mounts, the handling parties under them in the barbettes, the AA and torpedo crews, the
/// engineers, the command party, the directors' and transmitting stations' crews, the steering party, the air group, and
/// the rest as repair men in the quarters and stores. Not an ammunition train's <see cref="StationKind"/>.</summary>
public enum BattleStation : byte
{
    Guns,
    Handling,
    Aa,
    Torpedoes,
    Machinery,
    Command,
    Directors,
    Steering,
    Air,
    Repair,
}

/// <summary>Which battle station each of a ship's crew groups (<see cref="Casualties"/>' groups: the parts', then the
/// cells') is, and her men at each.</summary>
public static class BattleStations
{
    public static readonly string[] Names = ["guns", "handling", "aa", "torpedoes", "machinery", "command", "directors", "steering", "air", "repair"];

    public const int Count = 10;

    /// <summary>A group's station: a part's by its kind (a superstructure by its role: a director's crew, else the command
    /// party's, where a ship with no quarters below also keeps her repair men), a cell's by its room's kind.</summary>
    public static BattleStation Of(ShipClass c, int g)
    {
        if (g < c.Components.Length)
        {
            ref readonly var p = ref c.Components[g];
            return p.Kind switch
            {
                ComponentKind.Main or ComponentKind.Secondary => BattleStation.Guns,
                ComponentKind.Barbette => BattleStation.Handling,
                ComponentKind.Aa => BattleStation.Aa,
                ComponentKind.Torpedo or ComponentKind.TorpedoTube => BattleStation.Torpedoes,
                ComponentKind.Superstructure => p.Role is ComponentRole.Director or ComponentRole.Rangefinder ? BattleStation.Directors : BattleStation.Command,
                ComponentKind.ConningTower => BattleStation.Command,
                ComponentKind.HangarBay or ComponentKind.FlightDeck => BattleStation.Air,
                _ => BattleStation.Repair,
            };
        }
        int room = c.Cells[g - c.Components.Length].Room;
        return room < 0 ? BattleStation.Repair : c.Rooms[room].Kind switch
        {
            RoomKind.BoilerRoom or RoomKind.EngineRoom => BattleStation.Machinery,
            RoomKind.TransmittingStation => BattleStation.Directors,
            RoomKind.Steering => BattleStation.Steering,
            RoomKind.TorpedoRoom => BattleStation.Torpedoes,
            RoomKind.Hangar => BattleStation.Air,
            _ => BattleStation.Repair,
        };
    }

    /// <summary>A ship's men by station, added in: her battle crew at each (<paramref name="men"/>) and those out of action
    /// there now (killed, or wounded and not back: <paramref name="gone"/>; none without a condition).</summary>
    public static void Tally(ShipClass c, ShipCondition? k, Span<double> men, Span<double> gone)
    {
        int groups = c.Components.Length + c.Cells.Length;
        for (int g = 0; g < groups; g++)
        {
            int crew = g < c.Components.Length ? c.Components[g].BattleCrew : c.Cells[g - c.Components.Length].BattleCrew;
            if (crew <= 0)
                continue;
            int s = (int)Of(c, g);
            men[s] += crew;
            gone[s] += k != null && g < k.Gone.Length ? Math.Min(k.Gone[g], crew) : 0;
        }
    }
}
