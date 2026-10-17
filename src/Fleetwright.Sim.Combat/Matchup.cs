using Fleetwright.Sim.Core;
using Fleetwright.Sim.Ships;

namespace Fleetwright.Sim.Combat;

/// <summary>A class as the matchup sees it (docs/battle/command.md 5.1g), compiled once from its rows and its guns' range
/// tables: its size and speed, its armour as falling shells meet it, its broadside's guns and torpedoes. Pure data: the
/// matchup reads it, nothing changes it.</summary>
public sealed class MatchupProfile
{
    public const double RangeStepM = 250;

    public ShipClass Class { get; }
    /// <summary>The shells it's judged with: a ship's own (an own ship's given loadout), else the design's.</summary>
    public Loadout Loadout { get; }
    public double DisplacementT { get; }
    public double LengthM { get; }
    public double BeamM { get; }
    /// <summary>Its height over the water for the danger space (its freeboard by <see cref="SimSettings.MatchupHeightShare"/>).</summary>
    public double HeightM { get; }
    public double SpeedKn { get; }
    /// <summary>No gun of a cruiser's 6" (<see cref="TorpedoCraftGunMm"/>) and torpedoes: it fights with its torpedoes.</summary>
    public bool TorpedoCraft { get; }

    /// <summary>The side and the deck as a falling shell meets them: shares of the face, each with its armour and its
    /// face-hardened plate (<see cref="MatchupFace"/>), and the hull's own plating there. A material's worth depends on
    /// the shell's calibre (MaterialRow.Against), so each face is taken against an 8" shell and a 16" one, and a shell
    /// between reads both (<see cref="Matchup.OutExponent"/>; held outside).</summary>
    public MatchupFace[] Side { get; }
    public MatchupFace[] Deck { get; }
    public MatchupFace[] Side16 { get; }
    public MatchupFace[] Deck16 { get; }
    public double SidePlatingMm { get; }
    public double DeckPlatingMm { get; }
    /// <summary>The share of the side over the main deck (the upperworks: from the silhouette), and the deck armour under
    /// them on the mean (mm, as the faces'): a burst up there reaches the hull's vitals only through it.</summary>
    public double UpperShare { get; }
    public double DeckMeanMm { get; }

    public MatchupGun[] Guns { get; }
    /// <summary>The main battery's reach (the heaviest gun's), m; 0 without one.</summary>
    public double MainReachM { get; }

    /// <summary>Its torpedoes: the tubes that bear on a beam (one salvo), all it carries, and the torpedo (the longest-ranged
    /// one aboard); no tubes, no torpedoes.</summary>
    public int TubesBearing { get; }
    public int Torpedoes { get; }
    public TorpedoRow Torpedo { get; }
    public double TorpedoDud { get; }

    /// <summary>8", the calibre the faces are first taken against (and 16" the second).</summary>
    public const double Eight = 8 * 25.4;

    /// <summary>The gun that makes a ship more than a torpedo craft: a cruiser's 6".</summary>
    public const double TorpedoCraftGunMm = 150;

    public MatchupProfile(ShipClass c, Func<BallisticKey, BallisticTable> table, SimSettings s, Loadout? loadout = null)
    {
        Class = c;
        Loadout = loadout ?? c.Loadout;
        var m = c.Motion;
        DisplacementT = m.FullDisplacementT;
        LengthM = m.LengthM;
        BeamM = m.BeamM;
        HeightM = Math.Max(c.Vertical.Freeboard, 1) * s.MatchupHeightShare;
        SpeedKn = m.MaxSpeedKn;
        (Side, Deck) = (SideArmour(c, Eight), DeckArmour(c, Eight));
        (Side16, Deck16) = (SideArmour(c, 2 * Eight), DeckArmour(c, 2 * Eight));
        (SidePlatingMm, DeckPlatingMm) = (c.Plating.ShellMm, c.Plating.StrengthDeckMm);
        var area = c.Numbers(c.Silhouette.SideAreaM2);
        double whole = Sight.AreaAbove(area, 0);
        UpperShare = whole > 0 ? Math.Clamp(Sight.AreaAbove(area, c.Vertical.Freeboard) / whole, 0, 1) : 0;
        foreach (var f in Deck)
            DeckMeanMm += f.Share * f.Mm;

        var guns = new List<MatchupGun>();
        double mainCal = 0;
        bool craft = true;
        for (int b = 0; b < c.Batteries.Length; b++)
        {
            ref readonly var row = ref c.Batteries[b];
            if (row.CalibreMm >= TorpedoCraftGunMm)
                craft = false;
            if (row.Kind is not (Battery.Main or Battery.Secondary))
                continue;
            int bearing = 0, rounds = 0, mounts = 0;
            double reload = 0, maxElev = 0;
            foreach (int mi in c.Links(row.Mounts))
            {
                ref readonly var mr = ref c.Mounts[mi];
                maxElev = Math.Max(maxElev, mr.MaxElevationDeg);
                if (!CombatStore.InArcs(c, mr, 90))
                    continue;
                bearing += c.TurretTypes[mr.Type].Barrels;
                rounds = rounds == int.MaxValue || mr.Rounds <= 0 ? int.MaxValue : rounds + mr.Rounds;
                reload += mr.ReloadS;
                mounts++;
            }
            if (bearing == 0)
                continue;
            var shell = Loadout.ShellTypes[Loadout.Fires(b)];
            var t = table(BallisticKey.Of(shell, maxElev));
            var g = new MatchupGun(b, row, bearing, 60 / (reload / mounts), rounds, shell, t, s);
            guns.Add(g);
            if (row.Kind == Battery.Main && row.CalibreMm > mainCal)
                (mainCal, MainReachM) = (row.CalibreMm, g.ReachM);
        }
        Guns = [.. guns];

        double fast = 0;
        int tubes = 0, torpedoes = 0;
        foreach (ref readonly var l in c.Launchers)
        {
            ref readonly var tr = ref c.Torpedoes[l.Torpedo];
            torpedoes += l.Torpedoes;
            if (l.Trains || CombatStore.InArcs(c, l.Arcs, 90))
                tubes += l.Tubes;
            if (tr.FastRangeM > fast)
                (fast, Torpedo) = (tr.FastRangeM, tr);
        }
        (TubesBearing, Torpedoes) = (tubes, torpedoes);
        TorpedoDud = s.TorpedoDud[Math.Clamp((int)c.Era, 0, s.TorpedoDud.Length - 1)];
        TorpedoCraft = craft && c.Launchers.Length > 0;
    }

