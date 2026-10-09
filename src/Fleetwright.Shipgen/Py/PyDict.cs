using System.Collections;

namespace Fleetwright.Shipgen;

/// <summary>A Python dict with string keys: insertion-ordered (a re-added key goes to the end), values as the port's
/// Python values (<see cref="Py"/>: long, double, bool, string, null, PyDict, lists and typed geometry). Small dicts
/// (nearly all of them) are searched linearly; a hash index is built past IndexFrom keys.</summary>
public sealed class PyDict : IEnumerable<KeyValuePair<string, object?>>
{
    const int IndexFrom = 32;
    string[] keys;
    object?[] values;
    int count;
    Dictionary<string, int>? index;

    public PyDict() : this(4)
    {
    }

    PyDict(int capacity)
    {
        keys = new string[Math.Max(capacity, 1)];
        values = new object?[Math.Max(capacity, 1)];
    }

    /// <summary>dict(k=v, ...): the pairs in order; a repeated key keeps its first place and its last value.</summary>
    public static PyDict Of(params (string Key, object? Value)[] items)
    {
        var p = new PyDict(items.Length);
        foreach (var (k, v) in items)
            p[k] = v;
        return p;
    }

    /// <summary>{**a, **b, ...}: later dicts win, keys in first-seen order. Nulls are skipped.</summary>
    public static PyDict Merge(params PyDict?[] dicts)
    {
        int n = 0;
        foreach (var x in dicts)
            n += x?.count ?? 0;
        var p = new PyDict(n);
        foreach (var x in dicts)
            if (x != null)
                for (int i = 0; i < x.count; i++)
                    p[x.keys[i]] = x.values[i];
        return p;
    }

    public int Count => count;

    public IEnumerable<string> Keys
    {
        get
        {
            for (int i = 0; i < count; i++)
                yield return keys[i];
        }
    }

    public IEnumerable<object?> Values
    {
        get
        {
            for (int i = 0; i < count; i++)
                yield return values[i];
        }
    }

    int Find(string key)
    {
        if (index != null)
            return index.TryGetValue(key, out int j) ? j : -1;
        for (int i = 0; i < count; i++)
            if (string.Equals(keys[i], key, StringComparison.Ordinal))
                return i;
        return -1;
    }

    void Reindex()
    {
        if (count < IndexFrom)
        {
            index = null;
            return;
        }
        index = new Dictionary<string, int>(count * 2, StringComparer.Ordinal);
        for (int i = 0; i < count; i++)
            index[keys[i]] = i;
    }

    public object? this[string key]
    {
        get
        {
            int i = Find(key);
            return i >= 0 ? values[i] : throw new PyKeyError(key);
        }
        set
        {
            int i = Find(key);
            if (i >= 0)
            {
                values[i] = value;
                return;
            }
            if (count == keys.Length)
            {
                Array.Resize(ref keys, keys.Length * 2);
                Array.Resize(ref values, values.Length * 2);
            }
            keys[count] = key;
            values[count] = value;
            count++;
            if (index != null)
                index[key] = count - 1;
            else if (count >= IndexFrom)
                Reindex();
        }
    }

    public bool Has(string key) => Find(key) >= 0;

    bool TryGet(string key, out object? v)
    {
        int i = Find(key);
        v = i >= 0 ? values[i] : null;
        return i >= 0;
    }

    /// <summary>d.get(key, def): def only when the key is missing.</summary>
    public object? Get(string key, object? def = null) => TryGet(key, out var v) ? v : def;

    /// <summary>d.get(key) or def: def when missing or falsy.</summary>
    public object? Or(string key, object? def) => TryGet(key, out var v) && Py.Truthy(v) ? v : def;

    public double F(string key) => Py.ToDouble(this[key]);
    public double F(string key, double def) => TryGet(key, out var v) ? Py.ToDouble(v) : def;
    public long I(string key) => Py.ToLong(this[key]);
    public long I(string key, long def) => TryGet(key, out var v) ? Py.ToLong(v) : def;
    public string S(string key) => (string)this[key]!;
    public string? S(string key, string? def) => TryGet(key, out var v) ? (string?)v : def;
    public PyDict D(string key) => (PyDict)this[key]!;
    /// <summary>d.get(key) or {}.</summary>
    public PyDict DOr(string key) => TryGet(key, out var v) && Py.Truthy(v) ? (PyDict)v! : new PyDict();
    public List<object?> L(string key) => (List<object?>)this[key]!;
    /// <summary>bool(d.get(key)).</summary>
    public bool B(string key) => TryGet(key, out var v) && Py.Truthy(v);

    /// <summary>d.setdefault(key, def).</summary>
    public object? SetDefault(string key, object? def)
    {
        if (TryGet(key, out var v))
            return v;
        this[key] = def;
        return def;
    }

    public bool Remove(string key)
    {
        int i = Find(key);
        if (i < 0)
            return false;
        Array.Copy(keys, i + 1, keys, i, count - i - 1);
        Array.Copy(values, i + 1, values, i, count - i - 1);
        count--;
        keys[count] = null!;
        values[count] = null;
        if (index != null)
            Reindex();
        return true;
    }

    /// <summary>d.pop(key[, def]): KeyError without a default.</summary>
    public object? Pop(string key)
    {
        if (!TryGet(key, out var v))
            throw new PyKeyError(key);
        Remove(key);
        return v;
    }

    public object? Pop(string key, object? def)
    {
        if (!TryGet(key, out var v))
            return def;
        Remove(key);
        return v;
    }

    /// <summary>d.update(k=v, ...): existing keys keep their place.</summary>
    public PyDict Update(params (string Key, object? Value)[] items)
    {
        foreach (var (k, v) in items)
            this[k] = v;
        return this;
    }

    public PyDict Update(PyDict other)
    {
        for (int i = 0; i < other.count; i++)
            this[other.keys[i]] = other.values[i];
        return this;
    }

    /// <summary>dict(d): a shallow copy.</summary>
    public PyDict Copy()
    {
        var p = new PyDict(count);
        Array.Copy(keys, p.keys, count);
        Array.Copy(values, p.values, count);
        p.count = count;
        p.Reindex();
        return p;
    }

    /// <summary>{k: v for k, v in d.items() if keep(k)}.</summary>
    public PyDict Where(Func<string, bool> keep)
    {
        var p = new PyDict(count);
        for (int i = 0; i < count; i++)
            if (keep(keys[i]))
                p[keys[i]] = values[i];
        return p;
    }

    public IEnumerator<KeyValuePair<string, object?>> GetEnumerator()
    {
        for (int i = 0; i < count; i++)
            yield return new KeyValuePair<string, object?>(keys[i], values[i]);
    }

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
