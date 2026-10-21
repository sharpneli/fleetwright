using System.Globalization;
using Fleetwright.Sim.Core;
using Fleetwright.Sim.Ships;

namespace Fleetwright.Shipgen.Cli;

/// <summary><c>shipgen fc-history</c>: the historical record as the bench every fire-control step answers to
/// (docs/battle/fire-control.md, "The yardstick", F1). Each case is run at its recorded range, weather, target and
/// behaviour, and printed beside the record and its band; a record holds only where it was made, so nothing is checked
/// between the cases but the orderings below. The first game's era (1890-1925) leads; the later cases are secondary
/// checks. What the sim doesn't model yet (smoke, the sea, haze on the rangefinders, splash confusion) is named with
/// each case, so a departure can be read for what it is. Where the record says which way the smoke went, the case's wind
/// blows it so in the case's own frame (the shooter's base course north; docs/battle/smoke.md Z3), for runs with the
/// smoke field on (--smoke); it changes nothing with the field off.</summary>
static class FcHistory
{
    static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    /// <summary>A case of the record: the engagement, what was recorded and the band a run is held to (hit %, or none),
    /// the first hit's salvo where the record gives it, and what the sim leaves out.</summary>
    sealed record HistoryCase(string Era, FcCase Case, string Record, double Lo, double Hi, string Missing, double FirstSalvoLo = double.NaN,
        double FirstSalvoHi = double.NaN);

    /// <summary>An ordering between two cases: a's hits over b's within a band of ratios.</summary>
    sealed record Ordering(string What, string A, string B, double Lo, double Hi);