    /// <summary>Whether it's built to make torpedo attacks (a destroyer, a light cruiser with a broadside of tubes; not a
    /// battleship with a few submerged ones): <see cref="SimSettings.MatchupAttackTubesPerKt"/> tubes bearing a thousand
    /// tonnes or more.</summary>
    public bool Attacker(SimSettings s) => TubesBearing > 0 && TubesBearing * 1000 / DisplacementT >= s.MatchupAttackTubesPerKt;

    /// <summary>The torpedo attack's firing range (<see cref="SimSettings.TorpedoAttackShare"/> of the fast setting), 0
    /// for a ship not built to attack with torpedoes.</summary>
    public double AttackRangeM(SimSettings s) => Attacker(s) ? s.TorpedoAttackShare * Torpedo.FastRangeM : 0;

    /// <summary>The side between the waterline and the main deck, sampled along its length and up it: the thickest plate
    /// over each point (by its material's quality), shares of the face by thickness.</summary>
    static MatchupFace[] SideArmour(ShipClass c, double cal)
    {
        const int nx = 48, nz = 8;
        double x0 = -c.Length / 2, wl = c.Vertical.Waterline, top = Math.Max(wl + 0.5, 0);
        var mm = new MatchupFace[nx * nz];
        for (int i = 0; i < nx; i++)
            for (int k = 0; k < nz; k++)
            {
                double x = x0 + (i + 0.5) * c.Length / nx, z = wl + (k + 0.5) * (top - wl) / nz;
                double best = 0, zone = double.NaN;
                int bestOf = -1, zoneOf = -1;
                for (int j = 0; j < c.SidePlates.Length; j++)
                {
                    ref readonly var p = ref c.SidePlates[j];
                    if (x >= p.X0 && x <= p.X1 && z >= p.Bottom && z <= p.Top)
                    {
                        // a leaning belt as thick as a level line through it (protection-plan.md H6)
                        double t = p.ThicknessMm * c.MaterialQ(p.Material, p.ThicknessMm, cal) / Math.Cos(double.DegreesToRadians(p.InclineDeg));
                        if (p.Kind == StrakeKind.Zone)
                            (zone, zoneOf) = (t, j);   // in the belt's place over the magazines
                        else if (t > best)
                            (best, bestOf) = (t, j);
                    }
                }
                if (!double.IsNaN(zone))
                    (best, bestOf) = (zone, zoneOf);
                // the inner bulkhead behind it (docs/shipgen/protection-plan.md H4), as the tracer adds it
                double inner = 0;
                foreach (ref readonly var b in c.Bulkheads)
                    if (b.Side == Fleetwright.Sim.Ships.Side.Starboard && b.ArmourMm > 0 && x >= b.X0 && x <= b.X1 && z >= b.ArmourBottom && z <= b.ArmourTop)
                        inner = Math.Max(inner, b.ArmourMm * c.MaterialQ(b.ArmourMaterial, b.ArmourMm, cal));
                // and the inclined plates (a deck's slopes, an internal belt: protection-plan.md H6), as thick as a level line
                // through them
                foreach (ref readonly var p in c.Inclined)
                    if (x >= p.X0 && x <= p.X1 && z >= p.Z0 && z <= p.Z1 && p.YAt(c.Hull, x, z) > 0)
                        inner += p.MmAt(z) * c.MaterialQ(p.Material, p.MmAt(z), cal) / Rise(p);
                mm[i * nz + k] = bestOf >= 0 ? Face(c, best + inner, best, c.SidePlates[bestOf].ThicknessMm, c.SidePlates[bestOf].Material) : new MatchupFace(1, inner, 0, 0, false);
            }
        return Shares(mm);
    }

    /// <summary>A point of a face: its armour, and its outer plate if that's face-hardened.</summary>
    static MatchupFace Face(ShipClass c, double mm, double outerMm, double outerRawMm, int material) =>
        material >= 0 && c.MaterialOf(material) is { FaceHardened: true } m
            ? new MatchupFace(1, mm, outerMm, outerRawMm, m.FaceShatters)
            : new MatchupFace(1, mm, 0, 0, false);

