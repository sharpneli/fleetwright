using System.Numerics;

namespace Fleetwright.HitView;

/// <summary>
/// The kinds a hitbox view tells apart, and how each is drawn (hitview.py's KIND): colour (sRGB 0..1), opacity, and
/// whether its outline is drawn. Kinds above <see cref="SolidAlpha"/> are solid and hide what is behind them; the rest
/// are blended over. The mesh carries only a kind's index; the renderer and the UI read the rest from here.
/// </summary>
public static class HitKinds
{
    public readonly record struct Kind(string Name, Vector3 Colour, float Alpha, bool Filler = false, bool Edges = true)
    {
        public bool Solid => Alpha > SolidAlpha;
    }

    public const float SolidAlpha = 200 / 255f;

    static Kind K(string name, int r, int g, int b, int a, bool filler = false, bool edges = true) =>
        new(name, new Vector3(r, g, b) / 255f, a / 255f, filler, edges);

    /// <summary>Every kind, in index order. Unknown kinds map to <see cref="Other"/>.</summary>
    public static readonly Kind[] All =
    [
        K("hull", 150, 160, 170, 90, edges: false),
        K("main", 215, 70, 70, 255), K("secondary", 230, 150, 60, 255), K("torpedo", 80, 170, 230, 255),
        K("barbette", 200, 200, 205, 255), K("superstructure", 110, 200, 120, 255), K("funnel", 170, 110, 220, 255),
        K("uptake", 150, 100, 200, 200), K("casing", 120, 120, 140, 255), K("aa", 235, 220, 70, 255), K("mast", 180, 150, 110, 255),
        K("conning_tower", 240, 110, 190, 255), K("deck", 150, 175, 150, 255), K("sponson", 140, 150, 160, 255),
        K("flight_deck", 130, 140, 150, 200),
        K("belt", 60, 70, 90, 255), K("strake", 135, 145, 165, 255), K("armour_deck", 60, 70, 90, 110),
        K("boiler_room", 240, 140, 40, 235), K("engine_room", 190, 70, 40, 235), K("bunker", 70, 60, 55, 235),
        K("magazine", 230, 40, 40, 240), K("accommodation", 110, 180, 235, 55, filler: true), K("steering", 160, 90, 200, 235),
        K("hold", 200, 170, 110, 200), K("cargo_tank", 150, 120, 70, 200), K("fuel_tank", 90, 80, 60, 220),
        K("hangar_bay", 200, 200, 210, 120), K("stores", 150, 150, 120, 55, filler: true),
        K("double_bottom", 80, 95, 110, 40, filler: true), K("tds", 90, 140, 160, 45, filler: true),
        K("armoured_bulkhead", 60, 70, 90, 255),
        K("shaft", 225, 225, 120, 255), K("shaft_alley", 200, 190, 120, 150), K("propeller", 215, 160, 60, 255),
        K("rudder", 240, 110, 190, 255),
        // lines only: the waterline and the hull's edges (deck outline, keel, stem and stern, sections)
        K("waterline", 80, 170, 255, 200), K("hull_lines", 200, 210, 220, 160),
        K("other", 200, 200, 200, 255),
    ];

    /// <summary>The index of a kind that isn't in the table.</summary>
    public static readonly int Other = All.Length - 1;

    static readonly Dictionary<string, int> index = All.Select((k, i) => (k.Name, i)).ToDictionary(t => t.Name, t => t.i);

    public static int IndexOf(string name) => index.GetValueOrDefault(name, Other);

    /// <summary>The kinds outside the hull's propulsion that hitview's internal view kept: drawn with the rooms.</summary>
    public static readonly string[] Propulsion = ["shaft", "shaft_alley", "propeller", "rudder"];
}
