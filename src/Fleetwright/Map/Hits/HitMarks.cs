namespace Fleetwright.Map.Hits;

/// <summary>The marks shells left on each ship (docs/visuals/hits-and-fires.md I3), drawn on her sprite with the part
/// scorches (<see cref="ShipDrawList"/>, mode 4: lit as the scorches, clipped to her by the height map): a ragged dark
/// hole in a scorch, in her frame. At most <see cref="PerShip"/> a ship: past them the oldest goes (by then the parts round
/// it are wrecked and scorched whole). Display only, the map's own memory of the hits it saw. Allocates only for a ship's
/// first mark.</summary>
public sealed class HitMarks
{
    /// <summary>A mark: where on her (her frame, the height over her waterline: the sprite's tilt), its scorch's radius, m,
    /// and the hole's share of it (0: a scorch and a dent, no hole).</summary>
    public readonly record struct Mark(float X, float Y, float Z, float R, float Hole);

    public const int PerShip = 24;

    sealed class Ring
    {
        public readonly Mark[] Marks = new Mark[PerShip];
        public int Next, Count;
    }

    readonly Dictionary<int, Ring> ships = [];

    public void Clear() => ships.Clear();

    public void Add(int entity, in Mark m)
    {
        if (!(m.R > 0))
            return;
        if (!ships.TryGetValue(entity, out var r))
            ships[entity] = r = new Ring();
        r.Marks[r.Next] = m;
        r.Next = (r.Next + 1) % PerShip;
        r.Count = Math.Min(r.Count + 1, PerShip);
    }

    /// <summary>A ship's marks, in no order.</summary>
    public ReadOnlySpan<Mark> Of(int entity) => ships.TryGetValue(entity, out var r) ? r.Marks.AsSpan(0, r.Count) : default;
}
