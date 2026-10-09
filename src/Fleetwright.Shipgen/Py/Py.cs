using System.Globalization;
using System.Numerics;
using System.Text;

namespace Fleetwright.Shipgen;

/// <summary>Python semantics the port relies on, bit for bit where they reach the output: truthiness, equality, repr,
/// CPython's sum (Neumaier), hypot/dist (vector_norm), gamma (Lanczos), round, floor division and modulo, and min/max
/// (the first of equals wins; NaN and signed zeros as Python has them). Python values are long (int), double (float),
/// bool, string, null (None), PyDict, List&lt;object?&gt; (list) and object?[] (tuple).</summary>
public static class Py
{
    // ------------------------------------------------------------------ values

    public static bool Truthy(object? v) => v switch
    {
        null => false,
        bool b => b,
        long l => l != 0,
        int i => i != 0,
        double d => d != 0.0,
        string s => s.Length > 0,
        PyDict p => p.Count > 0,
        System.Collections.ICollection c => c.Count > 0,
        _ => true,
    };

    /// <summary>isinstance(v, int): bool is an int in Python.</summary>
    public static bool IsInt(object? v) => v is long or int or bool;

    /// <summary>isinstance(v, int) and not isinstance(v, bool).</summary>
    public static bool IsIntNotBool(object? v) => v is long or int;

    /// <summary>isinstance(v, (int, float)), bools included.</summary>
    public static bool IsNumber(object? v) => v is long or int or double or bool;

    public static double ToDouble(object? v) => v switch
    {
        double d => d,
        long l => l,
        int i => i,
        bool b => b ? 1.0 : 0.0,
        null => throw new PyTypeError("unsupported operand type(s): 'NoneType'"),
        _ => throw new PyTypeError($"not a number: {Repr(v)}"),
    };

    public static long ToLong(object? v) => v switch
    {
        long l => l,
        int i => i,
        bool b => b ? 1 : 0,
        double d => (long)d,
        _ => throw new PyTypeError($"not an int: {Repr(v)}"),
    };

    /// <summary>a + b for Python numbers: an int if both are ints (bools count as ints), else a float.</summary>
    public static object Add(object? a, object? b) =>
        a is double || b is double ? ToDouble(a) + ToDouble(b) : checked(ToLong(a) + ToLong(b));

    /// <summary>a or b.</summary>
    public static object? Or(object? a, object? b) => Truthy(a) ? a : b;

    /// <summary>Python ==.</summary>
    public static bool Eq(object? a, object? b)
    {
        if (ReferenceEquals(a, b))
            return true;
        if (a is null || b is null)
            return false;
        if (IsNumber(a) && IsNumber(b))
        {
            if (a is double || b is double)
                return ToDouble(a) == ToDouble(b);
            return ToLong(a) == ToLong(b);
        }
        if (a is string sa)
            return b is string sb && string.Equals(sa, sb, StringComparison.Ordinal);
        if (a is PyDict da)
        {
            if (b is not PyDict db || da.Count != db.Count)
                return false;
            foreach (var kv in da)
                if (!db.Has(kv.Key) || !Eq(kv.Value, db[kv.Key]))
                    return false;
            return true;
        }
        if (a is System.Collections.IList la && b is System.Collections.IList lb && (a is object?[]) == (b is object?[]))
        {
            if (la.Count != lb.Count)
                return false;
            for (int i = 0; i < la.Count; i++)
                if (!Eq(la[i], lb[i]))
                    return false;
            return true;
        }
        return a.Equals(b);
    }

    /// <summary>x in seq (Python ==).</summary>
    public static bool In(object? x, params object?[] seq)
    {
        foreach (var s in seq)
            if (Eq(x, s))
                return true;
        return false;
    }

    // ------------------------------------------------------------------ repr / str

    public static string Str(object? v) => v is string s ? s : Repr(v);