    /// <summary>The deck seen from above, sampled along its length: the armour decks over each point added up.</summary>
    static MatchupFace[] DeckArmour(ShipClass c, double cal)
    {
        const int nx = 48;
        double x0 = -c.Length / 2;
        var mm = new double[nx];
        var faces = new MatchupFace[nx];
        for (int i = 0; i < nx; i++)
        {
            double x = x0 + (i + 0.5) * c.Length / nx;
            double hard = 0, hardRaw = 0;
            int hardOf = -1;
            foreach (var d in c.ArmourDecks)
                if (x >= d.X0 && x <= d.X1)   // a box's deck by its share of the beam, a sloped deck's flat by its own
                {
                    double t = d.ThicknessMm * c.MaterialQ(d.Material, d.ThicknessMm, cal) * (double.IsNaN(d.W) ? 1 : Math.Min(1, d.W / c.Beam))
                               * (double.IsNaN(d.SlopeReach) ? 1 : Math.Max(0, 1 - 2 * d.SlopeReach / c.Beam));
                    mm[i] += t;
                    if (d.Material >= 0 && c.MaterialOf(d.Material).FaceHardened && t > hard)
                        (hard, hardRaw, hardOf) = (t, d.ThicknessMm, d.Material);
                }
            // the slopes by theirs, as thick as a line straight down through them
            foreach (var p in c.Inclined)
                if (x >= p.X0 && x <= p.X1 && p.Kind == InclinedKind.Slope)
                {
                    double reach = Math.Abs(p.U1 - p.U0), along = Math.Sqrt(reach * reach + (p.Z1 - p.Z0) * (p.Z1 - p.Z0));
                    mm[i] += p.ThicknessMm * c.MaterialQ(p.Material, p.ThicknessMm, cal) * along / reach * Math.Min(1, 2 * reach / c.Beam);
                }
            faces[i] = hardOf >= 0 ? new MatchupFace(1, mm[i], hard, hardRaw, c.MaterialOf(hardOf).FaceShatters) : new MatchupFace(1, mm[i], 0, 0, false);
        }
        return Shares(faces);
    }

    /// <summary>An inclined plate's rise over its width up its face (the sine of its angle off the horizontal).</summary>
    static double Rise(in InclinedRow p)
    {
        double dz = p.Z1 - p.Z0, du = p.U1 - p.U0;
        return dz / Math.Sqrt(dz * dz + du * du);
    }


    /// <summary>The points of a face as shares of it, by their armour to the millimetre (and their face-hardened plate's).</summary>
    static MatchupFace[] Shares(MatchupFace[] points)
    {
        var by = new SortedDictionary<(double, double, double, bool), int>();
        foreach (var p in points)
        {
            var key = (Math.Round(p.Mm), Math.Round(p.HardMm), Math.Round(p.HardRawMm), p.Shatters);
            by[key] = by.GetValueOrDefault(key) + 1;
        }
        return [.. by.Select(kv => new MatchupFace((double)kv.Value / points.Length, kv.Key.Item1, kv.Key.Item2, kv.Key.Item3, kv.Key.Item4))];
    }
}

/// <summary>A share of a face (the side or the deck as a falling shell meets it): its armour, mm of plate as good as the
/// penetration formula's (0: none), and of that its face-hardened plate's, as rated and as thick as it is (0: none), and
/// whether that plate's face shatters a bare-nosed shell (docs/battle/shells-armour-damage.md P2).</summary>
public readonly record struct MatchupFace(double Share, double Mm, double HardMm, double HardRawMm, bool Shatters);

/// <summary>One battery as the matchup sees it: the guns bearing on a beam, how fast each fires, the rounds they carry
/// (int.MaxValue: no limit given), its fire control, and its shell's fall and striking speed by range.</summary>
public sealed class MatchupGun
{
    public int Battery { get; }
    public Battery Kind { get; }
    public double CalibreMm { get; }
    public int Guns { get; }
    public double ShellsPerGunMin { get; }
    public int Rounds { get; }
    /// <summary>The fire-control level's row in the settings' tables (local control: 0).</summary>
    public int Level { get; }
    public double PatternPct { get; }
    public ShellTypeRow Shell { get; }
    public double ReachM { get; }
    readonly double[] fall, strike;

    internal MatchupGun(int battery, in BatteryRow row, int guns, double perMin, int rounds, ShellTypeRow shell, BallisticTable t, SimSettings s)
    {
        (Battery, Kind, CalibreMm, Guns, ShellsPerGunMin, Rounds, Shell, PatternPct) = (battery, row.Kind, row.CalibreMm, guns, perMin, rounds, shell, row.PatternPct);
        Level = row.Local ? 0 : (int)row.Level;
        double hi = Math.Min(t.Key.MaxElevationDeg, t.MaxElevationDeg - 2 * BallisticTable.ElevationStep);
        ReachM = t.Fall(hi, s.MatchupMuzzleM).Range;
        int n = (int)(ReachM / MatchupProfile.RangeStepM) + 1;
        (fall, strike) = (new double[n], new double[n]);
        for (int i = 0; i < n; i++)
        {
            double e = t.ElevationFor(Math.Max(i, 0.5) * MatchupProfile.RangeStepM, s.MatchupMuzzleM);
            var f = double.IsNaN(e) ? t.Fall(hi, s.MatchupMuzzleM) : t.Fall(e, s.MatchupMuzzleM);
            (fall[i], strike[i]) = (f.FallDeg, f.StrikeMs);
        }
    }

    // against each class met: the hit chance and the out exponent by range step (MatchupProfile.RangeStepM), made the
    // first time the pair is judged (the judgement's cost is these, not the sums over them)
    readonly Dictionary<MatchupProfile, double[]> pairs = new(ReferenceEqualityComparer.Instance);

    /// <summary>Against a class at a range within reach: the hit chance (<see cref="Matchup.HitChance"/>) and the out
    /// exponent (<see cref="Matchup.OutExponent"/>), from the pair's table, linear between its steps.</summary>
    public (double Hit, double Exponent) Against(MatchupProfile t, double rangeM, SimSettings s)
    {
        if (!pairs.TryGetValue(t, out var tab))
        {
            tab = new double[2 * fall.Length];
            for (int i = 0; i < fall.Length; i++)
            {
                double r = Math.Min(Math.Max(i, 0.5) * MatchupProfile.RangeStepM, ReachM);
                (tab[2 * i], tab[2 * i + 1]) = (Matchup.HitChance(this, t, r, s), Matchup.OutExponent(this, t, r, s));
            }
            pairs[t] = tab;
        }
        double u = Math.Clamp(rangeM / MatchupProfile.RangeStepM, 0, fall.Length - 1);
        int k = Math.Min((int)u, fall.Length - 2);
        if (k < 0)
            return (tab[0], tab[1]);
        double w = u - k;
        return (tab[2 * k] + w * (tab[2 * k + 2] - tab[2 * k]), tab[2 * k + 1] + w * (tab[2 * k + 3] - tab[2 * k + 1]));
    }

