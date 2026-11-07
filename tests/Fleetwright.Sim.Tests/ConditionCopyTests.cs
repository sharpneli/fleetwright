using System.Reflection;
using Fleetwright.Sim.Combat;
using Fleetwright.Sim.Ships;

namespace Fleetwright.Sim.Tests;

/// <summary><see cref="ShipCondition.CopyFrom"/> (docs/battle/workers.md W16a): a batch of explosions is worked out on a copy of
/// her condition and handed back the same way, so a field the copy misses is damage lost or state come back from before the hit.
/// Every field is set to something other than a fresh condition's, by reflection, private ones too, so a field added to the class
/// and not to the copy fails here.</summary>
public class ConditionCopyTests
{
    static ShipCondition Fresh(ShipClass c)
    {
        var k = new ShipCondition();
        k.Reset(c);
        return k;
    }

    static IEnumerable<FieldInfo> Fields() =>
        typeof(ShipCondition).GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

    [Theory]
    [InlineData("lion")]
    [InlineData("destroyer")]
    public void Every_field_comes_across_and_no_array_is_shared(string design)
    {
        var c = Fleet.Get(design).Class!;
        var from = Fresh(c);
        var to = Fresh(c);
        var rng = new Random(7);
        foreach (var f in Fields())
        {
            if (f.FieldType.IsArray)
            {
                var a = (Array)f.GetValue(from)!;
                var t = f.FieldType.GetElementType()!;
                for (int i = 0; i < a.Length; i++)
                    a.SetValue(Different(t, a.GetValue(i), rng), i);
            }
            else if (f.FieldType.IsValueType)
                f.SetValue(from, Different(f.FieldType, f.GetValue(from), rng));
        }

        to.CopyFrom(from);

        var missed = new List<string>();
        foreach (var f in Fields())
        {
            object? a = f.GetValue(from), b = f.GetValue(to);
            if (f.FieldType.IsArray)
            {
                Assert.False(ReferenceEquals(a, b) && ((Array)a!).Length > 0, $"{f.Name} is shared, not copied");
                var (x, y) = ((Array)a!, (Array)b!);
                if (x.Length != y.Length || Enumerable.Range(0, x.Length).Any(i => !Equals(x.GetValue(i), y.GetValue(i))))
                    missed.Add(f.Name);
            }
            else if (!Equals(a, b))
                missed.Add(f.Name);
        }
        Assert.True(missed.Count == 0, "not copied: " + string.Join(", ", missed));
    }

    /// <summary>Copying into a condition of another class sets it up for hers first.</summary>
    [Fact]
    public void A_copy_into_another_class_takes_hers()
    {
        var lion = Fleet.Get("lion").Class!;
        var from = Fresh(lion);
        from.WaterM3[3] = 42;
        var to = Fresh(Fleet.Get("destroyer").Class!);
        to.CopyFrom(from);
        Assert.Same(lion, to.Class);
        Assert.Equal(from.WaterM3.Length, to.WaterM3.Length);
        Assert.Equal(42, to.WaterM3[3]);
    }

    /// <summary>A value other than this one, of its type: a struct's every field moved in turn; a reference left as it is (the
    /// settings' rows and the class's tables a condition points at are shared, not hers).</summary>
    static object? Different(Type t, object? now, Random r)
    {
        if (t == typeof(bool))
            return !(bool)now!;
        if (t == typeof(double))
            return double.IsFinite((double)now!) ? (double)now! + 1 + r.NextDouble() : 1.5;
        if (t == typeof(float))
            return float.IsFinite((float)now!) ? (float)now! + 1 + (float)r.NextDouble() : 1.5f;
        if (t.IsEnum)
            return Enum.ToObject(t, Convert.ToInt64(now) + 1);
        if (t.IsPrimitive)
            return Convert.ChangeType(Convert.ToInt64(now) + 1 + r.Next(5), t);
        if (!t.IsValueType)
            return now;
        object boxed = now ?? Activator.CreateInstance(t)!;
        foreach (var f in t.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
            if (f.FieldType.IsValueType)
                f.SetValue(boxed, Different(f.FieldType, f.GetValue(boxed), r));
        return boxed;
    }
}