    public static string Repr(object? v)
    {
        switch (v)
        {
            case null: return "None";
            case bool b: return b ? "True" : "False";
            case long l: return l.ToString(CultureInfo.InvariantCulture);
            case int i: return i.ToString(CultureInfo.InvariantCulture);
            case double d: return FloatRepr(d);
            case string s: return StrRepr(s);
            case PyDict p:
                {
                    var sb = new StringBuilder("{");
                    bool first = true;
                    foreach (var kv in p)
                    {
                        if (!first)
                            sb.Append(", ");
                        first = false;
                        sb.Append(StrRepr(kv.Key)).Append(": ").Append(Repr(kv.Value));
                    }
                    return sb.Append('}').ToString();
                }
            case object?[] t:
                return t.Length == 1 ? $"({Repr(t[0])},)" : "(" + string.Join(", ", t.Select(Repr)) + ")";
            case System.Collections.IEnumerable e:
                {
                    var parts = new List<string>();
                    foreach (var x in e)
                        parts.Add(Repr(x));
                    return "[" + string.Join(", ", parts) + "]";
                }
            default: return v.ToString() ?? "";
        }
    }

    /// <summary>repr(str): single quotes unless the string holds a single quote and no double quote.</summary>
    public static string StrRepr(string s)
    {
        char q = s.Contains('\'') && !s.Contains('"') ? '"' : '\'';
        var sb = new StringBuilder();
        sb.Append(q);
        foreach (char c in s)
        {
            if (c == q || c == '\\')
                sb.Append('\\').Append(c);
            else if (c == '\n')
                sb.Append("\\n");
            else if (c == '\r')
                sb.Append("\\r");
            else if (c == '\t')
                sb.Append("\\t");
            else if (c < 0x20 || c == 0x7f)
                sb.Append("\\x").Append(((int)c).ToString("x2", CultureInfo.InvariantCulture));
            else
                sb.Append(c);
        }
        return sb.Append(q).ToString();
    }

    /// <summary>repr(float): the shortest round-tripping digits, fixed for 1e-4 &lt;= |x| &lt; 1e16, else exponential.</summary>
    public static string FloatRepr(double x)
    {
        if (double.IsNaN(x))
            return "nan";
        if (double.IsInfinity(x))
            return x > 0 ? "inf" : "-inf";
        if (x == 0)
            return double.IsNegative(x) ? "-0.0" : "0.0";
        var (neg, digits, decpt) = ShortestDigits(x);
        string sign = neg ? "-" : "";
        if (decpt > -4 && decpt <= 16)
        {
            if (decpt <= 0)
                return sign + "0." + new string('0', -decpt) + digits;
            if (decpt >= digits.Length)
                return sign + digits + new string('0', decpt - digits.Length) + ".0";
            return sign + digits[..decpt] + "." + digits[decpt..];
        }
        int e = decpt - 1;
        string mant = digits.Length == 1 ? digits : digits[0] + "." + digits[1..];
        return sign + mant + "e" + (e < 0 ? "-" : "+") + Math.Abs(e).ToString("00", CultureInfo.InvariantCulture);
    }

    /// <summary>The shortest round-tripping decimal digits of x (non-zero, finite): x = 0.digits x 10^decpt.</summary>
    static (bool Neg, string Digits, int DecPt) ShortestDigits(double x)
    {
        // .NET Core 3.0+ "R" gives the shortest round-trip string; read its digits and exponent
        string s = Math.Abs(x).ToString("R", CultureInfo.InvariantCulture);
        int exp = 0;
        int ei = s.IndexOfAny(['E', 'e']);
        if (ei >= 0)
        {
            exp = int.Parse(s[(ei + 1)..], NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture);
            s = s[..ei];
        }
        int dot = s.IndexOf('.');
        string intPart = dot >= 0 ? s[..dot] : s;
        string frac = dot >= 0 ? s[(dot + 1)..] : "";
        string all = intPart + frac;
        int decpt = intPart.Length + exp;
        int lead = 0;
        while (lead < all.Length - 1 && all[lead] == '0')
            lead++;
        all = all[lead..];
        decpt -= lead;
        all = all.TrimEnd('0');
        if (all.Length == 0)
            all = "0";
        return (x < 0, all, decpt);
    }

    // ------------------------------------------------------------------ exact decimal rounding

    /// <summary>x = (-1)^neg m 2^e exactly.</summary>
    static (bool Neg, BigInteger M, int E) Decompose(double x)
    {
        long bits = BitConverter.DoubleToInt64Bits(x);
        bool neg = bits < 0;
        int ex = (int)((bits >> 52) & 0x7FF);
        long man = bits & 0xFFFFFFFFFFFFFL;
        if (ex == 0)
            ex = 1;
        else
            man |= 1L << 52;
        return (neg, man, ex - 1075);
    }