    /// <summary>The fall (degrees under the horizontal) and striking speed at a range within reach.</summary>
    public (double FallDeg, double StrikeMs) At(double rangeM)
    {
        double u = Math.Clamp(rangeM / MatchupProfile.RangeStepM, 0, fall.Length - 1);
        int i = Math.Min((int)u, fall.Length - 2);
        if (i < 0)
            return (fall[0], strike[0]);
        double w = u - i;
        return (fall[i] + w * (fall[i + 1] - fall[i]), strike[i] + w * (strike[i + 1] - strike[i]));
    }
}

/// <summary>Ships of one class on a side of a matchup (a count, fractional for a contact spread over the classes it may
/// be), and what they still have: their guns working (main and the rest, shares), their rounds left (shares of the
/// outfit), their torpedoes left (a share), their speed (a share of the designed) and the toughness left in them (a
/// share: less once they're seen low or listing).</summary>
public readonly record struct MatchupEntry(MatchupProfile Profile, double Count, double MainGuns = 1, double OtherGuns = 1, double MainRounds = 1,
    double OtherRounds = 1, double TorpedoesLeft = 1, double Speed = 1, double Toughness = 1)
{
    public double SpeedKn => Profile.SpeedKn * Speed;
    public double GunShare(MatchupGun g) => g.Kind == Battery.Main ? MainGuns : OtherGuns;
    public double RoundsShare(MatchupGun g) => g.Kind == Battery.Main ? MainRounds : OtherRounds;
}

/// <summary>How the engagement judged is fought: the lines' gunfight, and each side's torpedo attack, if it makes one
/// (its ships built for it run in on the other's line while the lines fight).</summary>
[Flags]
public enum MatchupStyle : byte
{
    Gunfight = 0,
    AttackByA = 1,
    AttackByB = 2,
}

/// <summary>An engagement's terms, for the bench and the tests: the lines' range and each side's attack's firing range
/// (0: none), what each side's guns and torpedoes put out of the fight (ships, expected), the attackers lost on the way
/// in, how long it lasts, and the shares of each side's tonnage lost.</summary>
public readonly record struct MatchupTerms(double RangeM, MatchupStyle Style, double AttackA, double AttackB, double Minutes, double GunsOnB,
    double TorpedoesOnB, double GunsOnA, double TorpedoesOnA, double RunInLost, double LostA, double LostB)
{
    /// <summary>A's confidence: the share of B's tonnage it puts out for the share of its own it loses.</summary>
    public double Confidence(double floor) => Math.Max(LostB, floor) / Math.Max(LostA, floor);
}

/// <summary>A's judgement against B: its confidence at the range and style the faster side picks (or A's own at about
/// equal speed), both sides' speeds (each its slowest ship's), and the terms behind it.</summary>
public readonly record struct MatchupResult(double Confidence, double SpeedA, double SpeedB, MatchupTerms Terms)
{
    /// <summary>A can refuse the fight (it's the faster by the margin).</summary>
    public bool ACanRefuse(SimSettings s) => SpeedA > SpeedB + s.MatchupSpeedMarginKn;
    /// <summary>B can refuse it, or force it on A.</summary>
    public bool BCanRefuse(SimSettings s) => SpeedB > SpeedA + s.MatchupSpeedMarginKn;
}

/// <summary>Who can fight whom (docs/battle/command.md 5.1g): not a strength, a matchup. How fast each side puts the
/// other's ships out of the fight, from the classes alone (v0 is analytic; the coarse tier's tables, 4.8-4.9, can replace
/// its numbers later): guns by the quick hit chance and what a hit does through the armour, torpedoes by the boats that
/// get to their range through the defence's fire (shared among them, so numbers saturate it) and what a hit does. A side's
/// fire goes where it does most. A's confidence is the share of B's tonnage it puts out against the share of its own it
/// loses, over the engagement: Lanchester's square law falls out of it (twice the ships, four times the confidence). Pure;
/// it allocates nothing.</summary>
public static class Matchup
{
    /// <summary>The most classes a side may have in one judgement.</summary>
    public const int MaxClasses = 64;

    /// <summary>A's judgement of B. Each side's gun range is its doctrine's share of its main guns' reach: the lines fight
    /// at the faster side's (by the margin) best of the two, else at A's. A side's ships built for torpedo attacks attack
    /// if that's better for it than not (each judged against the other not attacking). The defence's guns open on an
    /// attack from their reach, or from <paramref name="sightM"/> if that's less (by night), and the lines fight no further
    /// off than that.</summary>
    public static MatchupResult Judge(ReadOnlySpan<MatchupEntry> a, ReadOnlySpan<MatchupEntry> b, double rangeShareA, double rangeShareB, SimSettings s,
        double sightM = double.PositiveInfinity)
    {
        double speedA = Slowest(a), speedB = Slowest(b);
        bool aPicks = speedA > speedB + s.MatchupSpeedMarginKn, bPicks = speedB > speedA + s.MatchupSpeedMarginKn;
        double gunA = Math.Min(rangeShareA * Reach(a), sightM), gunB = Math.Min(rangeShareB * Reach(b), sightM);
        double line = gunA > 0 ? gunA : gunB;
        var plain = Engage(a, b, line, MatchupStyle.Gunfight, s, sightM);
        if ((aPicks || bPicks) && gunB > 0 && gunB != line)
        {
            var other = Engage(a, b, gunB, MatchupStyle.Gunfight, s, sightM);
            double c0 = plain.Confidence(s.MatchupFloor), c1 = other.Confidence(s.MatchupFloor);
            if (aPicks ? c1 > c0 : c1 < c0)
                (line, plain) = (gunB, other);
        }
        double c = plain.Confidence(s.MatchupFloor);
        var style = MatchupStyle.Gunfight;
        if (AttackRange(a, s) > 0 && Engage(a, b, line, MatchupStyle.AttackByA, s, sightM).Confidence(s.MatchupFloor) > c)
            style |= MatchupStyle.AttackByA;
        if (AttackRange(b, s) > 0 && Engage(a, b, line, MatchupStyle.AttackByB, s, sightM).Confidence(s.MatchupFloor) < c)
            style |= MatchupStyle.AttackByB;
        var t = style == MatchupStyle.Gunfight ? plain : Engage(a, b, line, style, s, sightM);
        return new MatchupResult(t.Confidence(s.MatchupFloor), speedA, speedB, t);
    }

