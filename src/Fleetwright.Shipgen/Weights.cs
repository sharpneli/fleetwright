namespace Fleetwright.Shipgen;

/// <summary>A weight's height rule: ("frac", k) is k x D above the keel, ("deck", h) is D + h.</summary>
public readonly record struct ZRel(string Kind, double V)
{
    public static ZRel Frac(double k) => new("frac", k);
    public static ZRel Deck(double h) => new("deck", h);
}

/// <summary>weights: the Weight record every design module books its tonnes in (navarch.solve sums them), and the
/// densities they are weighed with.</summary>
public sealed class Weight
{
    public const double Steel = 7.85;      // t/m^3
    public const double Seawater = 1.025;  // t/m^3

    public string Name;
    public string Group;
    public double W;
    public double X;
    public double? Z;          // absolute height above keel, set once D is known
    public ZRel ZRel;

    public Weight(string name, string group, double w, double x = 0.0, ZRel? zRel = null)
    {
        Name = name;
        Group = group;
        W = w;
        X = x;
        ZRel = zRel ?? ZRel.Frac(0.5);
    }

    public Weight Clone() => new(Name, Group, W, X, ZRel) { Z = Z };
}
