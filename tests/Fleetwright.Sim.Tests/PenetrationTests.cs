using Fleetwright.Sim.Combat;

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
}