    /// <summary>round-half-even(|x| x 10^n) as an integer, exactly (n may be negative).</summary>
    static BigInteger ScaledRound(double x, int n)
    {
        var (_, m, e) = Decompose(x);
        BigInteger num = m, den = BigInteger.One;
        if (e >= 0)
            num <<= e;
        else
            den <<= -e;
        if (n >= 0)
            num *= BigInteger.Pow(10, n);
        else
            den *= BigInteger.Pow(10, -n);
        var q = BigInteger.DivRem(num, den, out var rem);
        var twice = rem * 2;
        int c = twice.CompareTo(den);
        if (c > 0 || (c == 0 && !q.IsEven))
            q += 1;
        return q;
    }

    /// <summary>round(x): half to even, an int.</summary>
    public static long Round(double x)
    {
        if (!double.IsFinite(x))
            throw new OverflowException("cannot convert float infinity or NaN to integer");
        return (long)Math.Round(x, MidpointRounding.ToEven);
    }

    /// <summary>round(x, n) for a float: the nearest float to x correctly rounded to n decimals (half to even).</summary>
    public static double Round(double x, int n)
    {
        if (!double.IsFinite(x) || x == 0.0)
            return x;
        if (n > 323)
            return x;
        if (n < -308)
            return 0.0 * x;
        var k = ScaledRound(x, n);
        double r = double.Parse(k.ToString(CultureInfo.InvariantCulture) + "E" + (-n).ToString(CultureInfo.InvariantCulture),
            NumberStyles.Float, CultureInfo.InvariantCulture);
        return x < 0 ? -r : r;
    }

    /// <summary>round(v, n) for a Python number: an int stays an int (rounded for n &lt; 0).</summary>
    public static object RoundObj(object? v, int n)
    {
        if (v is double d)
            return Round(d, n);
        long l = ToLong(v);
        if (n >= 0)
            return l;
        var p = BigInteger.Pow(10, -n);
        var q = BigInteger.DivRem(BigInteger.Abs(l), p, out var rem);
        int c = (rem * 2).CompareTo(p);
        if (c > 0 || (c == 0 && !q.IsEven))
            q += 1;
        return (long)(q * p) * Math.Sign(l);
    }

    /// <summary>round(v) for a Python number: an int.</summary>
    public static long RoundObj(object? v) => v is double d ? Round(d) : ToLong(v);

    // ------------------------------------------------------------------ format specs

    static readonly NumberFormatInfo Nfi = CultureInfo.InvariantCulture.NumberFormat;

    /// <summary>format(x, ".nf") with optional "," grouping and "+" sign.</summary>
    public static string F(double x, int n, bool comma = false, bool plus = false)
    {
        if (double.IsNaN(x))
            return (plus ? "+" : "") + "nan";
        if (double.IsInfinity(x))
            return x > 0 ? (plus ? "+inf" : "inf") : "-inf";
        var k = ScaledRound(x, n);
        string digits = k.ToString(CultureInfo.InvariantCulture);
        if (digits.Length <= n)
            digits = new string('0', n - digits.Length + 1) + digits;
        string ip = digits[..(digits.Length - n)];
        string fp = digits[(digits.Length - n)..];
        if (comma)
            ip = Group(ip);
        string sign = double.IsNegative(x) ? "-" : plus ? "+" : "";
        return sign + ip + (n > 0 ? "." + fp : "");
    }

    /// <summary>format(v, ".nf") for a Python number (an int formats as its float).</summary>
    public static string F(object? v, int n, bool comma = false, bool plus = false) => F(ToDouble(v), n, comma, plus);

    /// <summary>format(n, ","): an int with thousands separators.</summary>
    public static string Comma(long n) => (n < 0 ? "-" : "") + Group(Math.Abs(n).ToString(CultureInfo.InvariantCulture));

    static string Group(string ip)
    {
        var sb = new StringBuilder();
        int first = ip.Length % 3;
        if (first == 0)
            first = 3;
        sb.Append(ip, 0, Math.Min(first, ip.Length));
        for (int i = first; i < ip.Length; i += 3)
            sb.Append(',').Append(ip, i, 3);
        return sb.ToString();
    }

