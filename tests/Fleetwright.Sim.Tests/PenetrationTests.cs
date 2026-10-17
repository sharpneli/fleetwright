using Fleetwright.Shipgen;
using Fleetwright.Sim.Combat;
using Fleetwright.Sim.Ships;

namespace Fleetwright.Sim.Tests;

/// <summary>Thompson's limit velocity and its closed-form inverse (docs/battle/shells-armour-damage.md P1a).</summary>
public class PenetrationTests
{
    [Fact]
    public void The_inverse_round_trips()
    {
        double worst = 0;
        foreach (double cal in new[] { 37.0, 102, 152, 203, 305, 381, 460 })
            foreach (double massK in new[] { 8_000.0, 14_000, 20_000 })   // kg/m³: light HE to super-heavy AP
                foreach (double ob in new[] { 0.0, 10, 30, 45, 60, 70, 75, 80, 85, 89 })
                    foreach (double v in new[] { 1.0, 50, 200, 450, 700, 1000 })
                    {
                        double kg = massK * Math.Pow(cal / 1000, 3);
                        double t = Terminal.PenMm(v, cal, kg, ob);
                        Assert.True(t > 0 && double.IsFinite(t), $"{cal} mm {kg:0.#} kg {ob}° {v} m/s: {t}");
                        double back = Terminal.LimitMs(t, cal, kg, ob);
                        worst = Math.Max(worst, Math.Abs(back - v) / v);
                    }
        Assert.True(worst < 1e-9, $"worst {worst:E2}");
    }

    /// <summary>The navweaps / USN 16"/50 Mk 8 table (2,700 lb): striking speed and fall, belt and deck in inches.
    /// Thompson's F_STD is the formula the table was made from, so belts agree to a few percent; decks too.</summary>
    [Fact]
    public void The_USN_16_inch_table()
    {
        (double V, double Fall, double Belt, double Deck)[] rows =
        [
            (2280, 2.5, 29.39, 0.67), (2074, 5.7, 26.16, 1.71), (1893, 9.8, 23.04, 2.79), (1740, 14.9, 20.04, 3.90),
            (1632, 21.1, 17.36, 5.17), (1567, 28.25, 14.97, 6.65), (1556, 36.27, 12.97, 8.48), (1607, 45.47, 11.02, 11.26),
        ];
        double kg = 2700 * 0.45359237;
        foreach (var r in rows)
        {
            double v = r.V * 0.3048;
            double belt = Terminal.PenMm(v, 406.4, kg, r.Fall) / 25.4, deck = Terminal.PenMm(v, 406.4, kg, 90 - r.Fall) / 25.4;
            Assert.InRange(belt / r.Belt, 0.96, 1.04);
            if (r.Deck >= 2)
                Assert.InRange(deck / r.Deck, 0.9, 1.1);
        }
    }

    /// <summary>Lambert-Jonas on Thompson's limit: a plate costs the energy that just gets through it, and plates
    /// stacked hold less than one of their sum (the spaced 2+1+2" STS deck at 68° against a 14": 0.74 of a solid 5"
    /// measured; armour-and-damage.md §3.8).</summary>
    [Fact]
    public void Stacked_plates_hold_less_than_one_of_their_sum()
    {
        Assert.Equal(400, Terminal.Residual(500, 300), 9);
        Assert.Equal(0, Terminal.Residual(300, 300));
        const double cal = 355.6, kg = 680.4, ob = 68;
        double[] stack = [50.8, 25.4, 50.8];
        bool Through(double v)
        {
            foreach (double t in stack)
            {
                double vl = Terminal.LimitMs(t, cal, kg, ob);
                if (v < vl)
                    return false;
                v = Terminal.Residual(v, vl);
            }
            return true;
        }
        double lo = 0, hi = 3000;
        for (int i = 0; i < 60; i++)
        {
            double mid = (lo + hi) / 2;
            (lo, hi) = Through(mid) ? (lo, mid) : (mid, hi);
        }
        double ratio = hi / Terminal.LimitMs(stack.Sum(), cal, kg, ob);
        Assert.InRange(ratio, 0.70, 0.80);
    }

    /// <summary>The catalogue through the material rules gives armor/00 §4.2's table: q at 8" and below, the 16" column
    /// by Q(D) or the elongation penalty, and no face-hardened advantage on a thin plate.</summary>
    [Fact]
    public void Materials_by_calibre_follow_the_research_table()
    {
        static double At(string name, double calIn, double tMm = 400) =>
            MaterialRow.Of(MaterialTemplates.Get(name)!).Against(tMm, calIn * 25.4);
        (string Name, double Q8, double Q16)[] table =
        [
            ("Krupp cemented", 0.88, 0.85), ("cemented armour (CA)", 0.99, 1.03), ("Krupp cemented (KC n/A)", 1.01, 0.99),
            ("Terni cemented (1930s)", 1.03, 1.09), ("Class A", 1.03, 0.92), ("Vickers hardened", 0.89, 0.89),
            ("Krupp non-cemented", 0.92, 0.86), ("special treatment steel", 0.95, 0.91), ("STS", 1.00, 1.00),
            ("Wotan hart", 1.00, 0.85), ("Wotan", 0.95, 0.89), ("nickel steel", 0.85, 0.85), ("wrought iron", 0.53, 0.53),
        ];
        foreach (var (name, q8, q16) in table)
        {
            Assert.Equal(q8, At(name, 6), 2);
            Assert.Equal(q8, At(name, 8), 2);
            Assert.InRange(At(name, 16), q16 - 0.011, q16 + 0.011);   // the table's rounded to 0.01
        }
        Assert.Equal(0.9 * 1.03, At("Class A", 6, 100), 9);   // under 5", the thinnest Class A made
        Assert.Equal(0.9 * 0.88 * Math.Pow(1.5, -0.05), At("Krupp cemented", 12, 90), 9);   // under 0.3 calibres
        Assert.Equal(0.7, At("steel", 16), 9);   // structural steel: no elongation given, no penalty
    }
}