    /// <summary>The engagement: the lines' gunfight at a range for <see cref="SimSettings.MatchupFightMin"/>, each ship
    /// firing one salvo of torpedoes within their range; and the attack of each side the style names: its ships built for
    /// torpedo attacks run in from where the other's guns open (their reach, or the sight by night) to their firing
    /// range, under fire at the mean range, fire one salvo from the boats still in it, and run out again, the defence's
    /// guns choosing between them and the line by what they do. Every gun fires at what it does most to, at the range
    /// between them (the lines' range, or the attack's).</summary>
    public static MatchupTerms Engage(ReadOnlySpan<MatchupEntry> a, ReadOnlySpan<MatchupEntry> b, double rangeM, MatchupStyle style, SimSettings s,
        double sightM = double.PositiveInfinity)
    {
        if (a.Length > MaxClasses || b.Length > MaxClasses)
            throw new ArgumentException($"a matchup takes at most {MaxClasses} classes a side");
        Span<double> outA = stackalloc double[a.Length], outB = stackalloc double[b.Length];
        Span<double> preA = stackalloc double[a.Length], preB = stackalloc double[b.Length];
        Span<bool> inA = stackalloc bool[a.Length], inB = stackalloc bool[b.Length];
        outA.Clear();
        outB.Clear();
        inA.Clear();
        inB.Clear();
        double attackA = (style & MatchupStyle.AttackByA) != 0 ? Attackers(a, s, inA) : 0;
        double attackB = (style & MatchupStyle.AttackByB) != 0 ? Attackers(b, s, inB) : 0;

        // the attacks: in, the salvo, fired, and out; the lines fighting on beside them
        double inMinA = 0, inMinB = 0, midA = rangeM, midB = rangeM;
        if (attackA > 0)
            (inMinA, midA) = RunIn(a, inA, b, attackA, sightM);
        if (attackB > 0)
            (inMinB, midB) = RunIn(b, inB, a, attackB, sightM);
        double inMin = Math.Max(inMinA, inMinB), fireMin = attackA > 0 || attackB > 0 ? s.MatchupTorpedoFireMin : 0, runIn = 0;
        double torpOnA = 0, torpOnB = 0;
        if (attackA > 0 || attackB > 0)
        {
            Phase(a, inA, b, inB, rangeM, midA, midB, inMin, s, outA, outB, s.MatchupRunInHitShare);
            runIn = Sum(outA, inA) + Sum(outB, inB);
            // the salvos are fired at once: each side's boats still in it as they were before either
            outA.CopyTo(preA);
            outB.CopyTo(preB);
            if (attackA > 0)
                torpOnB += Torpedoes(a, inA, b, attackA, s, outB, preA, 1);
            if (attackB > 0)
                torpOnA += Torpedoes(b, inB, a, attackB, s, outA, preB, 1);
            Phase(a, inA, b, inB, rangeM, attackA > 0 ? attackA : rangeM, attackB > 0 ? attackB : rangeM, fireMin, s, outA, outB);
            Phase(a, inA, b, inB, rangeM, midA, midB, inMin, s, outA, outB, s.MatchupRunInHitShare);
        }
        // the attackers are out of it: the lines alone for the rest of the fight
        double attackMin = 2 * inMin + fireMin, rest = Math.Max(s.MatchupFightMin - attackMin, 0);
        Span<bool> lineA = stackalloc bool[a.Length], lineB = stackalloc bool[b.Length];
        for (int i = 0; i < a.Length; i++)
            lineA[i] = !inA[i];
        for (int i = 0; i < b.Length; i++)
            lineB[i] = !inB[i];
        Guns(a, lineA, default, b, lineB, default, rangeM, rangeM, rangeM, rest, s, outB);
        Guns(b, lineB, default, a, lineA, default, rangeM, rangeM, rangeM, rest, s, outA);
        outA.CopyTo(preA);
        outB.CopyTo(preB);
        torpOnB += Torpedoes(a, lineA, b, rangeM, s, outB, preA, s.TorpedoFireShare);
        torpOnA += Torpedoes(b, lineB, a, rangeM, s, outA, preB, s.TorpedoFireShare);
        double gunsOnA = Sum(outA, default) - torpOnA, gunsOnB = Sum(outB, default) - torpOnB;
        return new MatchupTerms(rangeM, style, attackA, attackB, Math.Max(attackMin, s.MatchupFightMin), gunsOnB, torpOnB, gunsOnA, torpOnA, runIn,
            Lost(a, outA), Lost(b, outB));
    }

    /// <summary>A side's ships built for torpedo attacks with torpedoes left and under way (marked), and their firing
    /// range: the shortest of theirs; 0 with none.</summary>
    static double Attackers(ReadOnlySpan<MatchupEntry> side, SimSettings s, Span<bool> mark)
    {
        double r = 0;
        for (int i = 0; i < side.Length; i++)
        {
            ref readonly var e = ref side[i];
            mark[i] = e.Count > 0 && e.TorpedoesLeft > 0 && e.SpeedKn > s.MatchupSpeedMarginKn && e.Profile.Attacker(s);
            if (mark[i])
                r = r == 0 ? e.Profile.AttackRangeM(s) : Math.Min(r, e.Profile.AttackRangeM(s));
        }
        return r;
    }