    /// <summary>format(x, "g"): 6 significant digits, trailing zeros dropped, exponential below 1e-4 and from 1e6.</summary>
    public static string G(double x, int p = 6)
    {
        if (double.IsNaN(x))
            return "nan";
        if (double.IsInfinity(x))
            return x > 0 ? "inf" : "-inf";
        string sign = double.IsNegative(x) ? "-" : "";
        if (x == 0)
            return sign + "0";
        double ax = Math.Abs(x);
        int e = (int)Math.Floor(Math.Log10(ax));
        // exact: 10^e <= ax < 10^(e+1)
        while (CompareToPow10(ax, e) < 0)
            e--;
        while (CompareToPow10(ax, e + 1) >= 0)
            e++;
        var k = ScaledRound(ax, p - 1 - e);
        if (k >= BigInteger.Pow(10, p))
        {
            k /= 10;
            e++;
        }
        string digits = k.ToString(CultureInfo.InvariantCulture).PadLeft(p, '0');
        if (e >= -4 && e < p)
        {
            string s;
            if (e >= 0)
                s = digits[..(e + 1)] + "." + digits[(e + 1)..];
            else
                s = "0." + new string('0', -e - 1) + digits;
            if (s.Contains('.'))
                s = s.TrimEnd('0').TrimEnd('.');
            return sign + s;
        }
        string m = (digits[0] + "." + digits[1..]).TrimEnd('0').TrimEnd('.');
        return sign + m + "e" + (e < 0 ? "-" : "+") + Math.Abs(e).ToString("00", CultureInfo.InvariantCulture);
    }

    public static string G(object? v) => G(ToDouble(v));

    /// <summary>Sign of ax - 10^e, exactly.</summary>
    static int CompareToPow10(double ax, int e)
    {
        var (_, m, be) = Decompose(ax);
        BigInteger lhs = m, rhs = BigInteger.One;
        if (be >= 0)
            lhs <<= be;
        else
            rhs <<= -be;
        if (e >= 0)
            rhs *= BigInteger.Pow(10, e);
        else
            lhs *= BigInteger.Pow(10, -e);
        return lhs.CompareTo(rhs);
    }

    // ------------------------------------------------------------------ arithmetic

    /// <summary>x // y for floats (CPython float_floor_div).</summary>
    public static double FloorDiv(double vx, double wx)
    {
        if (wx == 0.0)
            throw new DivideByZeroException("float floor division by zero");
        double mod = vx % wx;
        double div = (vx - mod) / wx;
        if (mod != 0)
        {
            if ((wx < 0) != (mod < 0))
            {
                mod += wx;
                div -= 1.0;
            }
        }
        double floordiv;
        if (div != 0)
        {
            floordiv = Math.Floor(div);
            if (div - floordiv > 0.5)
                floordiv += 1.0;
        }
        else
            floordiv = Math.CopySign(0.0, vx / wx);
        return floordiv;
    }

    /// <summary>x % y for floats (CPython float_rem): the sign of the divisor.</summary>
    public static double Mod(double vx, double wx)
    {
        if (wx == 0.0)
            throw new DivideByZeroException("float modulo");
        double mod = vx % wx;
        if (mod != 0)
        {
            if ((wx < 0) != (mod < 0))
                mod += wx;
        }
        else
            mod = Math.CopySign(0.0, wx);
        return mod;
    }

    public static long FloorDiv(long a, long b)
    {
        long q = a / b;
        if ((a % b != 0) && ((a < 0) != (b < 0)))
            q--;
        return q;
    }

    public static long Mod(long a, long b)
    {
        long m = a % b;
        if (m != 0 && ((m < 0) != (b < 0)))
            m += b;
        return m;
    }

    /// <summary>math.floor: an int.</summary>
    public static long Floor(double x) => (long)Math.Floor(x);

    /// <summary>math.ceil: an int.</summary>
    public static long Ceil(double x) => (long)Math.Ceiling(x);

    /// <summary>int(x) for a float: toward zero.</summary>
    public static long Int(double x)
    {
        if (!double.IsFinite(x))
            throw new OverflowException("cannot convert float infinity or NaN to integer");
        return (long)Math.Truncate(x);
    }

