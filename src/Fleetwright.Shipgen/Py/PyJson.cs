using System.Globalization;
using System.IO.Compression;
using System.Text;

namespace Fleetwright.Shipgen;

/// <summary>A value that writes itself as Python data (geometry kept typed inside the port's dicts).</summary>
public interface IPyValue
{
    object? ToPy();
}

/// <summary>Python's json module for the port's values: numbers with a '.' or an exponent are floats, the rest ints;
/// NaN and Infinity are read and written (allow_nan); objects keep their key order; output is ensure_ascii.</summary>
public static class PyJson
{
    // ------------------------------------------------------------------ reading

    public static object? Parse(string text)
    {
        var p = new Parser(text);
        p.Ws();
        var v = p.Value();
        p.Ws();
        if (p.Pos != text.Length)
            throw new FormatException($"extra data at {p.Pos}");
        return v;
    }

    public static object? Load(string path)
    {
        if (path.EndsWith(".gz", StringComparison.OrdinalIgnoreCase))
        {
            using var fs = File.OpenRead(path);
            using var gz = new GZipStream(fs, CompressionMode.Decompress);
            using var sr = new StreamReader(gz, Encoding.UTF8);
            return Parse(sr.ReadToEnd());
        }
        return Parse(File.ReadAllText(path, Encoding.UTF8));
    }

    sealed class Parser(string s)
    {
        public int Pos;

        public void Ws()
        {
            while (Pos < s.Length && (s[Pos] == ' ' || s[Pos] == '\t' || s[Pos] == '\n' || s[Pos] == '\r'))
                Pos++;
        }

        public object? Value()
        {
            if (Pos >= s.Length)
                throw new FormatException("unexpected end of JSON");
            char c = s[Pos];
            switch (c)
            {
                case '{':
                    {
                        Pos++;
                        var d = new PyDict();
                        Ws();
                        if (s[Pos] == '}')
                        {
                            Pos++;
                            return d;
                        }
                        while (true)
                        {
                            Ws();
                            string k = Str();
                            Ws();
                            Expect(':');
                            Ws();
                            d[k] = Value();
                            Ws();
                            if (s[Pos] == ',')
                            {
                                Pos++;
                                continue;
                            }
                            Expect('}');
                            return d;
                        }
                    }
                case '[':
                    {
                        Pos++;
                        var l = new List<object?>();
                        Ws();
                        if (s[Pos] == ']')
                        {
                            Pos++;
                            return l;
                        }
                        while (true)
                        {
                            Ws();
                            l.Add(Value());
                            Ws();
                            if (s[Pos] == ',')
                            {
                                Pos++;
                                continue;
                            }
                            Expect(']');
                            return l;
                        }
                    }
                case '"':
                    return Str();
                case 't':
                    Word("true");
                    return true;
                case 'f':
                    Word("false");
                    return false;
                case 'n':
                    Word("null");
                    return null;
                case 'N':
                    Word("NaN");
                    return double.NaN;
                case 'I':
                    Word("Infinity");
                    return double.PositiveInfinity;
                default:
                    return Number();
            }
        }

        void Expect(char c)
        {
            if (Pos >= s.Length || s[Pos] != c)
                throw new FormatException($"expected '{c}' at {Pos}");
            Pos++;
        }

        void Word(string w)
        {
            if (string.CompareOrdinal(s, Pos, w, 0, w.Length) != 0)
                throw new FormatException($"expected {w} at {Pos}");
            Pos += w.Length;
        }

        object Number()
        {
            int start = Pos;
            if (s[Pos] == '-')
            {
                Pos++;
                if (Pos < s.Length && s[Pos] == 'I')
                {
                    Word("Infinity");
                    return double.NegativeInfinity;
                }
            }
            bool isFloat = false;
            while (Pos < s.Length)
            {
                char c = s[Pos];
                if (c >= '0' && c <= '9' || c == '-' || c == '+')
                    Pos++;
                else if (c == '.' || c == 'e' || c == 'E')
                {
                    isFloat = true;
                    Pos++;
                }
                else
                    break;
            }
            var tok = s.AsSpan(start, Pos - start);
            if (tok.Length == 0)
                throw new FormatException($"bad JSON value at {start}");
            if (isFloat)
                return double.Parse(tok, NumberStyles.Float, CultureInfo.InvariantCulture);
            return long.Parse(tok, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture);
        }

        string Str()
        {
            Expect('"');
            var sb = new StringBuilder();
            while (true)
            {
                char c = s[Pos++];
                if (c == '"')
                    return sb.ToString();
                if (c != '\\')
                {
                    sb.Append(c);
                    continue;
                }
                char e = s[Pos++];
                switch (e)
                {
                    case '"': sb.Append('"'); break;
                    case '\\': sb.Append('\\'); break;
                    case '/': sb.Append('/'); break;
                    case 'b': sb.Append('\b'); break;
                    case 'f': sb.Append('\f'); break;
                    case 'n': sb.Append('\n'); break;
                    case 'r': sb.Append('\r'); break;
                    case 't': sb.Append('\t'); break;
                    case 'u':
                        sb.Append((char)int.Parse(s.AsSpan(Pos, 4), NumberStyles.HexNumber, CultureInfo.InvariantCulture));
                        Pos += 4;
                        break;
                    default: throw new FormatException($"bad escape at {Pos}");
                }
            }
        }
    }