    /// <summary>An attack's run in: minutes from where the defence's guns open to the firing range at the attackers'
    /// speed, and the mean range on the way.</summary>
    static (double Minutes, double MidM) RunIn(ReadOnlySpan<MatchupEntry> att, ReadOnlySpan<bool> inAtt, ReadOnlySpan<MatchupEntry> def, double fireM,
        double sightM)
    {
        double open = Math.Min(MaxReach(def), sightM), kn = double.MaxValue;
        for (int i = 0; i < att.Length; i++)
            if (inAtt[i])
                kn = Math.Min(kn, att[i].SpeedKn);
        double ms = Math.Max(kn == double.MaxValue ? 1 : kn, 1) * Handling.MsPerKnot;
        return (Math.Max(open - fireM, 0) / ms / 60, (Math.Max(open, fireM) + fireM) / 2);
    }

    /// <summary>A stretch of an attack: every ship of both sides fires (the lines at each other at the lines' range, at
    /// an attacker at its side's attack range for the stretch).</summary>
    static void Phase(ReadOnlySpan<MatchupEntry> a, ReadOnlySpan<bool> inA, ReadOnlySpan<MatchupEntry> b, ReadOnlySpan<bool> inB, double lineM,
        double attackAM, double attackBM, double minutes, SimSettings s, Span<double> outA, Span<double> outB, double evaded = 1)
    {
        Guns(a, default, inA, b, default, inB, lineM, attackAM, attackBM, minutes, s, outB, evaded);
        Guns(b, default, inB, a, default, inA, lineM, attackBM, attackAM, minutes, s, outA, evaded);
    }

    /// <summary>The ships of <paramref name="from"/>'s guns put out of <paramref name="to"/> over some minutes (added
    /// into <paramref name="outTo"/>, by class), the ships of each that take part marked (empty: all), at the range between
    /// them: an attacker's side's attack range when either is in an attack (<paramref name="inFrom"/>,
    /// <paramref name="inTo"/>), else the lines'. Each battery's shells go over the targets by what they do there (more
    /// where they do more), at most the rounds it has left.</summary>
    static void Guns(ReadOnlySpan<MatchupEntry> from, ReadOnlySpan<bool> fromSel, ReadOnlySpan<bool> inFrom, ReadOnlySpan<MatchupEntry> to,
        ReadOnlySpan<bool> toSel, ReadOnlySpan<bool> inTo, double lineM, double fromAttackM, double toAttackM, double minutes, SimSettings s, Span<double> outTo,
        double evaded = 1)
    {
        if (minutes <= 0)
            return;
        Span<double> k = stackalloc double[to.Length];
        for (int i = 0; i < from.Length; i++)
        {
            ref readonly var e = ref from[i];
            if (e.Count == 0 || fromSel.Length > 0 && !fromSel[i])
                continue;
            bool attacking = inFrom.Length > 0 && inFrom[i];
            foreach (var g in e.Profile.Guns)
            {
                double shells = Math.Min(g.Guns * e.GunShare(g) * g.ShellsPerGunMin * minutes,
                    g.Rounds == int.MaxValue ? double.MaxValue : g.Rounds * e.RoundsShare(g)) * e.Count;
                if (shells <= 0)
                    continue;
                double sum = 0;
                for (int j = 0; j < to.Length; j++)
                {
                    k[j] = 0;
                    if (to[j].Count == 0 || toSel.Length > 0 && !toSel[j])
                        continue;
                    double r = attacking ? fromAttackM : inTo.Length > 0 && inTo[j] ? toAttackM : lineM;
                    if (r > g.ReachM)
                        continue;
                    // an attacker running in or out zigzags (the defence's fire on it spoilt by the share given)
                    var (hit, exponent) = g.Against(to[j].Profile, r, s);
                    k[j] = hit * (1 - Math.Exp(-exponent / Math.Max(to[j].Toughness, 1e-3))) * (inTo.Length > 0 && inTo[j] ? evaded : 1);
                    sum += to[j].Count * k[j];
                }
                if (sum <= 0)
                    continue;
                for (int j = 0; j < to.Length; j++)
                    outTo[j] += shells * to[j].Count * k[j] / sum * k[j];
            }
        }
    }

    /// <summary>One salvo of torpedoes from every ship of <paramref name="from"/> marked (an attack's, or a line's firing
    /// at will) with torpedoes left and the range within their reach, from the share of its ships still in the fight (<paramref name="outFrom"/> so far), at the biggest of
    /// <paramref name="to"/>'s classes; what they put out, added into <paramref name="outTo"/>.</summary>
    static double Torpedoes(ReadOnlySpan<MatchupEntry> from, ReadOnlySpan<bool> sel, ReadOnlySpan<MatchupEntry> to, double rangeM, SimSettings s,
        Span<double> outTo, ReadOnlySpan<double> outFrom, double reachShare)
    {
        int target = -1;
        for (int j = 0; j < to.Length; j++)
            if (to[j].Count > 0 && (target < 0 || to[j].Profile.DisplacementT > to[target].Profile.DisplacementT))
                target = j;
        if (target < 0)
            return 0;
        var tp = to[target].Profile;
        double near = Math.Min(s.MatchupTorpedoHitMax, s.MatchupTorpedoHitM / Math.Max(rangeM, 1)) * Math.Sqrt(tp.LengthM / s.MatchupTorpedoLengthM)
                      * s.MatchupTorpedoEvadedShare;
        double total = 0;
        for (int i = 0; i < from.Length; i++)
        {
            ref readonly var e = ref from[i];
            var pr = e.Profile;
            if (!sel[i] || pr.TubesBearing == 0 || e.TorpedoesLeft <= 0 || rangeM > reachShare * pr.Torpedo.FastRangeM && rangeM > reachShare * pr.Torpedo.SlowRangeM)
                continue;
            double left = e.Count * Math.Exp(-outFrom[i] / Math.Max(e.Count, 1e-9));
            // a firing is the captains' spread (TorpedoSpread), not the whole broadside
            double fired = Math.Min(Math.Min(pr.TubesBearing, s.TorpedoSpread), pr.Torpedoes * e.TorpedoesLeft) * left;
            // a target turning away outruns a torpedo the faster she is beside it (the bench's target steamed at the reference speed)
            double kn = pr.Torpedo.FastKn, run = Math.Clamp((kn - to[target].SpeedKn) / Math.Max(kn - s.MatchupTorpedoRefKn, 1), 0, 1.5);
            double q = (1 - pr.TorpedoDud) * (1 - Math.Exp(-s.MatchupTorpedoK * pr.Torpedo.ChargeKg / (tp.DisplacementT * to[target].Toughness)));
            total += fired * near * run * q;
        }
        outTo[target] += total;
        return total;
    }