    /// <summary>x ** y for floats: the C library's pow (Python's float_pow defers to it for the cases met here).</summary>
    public static double Pow(double x, double y)
    {
        if (y == 0)
            return 1.0;
        if (x == 1.0)
            return 1.0;
        if (x < 0 && y != Math.Floor(y) && double.IsFinite(y))
            throw new PyValueError("a negative number to a fractional power gives a complex number");
        if (x == 0 && y < 0)
            throw new DivideByZeroException("0.0 cannot be raised to a negative power");
        return Math.Pow(x, y);
    }

    /// <summary>x ** n for ints, n &gt;= 0.</summary>
    public static long Pow(long x, long n)
    {
        long r = 1;
        for (long i = 0; i < n; i++)
            r = checked(r * x);
        return r;
    }

    /// <summary>max(a, b): b only if b &gt; a (the first of equals, and NaN never wins from the right).</summary>
    public static double Max(double a, double b) => b > a ? b : a;

    /// <summary>min(a, b): b only if b &lt; a.</summary>
    public static double Min(double a, double b) => b < a ? b : a;

    public static double Max(double a, double b, params double[] rest)
    {
        double m = Max(a, b);
        foreach (var x in rest)
            m = Max(m, x);
        return m;
    }

    public static double Min(double a, double b, params double[] rest)
    {
        double m = Min(a, b);
        foreach (var x in rest)
            m = Min(m, x);
        return m;
    }

    public static long Max(long a, long b) => b > a ? b : a;
    public static long Min(long a, long b) => b < a ? b : a;

    /// <summary>max(seq) of floats: ValueError when empty.</summary>
    public static double Max(IEnumerable<double> seq)
    {
        bool any = false;
        double m = 0;
        foreach (var x in seq)
        {
            if (!any || x > m)
                m = x;
            any = true;
        }
        if (!any)
            throw new PyValueError("max() iterable argument is empty");
        return m;
    }

    public static double Min(IEnumerable<double> seq)
    {
        bool any = false;
        double m = 0;
        foreach (var x in seq)
        {
            if (!any || x < m)
                m = x;
            any = true;
        }
        if (!any)
            throw new PyValueError("min() iterable argument is empty");
        return m;
    }

    public static double Max(IEnumerable<double> seq, double def)
    {
        bool any = false;
        double m = 0;
        foreach (var x in seq)
        {
            if (!any || x > m)
                m = x;
            any = true;
        }
        return any ? m : def;
    }

    public static double Min(IEnumerable<double> seq, double def)
    {
        bool any = false;
        double m = 0;
        foreach (var x in seq)
        {
            if (!any || x < m)
                m = x;
            any = true;
        }
        return any ? m : def;
    }

    public static long Max(IEnumerable<long> seq, long def)
    {
        bool any = false;
        long m = 0;
        foreach (var x in seq)
        {
            if (!any || x > m)
                m = x;
            any = true;
        }
        return any ? m : def;
    }

    /// <summary>max(seq, key=key): the first of the largest keys.</summary>
    public static T MaxBy<T, K>(IEnumerable<T> seq, Func<T, K> key) where K : IComparable<K>
    {
        bool any = false;
        T best = default!;
        K bk = default!;
        foreach (var x in seq)
        {
            var k = key(x);
            if (!any || k.CompareTo(bk) > 0)
            {
                best = x;
                bk = k;
            }
            any = true;
        }
        if (!any)
            throw new PyValueError("max() iterable argument is empty");
        return best;
    }

    public static T MinBy<T, K>(IEnumerable<T> seq, Func<T, K> key) where K : IComparable<K>
    {
        bool any = false;
        T best = default!;
        K bk = default!;
        foreach (var x in seq)
        {
            var k = key(x);
            if (!any || k.CompareTo(bk) < 0)
            {
                best = x;
                bk = k;
            }
            any = true;
        }
        if (!any)
            throw new PyValueError("min() iterable argument is empty");
        return best;
    }

    /// <summary>max(seq, key=key, default=None).</summary>
    public static T? MaxByOrDefault<T, K>(IEnumerable<T> seq, Func<T, K> key) where K : IComparable<K> where T : class
    {
        bool any = false;
        T? best = null;
        K bk = default!;
        foreach (var x in seq)
        {
            var k = key(x);
            if (!any || k.CompareTo(bk) > 0)
            {
                best = x;
                bk = k;
            }
            any = true;
        }
        return best;
    }

