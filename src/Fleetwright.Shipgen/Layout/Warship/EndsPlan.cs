namespace Fleetwright.Shipgen;

public sealed partial class Layout
{
    /// <summary>A failure, and the beam asked of the sizer, when the deck amidships is too narrow for the biggest main
    /// turret.</summary>
    static void CheckMainBeam(WarshipSite site, MainGroups g)
    {
        var (lay, B, deckHw, bats) = (site.Lay, site.B, site.DeckHw, g.All);
        var big = bats.Count > 0 ? bats.MaxBy(g => g.Reach) : null;
        if (big != null)
            lay.NeedB = Math.Max(lay.NeedB, 2 * (big.Reach + 0.6) / site.DeckRatio);
        if (big != null && big.Reach + 0.6 > deckHw)
            lay.Fail("beam", $"Main turrets{(bats.Count == 1 ? "" : $" of {big.Cal}")} are {2 * big.Reach:F1} m " +
                             $"across; the {(lay.Topside.Plain ? $"{B} m beam" : $"{2 * deckHw:F1} m deck (tumblehome on a {B} m beam)")} " +
                             $"cannot carry them (needs about {2 * (big.Reach + 0.6):F1} m). Use fewer or smaller guns.");
    }

    /// <summary>The end groups along the hull (BuildLayout): each group's turrets' x, outermost first, and the
    /// middle's ends between them (MidFwd, MidAft: the fore group's aftmost turret's clearance, the aft group's
    /// foremost's).</summary>
    sealed record EndsPlan(List<double> Fore, List<double> Aft, double MidFwd, double MidAft)
    {
        /// <summary>The groups at the clearances from the bow and stern they like, closed up on the middle when it has
        /// length to spare, or the clearances cut toward their least when it needs more than mReq leaves (a failure
        /// beyond that, and Deficit and Short for the sizer); then the whole arrangement shifted by shift, within the
        /// range the clearances allow (ShiftRange, Geo.Shift).</summary>
        public static EndsPlan Of(WarshipSite site, MainGroups g, double mReq, double shift)
        {
            var (lay, L, deckHw) = (site.Lay, site.L, site.DeckHw);
            var (F, A) = (g.F, g.A);
            int nf = g.Nf, na = g.Na;
            bool flushF = g.FlushF, flushA = g.FlushA;

            bool Fits(double x, Gun g)
            {
                if (g.Reach + 0.6 > deckHw)
                    return true;
                return lay.DeckHalfWidth(x) >= g.Reach + 0.6;
            }

            double bowPref, bowMin, stPref, stMin;
            if (nf != 0)
                (bowPref, bowMin) = (0.09 * L + 2.5 * F[0].R, 0.06 * L + 1.5 * F[0].R);
            else
                (bowPref, bowMin) = (0.14 * L, 0.06 * L);
            if (na != 0)
                (stPref, stMin) = (0.08 * L + 2.0 * A[0].R, 0.05 * L + 1.0 * A[0].R);
            else
                (stPref, stMin) = (0.12 * L, 0.05 * L);
            var offF = GroupOffsets(F);
            var offA = GroupOffsets(A);

            (List<double> Fore, List<double> Aft, double MidFwd, double MidAft) Arrangement(double bowC, double stC, double sh)
            {
                var fore = nf != 0 ? offF.Select(o => L / 2 - bowC - F[0].R + sh - o).ToList() : [];
                var aft = na != 0 ? offA.Select(o => -L / 2 + stC + A[0].R + sh + o).ToList() : [];
                int guard = 0;
                while (fore.Count > 0 && !Fits(fore[0], F[0]) && fore[0] > 0 && guard < 4000)
                {
                    for (int i = 0; i < fore.Count; i++)
                        fore[i] -= 0.25;
                    guard++;
                }
                guard = 0;
                while (aft.Count > 0 && !Fits(aft[0], A[0]) && aft[0] < 0 && guard < 4000)
                {
                    for (int i = 0; i < aft.Count; i++)
                        aft[i] += 0.25;
                    guard++;
                }
                double midFwd = fore.Count > 0 ? (flushF ? fore[^1] - F[^1].RR - 1.0 : fore[^1] - F[^1].R - F[^1].Gap) : L / 2 - bowC + sh;
                double midAft = aft.Count > 0 ? (flushA ? aft[^1] + A[^1].RR + 1.0 : aft[^1] + A[^1].R + A[^1].Gap) : -L / 2 + stC + sh;
                return (fore, aft, midFwd, midAft);
            }

            var (fore, aft, midFwd, midAft) = Arrangement(bowPref, stPref, 0.0);
            double deficit = mReq - (midFwd - midAft);
            double bowCl = bowPref, stCl = stPref;
            lay.Deficit = deficit;
            if (deficit > 0)
            {
                lay.Short.Add("length");
                double sb = bowPref - bowMin, ss = stPref - stMin;
                double take = Math.Min(deficit, sb + ss);
                if (sb + ss > 0)
                {
                    bowCl -= take * sb / (sb + ss);
                    stCl -= take * ss / (sb + ss);
                }
                (fore, aft, midFwd, midAft) = Arrangement(bowCl, stCl, 0.0);
                deficit = mReq - (midFwd - midAft);
                if (deficit > 0.5)
                    lay.Fail("length", "Not enough length amidships: the machinery, funnels and midships turrets need about " +
                                       $"{mReq:F0} m between the turret groups, but only {midFwd - midAft:F0} m is free. " +
                                       "Reduce speed, use a more compact plant, or remove a turret.");
            }
            else if (deficit < 0)
            {
                // the length to spare goes to the ends: the turret groups close up on the middle, the citadel no longer
                bowCl -= deficit * bowPref / (bowPref + stPref);
                stCl -= deficit * stPref / (bowPref + stPref);
                (fore, aft, midFwd, midAft) = Arrangement(bowCl, stCl, 0.0);
            }

            bool ShiftOk(double sh)
            {
                var (f_, a_, mf, ma) = Arrangement(bowCl, stCl, sh);
                if (f_.Count > 0 && f_[0] != L / 2 - bowCl - F[0].R + sh)
                    return false;
                if (a_.Count > 0 && a_[0] != -L / 2 + stCl + A[0].R + sh)
                    return false;
                double frontEdge = f_.Count > 0 ? f_[0] + F[0].R : mf;
                double backEdge = a_.Count > 0 ? a_[0] - A[0].R : ma;
                return L / 2 - frontEdge >= bowMin - 1e-6 && backEdge + L / 2 >= stMin - 1e-6;
            }

            double lo = 0.0, hi = 0.0;
            while (lo > -0.3 * L && ShiftOk(lo - 0.25))
                lo -= 0.25;
            while (hi < 0.3 * L && ShiftOk(hi + 0.25))
                hi += 0.25;
            lay.ShiftRange = (lo, hi);
            shift = Clamp(shift, lo, hi);
            (fore, aft, midFwd, midAft) = Arrangement(bowCl, stCl, shift);
            lay.Geo.Shift = shift;
            return new EndsPlan(fore, aft, midFwd, midAft);
        }
    }
}