    static List<HistoryCase> Cases(Func<string, ShipClass> cls)
    {
        const double kyd = FcBench.Kyd;
        var practice = 1.0;
        FcCase C(string name, string shooter, string target, Func<FcCase, FcCase> with) =>
            with(new FcCase { Name = name, Shooter = cls(shooter), Target = cls(target), Minutes = 20 });
        return
        [
            new("1890-1925", C("Santiago 1898, US 12\"", "connecticut", "armoured_cruiser", c => c with
                {
                    Discipline = "independent", Fit = 0, RangeM = 2_600, BearingDeg = 30, CourseDeg = 0, OwnKn = 13, TargetKn = 14, Minutes = 30,
                }), "42 / 1,200 major calibre (3.5%); 1.3% all calibres", 1.75, 7, "brown powder smoke; a running fight along the coast"),
            new("1890-1925", C("Yellow Sea 1904, Japanese 12\"", "mikasa", "borodino", c => c with
                {
                    Discipline = "independent", RangeM = 5_600, BearingDeg = 90, CourseDeg = 0, OwnKn = 14, TargetKn = 14, Minutes = 30,
                }), "~30 / 603 (4.7%)", 2.5, 9, "6-7 h of manoeuvring reduced to a parallel run"),
            new("1890-1925", C("Tsushima 1905, Japanese 12\"", "mikasa", "borodino", c => c with
                {
                    Discipline = "independent", RangeM = 5_000 * FcBench.Yd, BearingDeg = 80, CourseDeg = 10, OwnKn = 15, TargetKn = 11, VisibilityM = 9_000, Minutes = 30,
                }), "~40 / 446 (~9%), mostly at 6,500 yd or less (the case at 5,000)", 5, 15, "high waves (ship motion)"),
            new("1890-1925", C("Tsushima 1905, Japanese 6\"", "mikasa", "borodino", c => c with
                {
                    Discipline = "independent", Battery = 1, RangeM = 5_000 * FcBench.Yd, BearingDeg = 80, CourseDeg = 10, OwnKn = 15, TargetKn = 11, VisibilityM = 9_000, Minutes = 30,
                }), "~350 / ~16,900 (~2%, Sims's estimate)", 1, 4, "high waves; whose splash among many 6\""),
            new("1890-1925", C("1912 trial, director (Thunderer)", "orion", "dreadnought", c => c with
                {
                    Discipline = "rn-1914", Fit = 3, RangeM = 8_200, BearingDeg = 90, CourseDeg = 0, OwnKn = 12, TargetKn = 6, Combat = practice, Minutes = 10,
                }), "heavy sea: 26 / 39 (67%); clear: about even with independent", double.NaN, double.NaN,
                "the heavy sea (ship motion); the trial's target and procedure (unknown)"),
            new("1890-1925", C("1912 trial, independent (Orion)", "orion", "dreadnought", c => c with
                {
                    Discipline = "rn-1914", Fit = 2, RangeM = 8_200, BearingDeg = 90, CourseDeg = 0, OwnKn = 12, TargetKn = 6, Combat = practice, Minutes = 10,
                }), "heavy sea: 4 / 27 (15%); clear: about even with director", double.NaN, double.NaN,
                "the heavy sea; smoke over the guns (independent aim's collapse in the trial)"),
            new("1890-1925", C("Dogger Bank 1915, British BCs", "lion", "seydlitz", c => c with
                {
                    Discipline = "rn-1914", RangeM = 16_500, BearingDeg = 20, CourseDeg = 0, OwnKn = 27, TargetKn = 23, Minutes = 30,
                    WindFromDeg = 20, WindKn = 10,
                }), "16 / 1,150 (1.4%); Lion's first hit after ~10 salvos", 0.7, 2.8, "the Germans' smoke on the British; the Germans' manoeuvres",
                FirstSalvoLo: 6, FirstSalvoHi: 15),
            new("1890-1925", C("Dogger Bank 1915, German BCs", "seydlitz", "lion", c => c with
                {
                    Discipline = "german", RangeM = 16_500, BearingDeg = 170, CourseDeg = 0, OwnKn = 23, TargetKn = 27, Minutes = 30,
                    WindFromDeg = 350, WindKn = 10,
                }), "~21 / 976 (~2.5%)", 1.2, 5, "their own funnel smoke"),
            new("1890-1925", C("Jutland 1916, British 1st/2nd BCS", "lion", "derfflinger", c => c with
                {
                    Discipline = "rn-1914", RangeM = 15_000, BearingDeg = 80, CourseDeg = 0, OwnKn = 25, TargetKn = 25, Behaviour = Policy.Zigzag(15, 300),
                    Own = Policy.Zigzag(15, 300), VisibilityM = 16_000, Minutes = 25, WindFromDeg = 260, WindKn = 8,
                }), "21 / 1,469 (1.4%)", 0.7, 2.8, "haze on the rangefinders; low contrast; smoke; splash confusion"),
            new("1890-1925", C("Jutland 1916, British 5th BS", "super_dreadnought", "moltke", c => c with
                {
                    Discipline = "rn-1914", RangeM = 17_500, BearingDeg = 80, CourseDeg = 0, OwnKn = 24, TargetKn = 25, Behaviour = Policy.Zigzag(15, 300),
                    Own = Policy.Zigzag(15, 300), VisibilityM = 18_000, Minutes = 25, WindFromDeg = 260, WindKn = 8,
                }), "29 / 1,099 (2.6%)", 1.3, 5.2, "haze on the rangefinders; smoke; splash confusion"),
            new("1890-1925", C("Jutland 1916, Grand Fleet BS", "iron_duke", "konig", c => c with
                {
                    Discipline = "rn-1914", RangeM = 10_500, BearingDeg = 90, CourseDeg = 0, OwnKn = 20, TargetKn = 17, Behaviour = Policy.Zigzag(20, 240),
                    VisibilityM = 12_000, Minutes = 20, WindFromDeg = 90, WindKn = 8,
                }), "57 / 1,593 (3.6%) at 9-12 kyd", 1.8, 7.2, "haze on the rangefinders; smoke; the turn-aways' few minutes"),
            new("1890-1925", C("Jutland 1916, British 3rd BCS", "invincible", "derfflinger", c => c with
                {
                    Discipline = "rn-1914", RangeM = 8_200, BearingDeg = 90, CourseDeg = 0, OwnKn = 25, TargetKn = 25, VisibilityM = 10_000, Minutes = 15,
                }), "16 / 373 (4.3%), short", 2.2, 8.6, "haze on the rangefinders; smoke"),
            new("1890-1925", C("Jutland 1916, German 1st SG", "derfflinger", "lion", c => c with
                {
                    Discipline = "german", RangeM = 15_000, BearingDeg = 100, CourseDeg = 0, OwnKn = 25, TargetKn = 25, Behaviour = Policy.Zigzag(15, 300),
                    Own = Policy.Zigzag(15, 300), VisibilityM = 18_000, Minutes = 25, WindFromDeg = 100, WindKn = 8,
                }), "67 / 1,670 (4.0%)", 2, 8, "the British silhouetted against the western sky (contrast)"),
            new("1890-1925", C("Jutland 1916, German battleships", "konig", "iron_duke", c => c with
                {
                    Discipline = "german", RangeM = 11_000, BearingDeg = 90, CourseDeg = 0, OwnKn = 17, TargetKn = 20, Behaviour = Policy.Zigzag(15, 300),
                    VisibilityM = 12_000, Minutes = 20, WindFromDeg = 270, WindKn = 8,
                }), "57 / 1,927 (3.0%)", 1.5, 6, "haze; the British line seen only by its flashes at times"),
            new("1890-1925", C("Falklands 1914, Invincible", "invincible", "armoured_cruiser", c => c with
                {
                    Discipline = "rn-1914", RangeM = 13_700, BearingDeg = 30, CourseDeg = 0, OwnKn = 25, TargetKn = 20, Minutes = 30,
                    WindFromDeg = 210, WindKn = 10,
                }), "~80 / 1,174 (~7%; hit counts uncertain)", 3.5, 14, "own funnel smoke across the range; the long chase's turns"),
            new("1890-1925", C("USN practice 1912-18, 6 kyd", "pennsylvania", "dreadnought", c => c with
                {
                    Discipline = "usn", RangeM = 6 * kyd, BearingDeg = 90, CourseDeg = 0, OwnKn = 12, TargetKn = 8, Combat = practice, Minutes = 10,
                }), "H = 100/[1 + 0.0007(R - 2000)]: 26%", 17, 39, "a practice screen, not a ship"),
            new("1890-1925", C("USN practice 1912-18, 10 kyd", "pennsylvania", "dreadnought", c => c with
                {
                    Discipline = "usn", RangeM = 10 * kyd, BearingDeg = 90, CourseDeg = 0, OwnKn = 12, TargetKn = 8, Combat = practice, Minutes = 10,
                }), "H = 100/[1 + 0.0007(R - 2000)]: 15%", 10, 23, "a practice screen, not a ship"),
            new("1890-1925", C("Tennessee-like practice, 19 kyd", "pennsylvania", "dreadnought", c => c with
                {
                    Discipline = "usn", Fit = 4, RangeM = 19 * kyd, BearingDeg = 90, CourseDeg = 0, OwnKn = 15, TargetKn = 10, Combat = practice, Minutes = 15,
                }), "first hit on salvo 4, 3 min 42 s (practice hits from salvo 2-5)", double.NaN, double.NaN, "a practice target",
                FirstSalvoLo: 2, FirstSalvoHi: 5),

            new("later", C("Bismarck's end, Rodney", "nelson", "bismarck", c => c with
                {
                    Discipline = "rn-1917", RangeM = 14_000, BearingDeg = 30, CourseDeg = 210, OwnKn = 20, TargetKn = 7, Behaviour = Policy.Crippled,
                    VisibilityM = 15_000, Minutes = 25,
                }), "~40 / 375 (~11%)", 6, 20, "splash confusion (two battleships on one target); the closing to 3 kyd"),
            new("later", C("Denmark Strait, Bismarck", "bismarck", "kgv", c => c with
                {
                    Discipline = "german", RangeM = 20_000, BearingDeg = 60, CourseDeg = 240, OwnKn = 28, TargetKn = 28, VisibilityM = 30_000, Minutes = 10,
                }), "5-6 / 93 (~5.5%)", 2.7, 11, "two targets; the British turning to open their arcs"),
            new("later", C("Komandorski, Japanese on a salvo chaser", "heavy_cruiser", "heavy_cruiser", c => c with
                {
                    Discipline = "rn-1914", RangeM = 20 * kyd, BearingDeg = 10, CourseDeg = 0, OwnKn = 30, TargetKn = 33, Behaviour = Policy.Chase(35, 30),
                    VisibilityM = 25_000, Minutes = 30,
                }), "~8 / ~1,600 (~0.5%); 200+ within 50 yd of Salt Lake City", 0.25, 1, "smoke; the 3.5 h chase reduced to 30 min"),
            new("later", C("Komandorski geometry, a steady target", "heavy_cruiser", "heavy_cruiser", c => c with
                {
                    Discipline = "rn-1914", RangeM = 20 * kyd, BearingDeg = 10, CourseDeg = 0, OwnKn = 30, TargetKn = 33, VisibilityM = 25_000, Minutes = 30,
                }), "(the control: no record)", double.NaN, double.NaN, ""),
            new("later", C("LRBP 1932-33, 16\" at 30 kyd", "colorado", "battleship", c => c with
                {
                    Discipline = "usn", Fit = 4, RangeM = 30 * kyd, BearingDeg = 90, CourseDeg = 0, OwnKn = 15, TargetKn = 15, VisibilityM = 40_000, Combat = practice,
                    Minutes = 15,
                }), "4.2-5.4% (4.4% mean; MPI error 313 yd)", 2.9, 6.6, "practice target; spotting aircraft"),
            new("later", C("USN 1944 study, 16\" at 10 kyd", "colorado", "battleship", c => c with
                {
                    Discipline = "usn", Fit = 5, RangeM = 10 * kyd, BearingDeg = 90, CourseDeg = 0, OwnKn = 15, TargetKn = 15, Combat = practice,
                }), "32.7% (practice estimate)", 22, 49, "radar ranging and spotting (F9)"),
            new("later", C("USN 1944 study, 16\" at 20 kyd", "colorado", "battleship", c => c with
                {
                    Discipline = "usn", Fit = 5, RangeM = 20 * kyd, BearingDeg = 90, CourseDeg = 0, OwnKn = 15, TargetKn = 15, Combat = practice,
                }), "10.5% (practice estimate)", 7, 16, "radar ranging and spotting (F9)"),
            new("later", C("USN 1944 study, 16\" at 30 kyd", "colorado", "battleship", c => c with
                {
                    Discipline = "usn", Fit = 5, RangeM = 30 * kyd, BearingDeg = 90, CourseDeg = 0, OwnKn = 15, TargetKn = 15, VisibilityM = 40_000, Combat = practice,
                }), "2.7% (practice estimate)", 1.8, 4, "radar ranging and spotting (F9)"),
            new("later", C("Light guns at a fast boat, 5\"", "fubuki", "pt_boat", c => c with
                {
                    Discipline = "rn-1914", RangeM = 3_000, BearingDeg = 90, CourseDeg = 0, OwnKn = 20, TargetKn = 38, Behaviour = Policy.Zigzag(30, 30), Minutes = 10,
                }), "0.5-2% a round", 0.5, 2, "the boat's small silhouette in a sea"),
        ];
    }

