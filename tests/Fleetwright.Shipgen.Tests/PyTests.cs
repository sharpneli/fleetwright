using System.Globalization;

namespace Fleetwright.Shipgen.Tests;

/// <summary>The Python helpers against Windows CPython, bit for bit (Data/pyref.json.gz, made by Data/pyref.py):
/// libm through .NET's Math, CPython's own hypot, dist, gamma, sum, round and float formatting.</summary>
public class PyTests
{
    static readonly PyDict Ref = (PyDict)PyJson.Load(Paths.TestData("pyref.json.gz"))!;

    static double H(object? hex) =>
        BitConverter.Int64BitsToDouble(long.Parse((string)hex!, NumberStyles.HexNumber, CultureInfo.InvariantCulture));

    static string Hex(double x) => BitConverter.DoubleToInt64Bits(x).ToString("x16", CultureInfo.InvariantCulture);

    static void Same(double want, double got, string what)
    {
        if (BitConverter.DoubleToInt64Bits(want) != BitConverter.DoubleToInt64Bits(got))
            Assert.Fail($"{what}: python {Py.FloatRepr(want)} ({Hex(want)}), C# {Py.FloatRepr(got)} ({Hex(got)})");
    }

    [Fact]
    public void Unary()
    {
        Func<double, double>[] fs =
        [
            Math.Sin, Math.Cos, Math.Tan, Math.Atan,
            v => v < 700 ? Math.Exp(v) : 0.0, v => v != 0 ? Math.Log(Math.Abs(v)) : 0.0,
            v => Math.Sqrt(Math.Abs(v)), v => Py.Gamma(Py.Mod(Math.Abs(v), 30) + 0.01),
            v => Py.Pow(Math.Abs(v), 1.0 / 3), v => Py.Pow(Math.Abs(v), 0.75), v => Py.Pow(v, 2),
            Py.Radians, Py.Degrees, v => Py.FloorDiv(v, 0.37), v => Py.Mod(v, 0.37), v => Py.Mod(v, 360.0),
        ];
        string[] names = ["sin", "cos", "tan", "atan", "exp", "log", "sqrt", "gamma", "cbrt", "pow075", "sq", "radians",
                          "degrees", "floordiv", "mod", "mod360"];
        foreach (var rec in Ref.L("unary").Cast<List<object?>>())
        {
            double x = H(rec[0]);
            for (int i = 0; i < fs.Length; i++)
                Same(H(rec[i + 1]), fs[i](x), $"{names[i]}({Py.FloatRepr(x)})");
        }
    }

    [Fact]
    public void Binary()
    {
        foreach (var rec in Ref.L("binary").Cast<List<object?>>())
        {
            double x = H(rec[0]), y = H(rec[1]);
            Same(H(rec[2]), Math.Atan2(x, y), $"atan2({x}, {y})");
            Same(H(rec[3]), Py.Hypot(x, y), $"hypot({x}, {y})");
            Same(H(rec[4]), Py.Dist(x, y, y * 0.3, x - 1), $"dist({x}, {y})");
            Same(H(rec[5]), Math.Abs(y) < 50 && x != 0 ? Py.Pow(Math.Abs(x), y) : 0.0, $"pow({x}, {y})");
        }
    }

    [Fact]
    public void Formatting()
    {
        foreach (var rec in Ref.L("fmt").Cast<List<object?>>())
        {
            double x = H(rec[0]);
            Assert.Equal((string)rec[1]!, Py.FloatRepr(x));
            Assert.Equal((string)rec[2]!, Py.F(x, 0));
            Assert.Equal((string)rec[3]!, Py.F(x, 1));
            Assert.Equal((string)rec[4]!, Py.F(x, 2));
            Assert.Equal((string)rec[5]!, Py.F(x, 3));
            Assert.Equal((string)rec[6]!, Py.G(x));
            Assert.Equal((string)rec[7]!, Py.F(x, 0, comma: true));
            Assert.Equal((string)rec[8]!, Py.F(x, 1, plus: true));
            Assert.Equal((string)rec[9]!, Py.F(x, 1, comma: true));
        }
    }

    [Fact]
    public void Rounding()
    {
        foreach (var rec in Ref.L("round").Cast<List<object?>>())
        {
            double x = H(rec[0]);
            int n = (int)(long)rec[1]!;
            Same(H(rec[2]), Py.Round(x, n), $"round({Py.FloatRepr(x)}, {n})");
            if (Math.Abs(x) < 1e15)
                Assert.Equal((long)rec[3]!, Py.Round(x));
        }
    }

    [Fact]
    public void Sums()
    {
        foreach (var rec in Ref.L("sums").Cast<List<object?>>())
        {
            var xs = ((List<object?>)rec[0]!).Select(H).ToList();
            Same(H(rec[1]), Py.Sum(xs), $"sum of {xs.Count}");
        }
    }

    [Fact]
    public void ReprAndJson()
    {
        Assert.Equal("{'a': 1, 'b': [1.5, None, True], 'c': 'x'}",
            Py.Repr(PyDict.Of(("a", 1L), ("b", Py.List(1.5, null, true)), ("c", "x"))));
        Assert.Equal("\"it's\"", Py.Repr("it's"));
        var v = PyJson.Parse("{\"a\": 1, \"b\": 1.0, \"c\": [NaN, -Infinity, 1e5], \"d\": \"\\u00e9\"}") as PyDict;
        Assert.IsType<long>(v!["a"]);
        Assert.IsType<double>(v["b"]);
        Assert.Equal("{\"a\": 1, \"b\": 1.0, \"c\": [NaN, -Infinity, 100000.0], \"d\": \"\\u00e9\"}", PyJson.Dumps(v, null));
    }
}
