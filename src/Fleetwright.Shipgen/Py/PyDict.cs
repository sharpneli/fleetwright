using System.Collections;

namespace Fleetwright.Shipgen;

/// <summary>A Python dict with string keys: insertion-ordered (a re-added key goes to the end), values as the port's
/// Python values (<see cref="Py"/>: long, double, bool, string, null, PyDict, lists and typed geometry).</summary>
public sealed class PyDict : IEnumerable<KeyValuePair<string, object?>>
{
    readonly OrderedDictionary<string, object?> d;

    public PyDict() => d = new OrderedDictionary<string, object?>(StringComparer.Ordinal);

    PyDict(int capacity) => d = new OrderedDictionary<string, object?>(capacity, StringComparer.Ordinal);

    /// <summary>dict(k=v, ...): the pairs in order; a repeated key keeps its first place and its last value.</summary>
    public static PyDict Of(params (string Key, object? Value)[] items)
    {
        var p = new PyDict(items.Length);
        foreach (var (k, v) in items)
            p.d[k] = v;
        return p;
    }

    /// <summary>{**a, **b, ...}: later dicts win, keys in first-seen order. Nulls are skipped.</summary>
    public static PyDict Merge(params PyDict?[] dicts)
    {
        var p = new PyDict();
        foreach (var x in dicts)
            if (x != null)
                foreach (var kv in x.d)
                    p.d[kv.Key] = kv.Value;
        return p;
    }

    public int Count => d.Count;
    public IEnumerable<string> Keys => d.Keys;
    public IEnumerable<object?> Values => d.Values;

    public object? this[string key]
    {
        get => d.TryGetValue(key, out var v) ? v : throw new PyKeyError(key);
        set => d[key] = value;
    }

    public bool Has(string key) => d.ContainsKey(key);

    /// <summary>d.get(key, def): def only when the key is missing.</summary>
    public object? Get(string key, object? def = null) => d.TryGetValue(key, out var v) ? v : def;

    /// <summary>d.get(key) or def: def when missing or falsy.</summary>
    public object? Or(string key, object? def) => d.TryGetValue(key, out var v) && Py.Truthy(v) ? v : def;

    public double F(string key) => Py.ToDouble(this[key]);
    public double F(string key, double def) => d.TryGetValue(key, out var v) ? Py.ToDouble(v) : def;
    public long I(string key) => Py.ToLong(this[key]);
    public long I(string key, long def) => d.TryGetValue(key, out var v) ? Py.ToLong(v) : def;
    public string S(string key) => (string)this[key]!;
    public string? S(string key, string? def) => d.TryGetValue(key, out var v) ? (string?)v : def;
    public PyDict D(string key) => (PyDict)this[key]!;
    /// <summary>d.get(key) or {}.</summary>
    public PyDict DOr(string key) => d.TryGetValue(key, out var v) && Py.Truthy(v) ? (PyDict)v! : new PyDict();
    public List<object?> L(string key) => (List<object?>)this[key]!;
    /// <summary>bool(d.get(key)).</summary>
    public bool B(string key) => d.TryGetValue(key, out var v) && Py.Truthy(v);

    /// <summary>d.setdefault(key, def).</summary>
    public object? SetDefault(string key, object? def)
    {
        if (d.TryGetValue(key, out var v))
            return v;
        d[key] = def;
        return def;
    }

    public bool Remove(string key) => d.Remove(key);

    /// <summary>d.pop(key[, def]): KeyError without a default.</summary>
    public object? Pop(string key)
    {
        if (!d.Remove(key, out var v))
            throw new PyKeyError(key);
        return v;
    }

    public object? Pop(string key, object? def) => d.Remove(key, out var v) ? v : def;

    /// <summary>d.update(k=v, ...): existing keys keep their place.</summary>
    public PyDict Update(params (string Key, object? Value)[] items)
    {
        foreach (var (k, v) in items)
            d[k] = v;
        return this;
    }

    public PyDict Update(PyDict other)
    {
        foreach (var kv in other.d)
            d[kv.Key] = kv.Value;
        return this;
    }

    /// <summary>dict(d): a shallow copy.</summary>
    public PyDict Copy()
    {
        var p = new PyDict(d.Count);
        foreach (var kv in d)
            p.d[kv.Key] = kv.Value;
        return p;
    }

    /// <summary>{k: v for k, v in d.items() if keep(k)}.</summary>
    public PyDict Where(Func<string, bool> keep)
    {
        var p = new PyDict();
        foreach (var kv in d)
            if (keep(kv.Key))
                p.d[kv.Key] = kv.Value;
        return p;
    }

    public IEnumerator<KeyValuePair<string, object?>> GetEnumerator() => d.GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    public override string ToString() => Py.Repr(this);
}

/// <summary>Python's KeyError: str() of it is the key's repr.</summary>
public sealed class PyKeyError(object key) : Exception(Py.Repr(key))
{
    public object Key { get; } = key;
}

/// <summary>Python's ValueError.</summary>
public sealed class PyValueError(string message) : Exception(message);

/// <summary>Python's TypeError.</summary>
public sealed class PyTypeError(string message) : Exception(message);