    /// <summary>The chance a shell from a battery hits a class at a range (fire-control-research.md §8.3): the fire
    /// control's error and the salvo's dispersion over the target's hitting space, beam on.</summary>
    public static double HitChance(MatchupGun g, MatchupProfile t, double rangeM, SimSettings s)
    {
        var (fall, _) = g.At(rangeM);
        double danger = t.HeightM / Math.Tan(double.DegreesToRadians(Math.Max(fall, 0.1)));
        double depth = t.BeamM, width = t.LengthM;
        var (dr, dd) = FireControl.Dispersion(g.PatternPct, rangeM, g.Guns, s.DispersionDeflectionRatio);
        int lvl = Math.Clamp(g.Level, 0, s.MatchupMpiPct.Length - 1);
        double sr = Math.Sqrt(Sq(s.MatchupMpiPct[lvl] / 100 * rangeM) + dr * dr);
        double sd = Math.Sqrt(Sq(s.MatchupDeflectionMil[Math.Min(lvl, s.MatchupDeflectionMil.Length - 1)] * FireControl.Mil * rangeM) + dd * dd);
        double pr = Phi(depth / 2 / sr) - Phi((-depth / 2 - danger) / sr);
        double pd = 2 * Phi(width / 2 / sd) - 1;
        return Math.Max(pr * pd, 0);
    }

    /// <summary>The chance a hit from a battery at a range puts a ship of a class out of the fight: by its shell's size (the
    /// square root of its mass) and whether it bursts inside, over the side's and deck's armour as the fall angle splits the hits between them (a
    /// side hit in the upperworks reaching the vitals only by its splinters through the deck under them), over the ship's
    /// displacement (and the toughness left in her).</summary>
    public static double OutChance(MatchupGun g, in MatchupEntry target, double rangeM, SimSettings s) =>
        1 - Math.Exp(-OutExponent(g, target.Profile, rangeM, s) / Math.Max(target.Toughness, 1e-3));

    /// <summary><see cref="OutChance"/>'s exponent for a whole ship (divided by the toughness left in her).</summary>
    public static double OutExponent(MatchupGun g, MatchupProfile t, double rangeM, SimSettings s)
    {
        var (fall, strike) = g.At(rangeM);
        double danger = t.HeightM / Math.Tan(double.DegreesToRadians(Math.Max(fall, 0.1)));
        double side = danger / (danger + t.BeamM);
        var shell = g.Shell;
        // as the terminal has it: a common shell beats the plate its capability's share of an AP's
        double common = shell.Kind == ShellKind.Common ? s.CommonPenShare : 1;
        double penSide = Terminal.PenMm(shell, strike, fall) * common, penDeck = Terminal.PenMm(shell, strike, 90 - fall) * common;
        // and what it beats just over its limit, where a body may break getting through (P2)
        double near = strike / s.NearLimitBand;
        double nearSide = Terminal.PenMm(shell, near, fall) * common, nearDeck = Terminal.PenMm(shell, near, 90 - fall) * common;
        // a burst where there's no armour (the upperworks, the ends) reaches the vitals by its splinters through the deck
        double bare = Math.Exp(-t.DeckMeanMm / s.MatchupSplinterMm);
        double upper = (shell.Fuze == Fuze.Nose || t.SidePlatingMm >= Terminal.FuzeArmMm(shell.CalibreMm, fall, s) ? 1 : s.MatchupPassShare) * bare;
        // the faces as this calibre meets them: between the 8" and the 16" profiles
        double w = Math.Clamp((shell.CalibreMm - MatchupProfile.Eight) / MatchupProfile.Eight, 0, 1);
        double sideIn = Through(shell, t.Side, penSide, nearSide, t.SidePlatingMm, fall, bare, s),
               deckIn = Through(shell, t.Deck, penDeck, nearDeck, t.DeckPlatingMm, 90 - fall, bare, s);
        if (w > 0)
        {
            sideIn += w * (Through(shell, t.Side16, penSide, nearSide, t.SidePlatingMm, fall, bare, s) - sideIn);
            deckIn += w * (Through(shell, t.Deck16, penDeck, nearDeck, t.DeckPlatingMm, 90 - fall, bare, s) - deckIn);
        }
        double share = side * (t.UpperShare * upper + (1 - t.UpperShare) * sideIn) + (1 - side) * deckIn;
        return s.MatchupShellK * Math.Sqrt(shell.MassKg) * share / t.DisplacementT;
    }

