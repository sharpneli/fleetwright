using System.Collections;
using System.Globalization;
using System.Reflection;
using System.Text;

namespace Fleetwright.HitView;

/// <summary>A prism described for a tooltip or a details pane: its id, kind and heights, then every field of what it
/// was made from (a component, a cell, an armour report) that has a value. Debug text, made when the hover or the
/// selection changes, not per frame.</summary>
public static class HitInfo
{
    static readonly HashSet<string> Skip = ["Id", "Kind", "Points", "Local", "Neighbours", "Rrect", "Footprint", "Base", "Top"];

    public static string Short(HitPrism p) =>
        $"{p.Id}\n{HitKinds.All[p.Kind].Name}, {p.Base:0.##} to {p.Top:0.##} m";

    public static string Full(HitPrism p)
    {
        var sb = new StringBuilder(Short(p));
        if (p.Source is not { } src)
            return sb.ToString();
        foreach (var prop in src.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            if (Skip.Contains(prop.Name) || prop.GetIndexParameters().Length > 0)
                continue;
            var text = Format(prop.GetValue(src));
            if (text != null)
                sb.Append('\n').Append(prop.Name).Append(": ").Append(text);
        }
        return sb.ToString();
    }

    static string? Format(object? v) => v switch
    {
        null => null,
        string s => s.Length == 0 ? null : s,
        double d => d.ToString("0.###", CultureInfo.InvariantCulture),
        float f => f.ToString("0.###", CultureInfo.InvariantCulture),
        IDictionary dict => dict.Count == 0 ? null
            : string.Join(", ", dict.Keys.Cast<object>().Take(8).Select(k => $"{k}={Format(dict[k])}")) + (dict.Count > 8 ? ", ..." : ""),
        IEnumerable e => List(e.Cast<object?>().ToList()),
        _ => Convert.ToString(v, CultureInfo.InvariantCulture),
    };

    static string? List(List<object?> items) => items.Count switch
    {
        0 => null,
        > 8 => $"[{items.Count} items]",
        _ => "[" + string.Join(", ", items.Select(Format)) + "]",
    };
}