    public static T? MinByOrDefault<T, K>(IEnumerable<T> seq, Func<T, K> key) where K : IComparable<K> where T : class
    {
        bool any = false;
        T? best = null;
        K bk = default!;
        foreach (var x in seq)
        {
            var k = key(x);
            if (!any || k.CompareTo(bk) < 0)
            {
                best = x;
                bk = k;
            }
            any = true;
        }
        return best;
    }

    /// <summary>sorted(seq, key=key): stable, ascending (reverse: descending, still stable as Python's).</summary>
    public static List<T> Sorted<T, K>(IEnumerable<T> seq, Func<T, K> key, bool reverse = false) where K : IComparable<K>
    {
        var items = seq.Select((x, i) => (x, k: key(x), i)).ToList();
        items.Sort((a, b) =>
        {
            int c = a.k.CompareTo(b.k);
            if (reverse)
                c = -c;
            return c != 0 ? c : a.i.CompareTo(b.i);
        });
        return items.Select(t => t.x).ToList();
    }

    public static List<double> Sorted(IEnumerable<double> seq, bool reverse = false)
    {
        var l = seq.ToList();
        // a total order (no NaN is expected here); a stable sort keeps equal floats' order, which can't show anyway
        l.Sort((a, b) => reverse ? b.CompareTo(a) : a.CompareTo(b));
        return l;
    }

    /// <summary>sorted(strings): code point order.</summary>
    public static List<string> Sorted(IEnumerable<string> seq)
    {
        var l = seq.ToList();
        l.Sort(StringComparer.Ordinal);
        return l;
    }

    // ------------------------------------------------------------------ sum (CPython 3.12+: Neumaier)

    /// <summary>sum(floats) as CPython: an int 0 start, the first float added plainly, the rest compensated.</summary>
    public static double Sum(IEnumerable<double> seq)
    {
        using var it = seq.GetEnumerator();
        if (!it.MoveNext())
            return 0.0;
        double hi = 0.0 + it.Current, lo = 0.0;
        while (it.MoveNext())
        {
            double x = it.Current;
            double t = hi + x;
            if (Math.Abs(hi) >= Math.Abs(x))
                lo += (hi - t) + x;
            else
                lo += (x - t) + hi;
            hi = t;
        }
        if (lo != 0 && double.IsFinite(lo))
            return hi + lo;
        return hi;
    }

    public static double Sum<T>(IEnumerable<T> seq, Func<T, double> f) => Sum(seq.Select(f));

    /// <summary>sum(ints).</summary>
    public static long Sum(IEnumerable<long> seq)
    {
        long s = 0;
        foreach (var x in seq)
            s = checked(s + x);
        return s;
    }

    /// <summary>sum() of Python numbers that may mix ints and floats: an int while they are ints.</summary>
    public static object SumObj(IEnumerable<object?> seq)
    {
        using var it = seq.GetEnumerator();
        long iacc = 0;
        while (it.MoveNext())
        {
            var v = it.Current;
            if (v is double d)
            {
                double hi = iacc + d, lo = 0.0;
                while (it.MoveNext())
                {
                    double x = ToDouble(it.Current);
                    double t = hi + x;
                    if (Math.Abs(hi) >= Math.Abs(x))
                        lo += (hi - t) + x;
                    else
                        lo += (x - t) + hi;
                    hi = t;
                }
                return lo != 0 && double.IsFinite(lo) ? hi + lo : hi;
            }
            iacc = checked(iacc + ToLong(v));
        }
        return iacc;
    }

    // ------------------------------------------------------------------ math module

    const double DegToRad = Math.PI / 180.0;
    const double RadToDeg = 180.0 / Math.PI;

    public static double Radians(double x) => x * DegToRad;
    public static double Degrees(double x) => x * RadToDeg;

    /// <summary>math.hypot(x, y): CPython's vector_norm, not libm's hypot.</summary>
    public static double Hypot(double x, double y)
    {
        Span<double> v = stackalloc double[2];
        v[0] = Math.Abs(x);
        v[1] = Math.Abs(y);
        return VectorNorm(v);
    }