    // ------------------------------------------------------------------ writing

    /// <summary>json.dumps(v, indent=indent) (indent null: one line with ", " and ": ").</summary>
    public static string Dumps(object? v, int? indent = 1)
    {
        var sb = new StringBuilder();
        Write(sb, v, indent, 0);
        return sb.ToString();
    }

    public static void Save(string path, object? v, int? indent = 1)
    {
        string text = Dumps(v, indent);
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        if (path.EndsWith(".gz", StringComparison.OrdinalIgnoreCase))
        {
            using var fs = File.Create(path);
            using var gz = new GZipStream(fs, CompressionLevel.SmallestSize);
            var bytes = Encoding.UTF8.GetBytes(text);
            gz.Write(bytes, 0, bytes.Length);
        }
        else
            File.WriteAllText(path, text, new UTF8Encoding(false));
    }

    static void Newline(StringBuilder sb, int? indent, int level)
    {
        if (indent is int n)
            sb.Append('\n').Append(' ', n * level);
    }

    static void Write(StringBuilder sb, object? v, int? indent, int level)
    {
        switch (v)
        {
            case null: sb.Append("null"); return;
            case bool b: sb.Append(b ? "true" : "false"); return;
            case long l: sb.Append(l.ToString(CultureInfo.InvariantCulture)); return;
            case int i: sb.Append(i.ToString(CultureInfo.InvariantCulture)); return;
            case double d:
                sb.Append(double.IsNaN(d) ? "NaN" : double.IsPositiveInfinity(d) ? "Infinity"
                    : double.IsNegativeInfinity(d) ? "-Infinity" : Py.FloatRepr(d));
                return;
            case string s: WriteStr(sb, s); return;
            case IPyValue pv: Write(sb, pv.ToPy(), indent, level); return;
            case PyDict p:
                {
                    if (p.Count == 0)
                    {
                        sb.Append("{}");
                        return;
                    }
                    sb.Append('{');
                    bool first = true;
                    foreach (var kv in p)
                    {
                        if (!first)
                            sb.Append(indent is null ? ", " : ",");
                        first = false;
                        Newline(sb, indent, level + 1);
                        WriteStr(sb, kv.Key);
                        sb.Append(": ");
                        Write(sb, kv.Value, indent, level + 1);
                    }
                    Newline(sb, indent, level);
                    sb.Append('}');
                    return;
                }
            case System.Collections.IEnumerable e:
                {
                    var items = new List<object?>();
                    foreach (var x in e)
                        items.Add(x);
                    if (items.Count == 0)
                    {
                        sb.Append("[]");
                        return;
                    }
                    sb.Append('[');
                    for (int i = 0; i < items.Count; i++)
                    {
                        if (i > 0)
                            sb.Append(indent is null ? ", " : ",");
                        Newline(sb, indent, level + 1);
                        Write(sb, items[i], indent, level + 1);
                    }
                    Newline(sb, indent, level);
                    sb.Append(']');
                    return;
                }
            default:
                throw new PyTypeError($"Object of type {v.GetType().Name} is not JSON serializable");
        }
    }

    static void WriteStr(StringBuilder sb, string s)
    {
        sb.Append('"');
        foreach (char c in s)
        {
            switch (c)
            {
                case '"': sb.Append("\\\""); break;
                case '\\': sb.Append("\\\\"); break;
                case '\n': sb.Append("\\n"); break;
                case '\r': sb.Append("\\r"); break;
                case '\t': sb.Append("\\t"); break;
                case '\b': sb.Append("\\b"); break;
                case '\f': sb.Append("\\f"); break;
                default:
                    if (c < 0x20 || c > 0x7e)
                        sb.Append("\\u").Append(((int)c).ToString("x4", CultureInfo.InvariantCulture));
                    else
                        sb.Append(c);
                    break;
            }
        }
        sb.Append('"');
    }

    /// <summary>A deep copy through JSON's types: dicts, lists (tuples become lists), typed values as their Python
    /// form. What json.loads(json.dumps(v)) gives.</summary>
    public static object? Plain(object? v) => v switch
    {
        null or bool or long or double or string => v,
        int i => (long)i,
        IPyValue pv => Plain(pv.ToPy()),
        PyDict p => PlainDict(p),
        System.Collections.IEnumerable e => PlainList(e),
        _ => throw new PyTypeError($"Object of type {v.GetType().Name} is not JSON serializable"),
    };

    static PyDict PlainDict(PyDict p)
    {
        var o = new PyDict();
        foreach (var kv in p)
            o[kv.Key] = Plain(kv.Value);
        return o;
    }

    static List<object?> PlainList(System.Collections.IEnumerable e)
    {
        var l = new List<object?>();
        foreach (var x in e)
            l.Add(Plain(x));
        return l;
    }
}
