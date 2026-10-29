using System.Globalization;
using System.Text;
using Fleetwright.Sim.Combat;
using Fleetwright.Sim.Core;
using Fleetwright.Sim.Ships;

namespace Fleetwright.Shipgen.Cli;

/// <summary>`shipgen searchlight`: a class's searchlights in the fight (docs/battle/night.md Q4), through the sim's own
/// beam model (<see cref="SearchlightModel"/>) and sight model: each light (mirror, beam, spread, control, blind arc); her
/// biggest's light on a ship by range, her reach (the operator's lookout makes out the reference ship by it) by the
/// visibility; a target class lit by it, made out by the operator (his view veiled by the beam's own light) and by a
/// consort beside him; and how far her lamp is seen: in its beam, 45° off it, and its beam from beside. A moonless clear
/// night unless --sky (cd/m²).</summary>
static class SearchlightBench
{
    static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    public static int Run(ShipClass own, ShipClass target, SimSettings s, double skyCdM2, double consortM)
    {
        var sb = new StringBuilder();
        void Line(string t) => sb.AppendLine(t);
        var rows = own.Searchlights;
        Line(string.Format(Inv, "{0}: {1} searchlights; sky {2:0.0e0} cd/m²", own, rows.Length, skyCdM2));
        if (rows.Length == 0)
        {
            Console.Write(sb);
            return 0;
        }
        int big = 0;
        for (int i = 0; i < rows.Length; i++)
        {
            ref readonly var l = ref rows[i];
            int blind = 0;
            for (int k = 0; k < Sight.Sectors; k++)
                blind += (l.Blind >> k & 1) != 0 ? 1 : 0;
            Line(string.Format(Inv, "  {0,-26} {1,4:0.00} m mirror, {2:0.0e0} cd, spread {3:0.0}°, {4}, {5,4:0.0} m up at x {6,6:0.0} y {7,5:0.0}, blind {8:0}°",
                own.ComponentId(l.Component), l.MirrorM, l.BeamCd, l.SpreadDeg, l.Control.ToString().ToLowerInvariant(), l.HeightM, l.X, l.Y,
                blind * Sight.SectorDeg));
            if (l.BeamCd > rows[big].BeamCd)
                big = i;
        }
        var light = rows[big];
        var (sigma, keep) = SearchlightModel.Held(light.SpreadDeg, SearchlightModel.PointingDeg(light.Control, s));
        // her operator's lookout: the eye nearest the lamp
        int eye = 0;
        double offset = double.MaxValue;
        for (int e = 0; e < own.Eyes.Length; e++)
        {
            ref readonly var o = ref own.Eyes[e];
            double d = Math.Sqrt((o.X - light.X) * (o.X - light.X) + (o.Y - light.Y) * (o.Y - light.Y) + (o.HeightM - light.HeightM) * (o.HeightM - light.HeightM));
            if (d < offset)
                (eye, offset) = (e, d);
        }
        double eyeM = own.Eyes.Length > 0 ? own.Eyes[eye].HeightM : light.HeightM;
        Line(string.Format(Inv, "  the biggest, {0}, held on a ship: σ {1:0.00}° with its pointing, {2:0%} of its peak; its flux {3:0.0e0} lm; " +
            "the operator's lookout {4} m off it at {5:0.0} m", own.ComponentId(light.Component), sigma, keep, SearchlightModel.FluxLm(light.BeamCd, light.SpreadDeg),
            Math.Round(offset, 1), eyeM));
        double[] ranges = [500, 1000, 2000, 4000, 8000];
        double[] visibilities = [25_000, 10_000, 5_000, 2_000];
        foreach (double v in visibilities)
            Line(string.Format(Inv, "  {0,2:0} km visibility: lux on her at {1}; reach {2:0.0} km; the operator's veil at 2 km {3:0.00} cd/m²", v / 1000,
                string.Join(", ", ranges.Select(r => string.Format(Inv, "{0:0.#} km {1:0.00}", r / 1000, SearchlightModel.LuxAt(light.BeamCd * keep, r, v)))),
                SearchlightModel.ReachM(light, offset, v, skyCdM2, s) / 1000,
                SearchlightModel.BackscatterCdM2(light.BeamCd, light.SpreadDeg, light.MirrorM, 0, sigma, keep, offset, 2000, v, s)));
        // the target lit, side on: the operator, down the beam; a consort beside him
        foreach (double v in visibilities)
        {
            double Seen(bool operatorEye, bool lit)
            {
                for (double d = 15_000; d >= 100; d -= 50)
                {
                    double veil = lit ? SearchlightModel.BackscatterCdM2(light.BeamCd, light.SpreadDeg, light.MirrorM, 0, sigma, keep,
                        operatorEye ? offset : consortM, d, v, s) : 0;
                    double off = operatorEye ? 0 : Math.Atan2(consortM, d) * 180 / Math.PI;
                    double lux = lit ? SearchlightModel.LuxAt(light.BeamCd * keep, d, v) * (1 + Math.Cos(off * Math.PI / 180)) / 2 : 0;
                    var line = new SightLine(d, 90, v, skyCdM2, 0, Glass: SightModel.LookoutGlass(SightModel.PupilMm(skyCdM2 + veil), s), LitLux: lux, VeilCdM2: veil);
                    if (SightModel.Margin(eyeM, s.SightLookoutMagnification, target, line, s) >= 1)
                        return d / 1000;
                }
                return 0;
            }
            Line(string.Format(Inv, "  {0,2:0} km: {1} side on made out at {2:0.0} km dark, lit {3:0.0} km by the operator, {4:0.0} km by a consort {5:0} m beside him",
                v / 1000, target, Seen(true, false), Seen(true, true), Seen(false, true), consortM));
        }
        // the lamp seen by a lookout as high as hers
        foreach (double v in visibilities)
        {
            double Lamp(double offDeg, bool column)
            {
                double cd = column ? SearchlightModel.ColumnCd(light.BeamCd, light.SpreadDeg, v, s) : SearchlightModel.LampCd(light.BeamCd, offDeg, sigma, keep, s);
                for (double d = 60_000; d >= 100; d -= 100)
                    if (SightModel.LightMargin(eyeM, cd, light.HeightM, d, v, skyCdM2, s) >= 1)
                        return d / 1000;
                return 0;
            }
            Line(string.Format(Inv, "  {0,2:0} km: the lamp seen at {1:0.0} km in its beam, {2:0.0} km 45° off it; its beam from beside at {3:0.0} km (to the horizon {4:0.0})",
                v / 1000, Lamp(0, false), Lamp(45, false), Lamp(0, true), SightModel.ReachM(eyeM, light.HeightM, s) / 1000));
        }
        Console.Write(sb);
        return 0;
    }
}