    public static double Hypot(double x, double y, double z)
    {
        Span<double> v = stackalloc double[3];
        v[0] = Math.Abs(x);
        v[1] = Math.Abs(y);
        v[2] = Math.Abs(z);
        return VectorNorm(v);
    }

    /// <summary>math.dist(p, q) in 2-D.</summary>
    public static double Dist(double px, double py, double qx, double qy) => Hypot(px - qx, py - qy);

    static double VectorNorm(Span<double> vec)
    {
        double max = 0.0;
        bool nan = false;
        foreach (var x in vec)
        {
            nan |= double.IsNaN(x);
            if (x > max)
                max = x;
        }
        int n = vec.Length;
        if (double.IsInfinity(max))
            return max;
        if (nan)
            return double.NaN;
        if (max == 0.0 || n <= 1)
            return max;
        int maxE = FrexpExponent(max);
        if (maxE < -1023)
        {
            const double DblMin = 2.2250738585072014e-308;
            for (int i = 0; i < n; i++)
                vec[i] /= DblMin;
            return DblMin * VectorNorm(vec);
        }
        double scale = Math.ScaleB(1.0, -maxE);
        double csum = 1.0, frac1 = 0.0, frac2 = 0.0;
        for (int i = 0; i < n; i++)
        {
            double x = vec[i] * scale;
            double prHi = x * x, prLo = Math.FusedMultiplyAdd(x, x, -prHi);
            double smHi = csum + prHi, smLo = (csum - smHi) + prHi;
            csum = smHi;
            frac1 += prLo;
            frac2 += smLo;
        }
        double h = Math.Sqrt(csum - 1.0 + (frac1 + frac2));
        {
            double prHi = -h * h, prLo = Math.FusedMultiplyAdd(-h, h, -prHi);
            double smHi = csum + prHi, smLo = (csum - smHi) + prHi;
            csum = smHi;
            frac1 += prLo;
            frac2 += smLo;
        }
        double xx = csum - 1.0 + (frac1 + frac2);
        h += xx / (2.0 * h);
        return h / scale;
    }

    /// <summary>frexp's exponent: x = m 2^e with 0.5 &lt;= m &lt; 1.</summary>
    static int FrexpExponent(double x)
    {
        long bits = BitConverter.DoubleToInt64Bits(x);
        int ex = (int)((bits >> 52) & 0x7FF);
        if (ex == 0)
        {   // subnormal
            int e = Math.ILogB(x);
            return e + 1;
        }
        return ex - 1022;
    }

    // math.gamma: CPython's own Lanczos approximation (Modules/mathmodule.c), not the C library's tgamma
    const double LanczosG = 6.024680040776729583740234375;
    const double LanczosGMinusHalf = 5.524680040776729583740234375;
    static readonly double[] LanczosNum =
    [
        23531376880.410759688572007674451636754734846804940,
        42919803642.649098768957899047001988850926355848959,
        35711959237.355668049440185451547166705960488635843,
        17921034426.037209699919755754458931112671403265390,
        6039542586.3520280050642916443072979210699388420708,
        1439720407.3117216736632230727949123939715485786772,
        248874557.86205415651146038641322942321632125127801,
        31426415.585400194380614231628318205362874684987640,
        2876370.6289353724412254090516208496135991145378768,
        186056.26539522349504029498971604569928220784236328,
        8071.6720023658162106380029022722506138218516325024,
        210.82427775157934587250973392071336271166969580291,
        2.5066282746310002701649081771338373386264310793408,
    ];
    static readonly double[] LanczosDen =
        [0.0, 39916800.0, 120543840.0, 150917976.0, 105258076.0, 45995730.0, 13339535.0, 2637558.0, 357423.0, 32670.0,
         1925.0, 66.0, 1.0];
    static readonly double[] GammaIntegral =
    [
        1.0, 1.0, 2.0, 6.0, 24.0, 120.0, 720.0, 5040.0, 40320.0, 362880.0, 3628800.0, 39916800.0, 479001600.0,
        6227020800.0, 87178291200.0, 1307674368000.0, 20922789888000.0, 355687428096000.0, 6402373705728000.0,
        121645100408832000.0, 2432902008176640000.0, 51090942171709440000.0, 1124000727777607680000.0,
    ];