    /// <summary>The share of a shell's effect that gets in through a face: a plate it passes whole bursts it inside (a
    /// base fuze, as its quality's outcomes have it) or keeps it out; a nose fuze bursts on any plate; where there's only
    /// plating it bursts there, its splinters reaching the vitals by the share <paramref name="bare"/> (through the deck's
    /// armour), but a base fuze that the plating can't arm passes through. As the terminal has it (P2): a face-hardened
    /// plate is thicker by the body's quality, and one that shatters the shell (a bare nose, by the cap's share at the
    /// angle, or a body past its limits) keeps it out whether it gets through or not; just over the limit
    /// (<paramref name="penNear"/>, what it beats at its speed over the band) a body may break. The cap is taken as on:
    /// what plating ahead knocks off is the terminal's.</summary>
    static double Through(in ShellTypeRow shell, MatchupFace[] face, double pen, double penNear, double platingMm, double obliquityDeg, double bare,
        SimSettings s)
    {
        double sum = 0, good = Good(shell, s), kept = s.MatchupKeptShare;
        double bareNose = Terminal.BareShare(shell.Cap, obliquityDeg), breaks = Terminal.NearLimitBreak(obliquityDeg, s);
        foreach (var x in face)
        {
            double f;
            if (x.Mm <= 0)
                f = (shell.Fuze == Fuze.Nose || platingMm >= Terminal.FuzeArmMm(shell.CalibreMm, obliquityDeg, s) ? 1 : s.MatchupPassShare) * bare;
            else if (shell.Fuze == Fuze.Nose)
                f = kept;
            else
            {
                double shatter = x.HardMm <= 0 ? 0
                    : Terminal.ShatterChance(shell, x.Shatters, x.HardRawMm / shell.CalibreMm, obliquityDeg, bareNose, s);
                double mm = x.Mm + x.HardMm * (1 / shell.BodyQuality - 1);
                double p = Phi((pen - mm) / (s.MatchupPenSpread * mm));
                double broke = Math.Max(x.HardRawMm, x.Mm) / shell.CalibreMm >= s.BreakFromTD
                    ? Math.Max(p - Phi((penNear - mm) / (s.MatchupPenSpread * mm)), 0) * breaks : 0;
                f = (1 - shatter) * ((p - broke) * good + (1 - p + broke) * kept) + shatter * kept;
            }
            sum += x.Share * f;
        }
        return sum;
    }

    /// <summary>What a shell through armour whole does, by its quality's outcome row: a full burst, a burst in the plate or
    /// a low-order one at their shares, a dud as one kept out.</summary>
    static double Good(in ShellTypeRow shell, SimSettings s)
    {
        var o = s.ShellOutcomes[Math.Clamp((int)shell.Quality, 0, s.ShellOutcomes.Length - 1)];
        return o.Burst + o.InPlate * s.MatchupInPlateShare + o.LowOrder * s.MatchupLowOrderShare + o.Dud * s.MatchupKeptShare;
    }

    /// <summary>The share of a side's tonnage out of the fight, from the ships expected out of each class (each ship out
    /// with the chance a Poisson count gives her).</summary>
    static double Lost(ReadOnlySpan<MatchupEntry> side, ReadOnlySpan<double> outs)
    {
        double all = 0, lost = 0;
        for (int i = 0; i < side.Length; i++)
        {
            double t = side[i].Profile.DisplacementT * side[i].Count;
            all += t;
            lost += side[i].Count > 0 ? t * (1 - Math.Exp(-outs[i] / side[i].Count)) : 0;
        }
        return all > 0 ? lost / all : 0;
    }

    static double Slowest(ReadOnlySpan<MatchupEntry> side)
    {
        double v = double.MaxValue;
        foreach (ref readonly var e in side)
            if (e.Count > 0)
                v = Math.Min(v, e.SpeedKn);
        return v == double.MaxValue ? 0 : v;
    }

    /// <summary>A side's main guns' reach (its best), m.</summary>
    static double Reach(ReadOnlySpan<MatchupEntry> side)
    {
        double r = 0;
        foreach (ref readonly var e in side)
            if (e.Count > 0 && e.MainGuns > 0)
                r = Math.Max(r, e.Profile.MainReachM);
        return r;
    }

    /// <summary>A side's longest gun reach, any battery.</summary>
    static double MaxReach(ReadOnlySpan<MatchupEntry> side)
    {
        double r = 0;
        foreach (ref readonly var e in side)
            if (e.Count > 0)
                foreach (var g in e.Profile.Guns)
                    r = Math.Max(r, g.ReachM);
        return r;
    }

    /// <summary>A side's torpedo attack range: the shortest of its ships built to attack with torpedoes, 0 with none that
    /// have torpedoes left.</summary>
    static double AttackRange(ReadOnlySpan<MatchupEntry> side, SimSettings s)
    {
        double r = double.MaxValue;
        foreach (ref readonly var e in side)
            if (e.Count > 0 && e.TorpedoesLeft > 0 && e.SpeedKn > s.MatchupSpeedMarginKn && e.Profile.Attacker(s))
                r = Math.Min(r, e.Profile.AttackRangeM(s));
        return r == double.MaxValue ? 0 : r;
    }

    /// <summary>The sum of v, over the marked entries (empty: all).</summary>
    static double Sum(ReadOnlySpan<double> v, ReadOnlySpan<bool> mark)
    {
        double x = 0;
        for (int i = 0; i < v.Length; i++)
            if (mark.Length == 0 || mark[i])
                x += v[i];
        return x;
    }

    static double Sq(double x) => x * x;

    /// <summary>The standard normal's distribution function (Abramowitz and Stegun 7.1.26, to 1.5e-7).</summary>
    public static double Phi(double x)
    {
        double z = Math.Abs(x) / Math.Sqrt(2), t = 1 / (1 + 0.3275911 * z);
        double erf = 1 - ((((1.061405429 * t - 1.453152027) * t + 1.421413741) * t - 0.284496736) * t + 0.254829592) * t * Math.Exp(-z * z);
        return x >= 0 ? 0.5 * (1 + erf) : 0.5 * (1 - erf);
    }
}
