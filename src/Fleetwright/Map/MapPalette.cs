using System.Numerics;

namespace Fleetwright.Map;

/// <summary>The map symbols' colours (docs/map-ui/symbols.md Y4), in one place: the design tokens
/// (docs/map-ui/README.md "Design tokens") and the few tints the map adds to them. Colour says whose and what state;
/// the symbol's shape says what it is, its line how sure (the shape review). The battle marks' and effects' own colours
/// (shells, splashes, fire, smoke) stay with them.</summary>
public static class MapPalette
{
    // the design tokens
    public static readonly Vector4 Brass = new(0.851f, 0.722f, 0.408f, 1), Amber = new(0.890f, 0.643f, 0.322f, 1),
        Friend = new(0.357f, 0.608f, 0.878f, 1), Hostile = new(0.886f, 0.408f, 0.361f, 1);

    /// <summary>The selection, everything that is hers on the map (her ring, her route, her squadron's symbol, the ships
    /// picked with her): the selected icon's own amber (<see cref="ShipDrawList.SelectedTint"/>).</summary>
    public static readonly Vector4 Select = new(1f, 0.78f, 0.25f, 0.95f), Picked = Select with { W = 0.6f },
        Follow = Select with { W = 0.45f };

    /// <summary>Own units' symbols and labels (the icons' plain tint), and the line joining a squadron's ships.</summary>
    public static readonly Vector4 Unit = new(0.95f, 0.92f, 0.82f, 0.9f), UnitLine = Unit with { W = 0.25f };

    /// <summary>A ship the player holds out of her line, and one whose superior's orders changed ("!").</summary>
    public static readonly Vector4 Held = new(0.55f, 0.85f, 1f, 0.8f), Asked = new(1f, 0.55f, 0.3f, 1f);

    /// <summary>Routes still to steam (the selection's in <see cref="RouteSelected"/>), the wake's track behind, the
    /// ordered course.</summary>
    public static readonly Vector4 Route = Brass with { W = 0.55f }, RouteSelected = new(1f, 0.8f, 0.3f, 0.95f),
        Track = new(0.85f, 0.9f, 1f, 0.35f), Course = new(1f, 1f, 1f, 0.6f);

    /// <summary>Encounters: the player's own in amber, others' in grey.</summary>
    public static readonly Vector4 OwnEncounter = Amber with { W = 0.8f }, OthersEncounter = new(0.7f, 0.76f, 0.85f, 0.5f);

    /// <summary>Contacts of no known side (the enemy's take <see cref="ShipDrawList.EnemyTint"/>), and smoke on the
    /// horizon.</summary>
    public static readonly Vector4 Unknown = new(0.95f, 0.9f, 0.7f, 1f), Smoke = new(0.62f, 0.58f, 0.52f, 0.85f);

    /// <summary>Fire lines: the selection's in brass, the player's own in friend blue, others' in hostile red.</summary>
    public static readonly Vector4 SelectedFire = Brass with { W = 0.9f }, FriendFire = Friend with { W = 0.5f },
        HostileFire = Hostile with { W = 0.55f };

    /// <summary>Water: a torpedo's reach.</summary>
    public static readonly Vector4 TorpedoReach = new(0.63f, 0.84f, 1f, 0.45f);

    /// <summary>A wreck and its name.</summary>
    public static readonly Vector4 Wreck = new(0.85f, 0.85f, 0.8f, 0.8f);

    // the dev panel's aids: the land's rings and feeler, the horizon, a placement
    public static readonly Vector4 DevLand = new(0.55f, 0.85f, 0.6f, 0.5f), DevSlide = new(0.55f, 1f, 0.6f, 0.95f),
        DevHorizon = new(0.6f, 0.85f, 1f, 0.6f), DevPlace = new(0.6f, 0.9f, 1f, 0.85f);
}