    static readonly Ordering[] Orderings =
    [
        new("Tsushima's 12\" over the Yellow Sea's (closer, blooded)", "Tsushima 1905, Japanese 12\"", "Yellow Sea 1904, Japanese 12\"", 1.2, 4),
        new("heavy guns over the 6\" in one action (×2-4)", "Tsushima 1905, Japanese 12\"", "Tsushima 1905, Japanese 6\"", 2, 6),
        new("director about even with independent in clear weather", "1912 trial, director (Thunderer)", "1912 trial, independent (Orion)", 0.7, 1.5),
        new("the Germans over the British at Jutland (BCs)", "Jutland 1916, German 1st SG", "Jutland 1916, British 1st/2nd BCS", 1.5, 5),
        new("the Grand Fleet's 9-12 kyd over the BCs' long range", "Jutland 1916, Grand Fleet BS", "Jutland 1916, British 1st/2nd BCS", 1.5, 5),
        new("a steady target over a salvo chaser at long range (×3-10)", "Komandorski geometry, a steady target",
            "Komandorski, Japanese on a salvo chaser", 3, 10),
        new("practice's fall with the range (6 against 10 kyd)", "USN practice 1912-18, 6 kyd", "USN practice 1912-18, 10 kyd", 1.2, 2.5),
    ];