    static double LanczosSum(double x)
    {
        double num = 0.0, den = 0.0;
        if (x < 5.0)
        {
            for (int i = LanczosNum.Length; --i >= 0;)
            {
                num = num * x + LanczosNum[i];
                den = den * x + LanczosDen[i];
            }
        }
        else
        {
            for (int i = 0; i < LanczosNum.Length; i++)
            {
                num = num / x + LanczosNum[i];
                den = den / x + LanczosDen[i];
            }
        }
        return num / den;
    }

    public static double Gamma(double x)
    {
        if (!double.IsFinite(x))
        {
            if (double.IsNaN(x) || x > 0.0)
                return x;
            throw new PyValueError("math domain error");
        }
        if (x == 0.0)
            throw new PyValueError("math domain error");
        if (x == Math.Floor(x))
        {
            if (x < 0.0)
                throw new PyValueError("math domain error");
            if (x <= GammaIntegral.Length)
                return GammaIntegral[(int)x - 1];
        }
        double absx = Math.Abs(x);
        if (absx < 1e-20)
            return 1.0 / x;
        if (absx > 200.0)
        {
            if (x < 0.0)
                return 0.0 / SinPi(x);
            throw new OverflowException("math range error");
        }
        double y = absx + LanczosGMinusHalf, z;
        if (absx > LanczosGMinusHalf)
        {
            double q = y - absx;
            z = q - LanczosGMinusHalf;
        }
        else
        {
            double q = y - LanczosGMinusHalf;
            z = q - absx;
        }
        z = z * LanczosG / y;
        double r;
        if (x < 0.0)
        {
            r = -Math.PI / SinPi(absx) / absx * Math.Exp(y) / LanczosSum(absx);
            r -= z * r;
            if (absx < 140.0)
                r /= Math.Pow(y, absx - 0.5);
            else
            {
                double sp = Math.Pow(y, absx / 2.0 - 0.25);
                r /= sp;
                r /= sp;
            }
        }
        else
        {
            r = LanczosSum(absx) / Math.Exp(y);
            r += z * r;
            if (absx < 140.0)
                r *= Math.Pow(y, absx - 0.5);
            else
            {
                double sp = Math.Pow(y, absx / 2.0 - 0.25);
                r *= sp;
                r *= sp;
            }
        }
        if (double.IsInfinity(r))
            throw new OverflowException("math range error");
        return r;
    }

    static double SinPi(double x)
    {
        double y = Math.Abs(x) % 2.0;
        int n = (int)Math.Round(2.0 * y, MidpointRounding.AwayFromZero);
        double r = n switch
        {
            0 => Math.Sin(Math.PI * y),
            1 => Math.Cos(Math.PI * (y - 0.5)),
            2 => Math.Sin(Math.PI * (1.0 - y)),
            3 => -Math.Cos(Math.PI * (y - 1.5)),
            4 => Math.Sin(Math.PI * (y - 2.0)),
            _ => throw new InvalidOperationException(),
        };
        return Math.CopySign(1.0, x) * r;
    }

    /// <summary>bisect.bisect_right(a, x).</summary>
    public static int BisectRight(IReadOnlyList<double> a, double x)
    {
        int lo = 0, hi = a.Count;
        while (lo < hi)
        {
            int mid = (lo + hi) / 2;
            if (x < a[mid])
                hi = mid;
            else
                lo = mid + 1;
        }
        return lo;
    }

    /// <summary>bisect.bisect_left(a, x).</summary>
    public static int BisectLeft(IReadOnlyList<double> a, double x)
    {
        int lo = 0, hi = a.Count;
        while (lo < hi)
        {
            int mid = (lo + hi) / 2;
            if (a[mid] < x)
                lo = mid + 1;
            else
                hi = mid;
        }
        return lo;
    }

    /// <summary>range(n): empty for n &lt;= 0.</summary>
    public static IEnumerable<int> Range(long n) => Enumerable.Range(0, (int)Math.Max(0, n));

    /// <summary>range(start, stop): empty when stop &lt;= start.</summary>
    public static IEnumerable<int> Range(long start, long stop) => Enumerable.Range((int)start, (int)Math.Max(0, stop - start));

    /// <summary>A list of Python values.</summary>
    public static List<object?> List(params object?[] items) => [.. items];
}