    public static int Run(SimSettings settings, int runs, string? only, string? refRoot, Func<string, ShipClass> cls)
    {
        var cases = Cases(cls).Where(h => only is null || h.Case.Name.Contains(only, StringComparison.OrdinalIgnoreCase)).ToList();
        Console.WriteLine($"The record, each case at its own range, weather and target behaviour ({runs} runs a case; fire-control.md, \"The yardstick\")");
        var res = FcBench.Runs(cases.Select(h => h.Case).ToList(), settings, runs, 1);
        var hit = new Dictionary<string, double>();
        string era = "";
        int inBand = 0, banded = 0;
        for (int k = 0; k < cases.Count; k++)
        {
            var (h, c) = (cases[k], cases[k].Case);
            var st = FcStats.Of(res[k], c.Minutes);
            hit[c.Name] = st.HitPct;
            if (h.Era != era)
            {
                era = h.Era;
                Console.WriteLine($"\n{(era == "later" ? "Later cuts (secondary checks)" : "The first game's era, " + era)}");
                Console.WriteLine($"  {"case",-42}{"fit",4}{"km",6}{"model",8}{"band",11}{"verdict",10}{"strad",7}{"1st hit",9}{"after",7}{"from",7}  record");
            }
            string verdict = double.IsNaN(h.Lo) ? "" : st.HitPct < h.Lo ? "LOW" : st.HitPct > h.Hi ? "HIGH" : "in";
            if (!double.IsNaN(h.Lo))
            {
                banded++;
                inBand += verdict == "in" ? 1 : 0;
            }
            if (!double.IsNaN(h.FirstSalvoLo))
                verdict += (verdict.Length > 0 ? "," : "") + (st.FirstHitSalvo < h.FirstSalvoLo ? "early" : st.FirstHitSalvo > h.FirstSalvoHi ? "late" :
                    double.IsNaN(st.FirstHitSalvo) ? "none" : "1st ok");
            string band = double.IsNaN(h.Lo) ? "" : string.Format(Inv, "{0:0.#}-{1:0.#}%", h.Lo, h.Hi);
            Console.WriteLine(string.Format(Inv, "  {0,-42}{1,4}{2,6:0.0}{3,7:0.0}%{4,11}{5,10}{6,6:0}%{7,9}{8,7}{9,7}  {10}", c.Name, c.Level, c.RangeM / 1000,
                st.HitPct, band, verdict, st.StraddlePct, st.FirstHitSalvo is var s && double.IsNaN(s) ? "-" : $"salvo {s:0}",
                double.IsNaN(st.FirstHitAfterOpenS) ? "-" : $"{st.FirstHitAfterOpenS:0} s", double.IsNaN(st.FirstHitS) ? "-" : $"{st.FirstHitS:0} s", h.Record));
            if (h.Missing.Length > 0)
                Console.WriteLine($"  {"",42}  not modelled: {h.Missing}; {st.Shells / Math.Max(st.Runs, 1)} shells a run, fire discipline {c.Discipline ?? settings.FireDisciplines[0].Name}" +
                    (double.IsNaN(c.WindKn) ? "" : string.Format(Inv, "; wind from {0:0}° at {1:0} kn", c.WindFromDeg, c.WindKn)));
        }
        Console.WriteLine($"\n{inBand} of {banded} cases in their bands.");
        var orderings = Orderings.Where(o => hit.ContainsKey(o.A) && hit.ContainsKey(o.B)).ToList();
        if (orderings.Count > 0)
        {
            Console.WriteLine("\nThe orderings (a's hit rate over b's):");
            foreach (var o in orderings)
            {
                double r = hit[o.A] / Math.Max(hit[o.B], 1e-9);
                Console.WriteLine(string.Format(Inv, "  {0,-62} {1,6:0.00}  want {2:0.#}-{3:0.#}  {4}", o.What, r, o.Lo, o.Hi,
                    r < o.Lo ? "LOW" : r > o.Hi ? "HIGH" : "in"));
            }
        }
        if (refRoot is not null)
            FcReference.Run(refRoot, "history", runs);
        return 0;
    }
}
