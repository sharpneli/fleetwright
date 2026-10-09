namespace Fleetwright.Shipgen;

/// <summary>geometry.Hull: the hull's planform (the main deck): half-width along the length with bow and stern tapers.</summary>
public sealed class Hull
{
    public readonly double L, B;
    public readonly PyDict Bow, Stern;
    readonly double bowTaper, bowPower, sternTaper, sternPower, transom;
    readonly string bowShape, sternShape;
    readonly double? flare;

    public Hull(PyDict spec)
    {
        L = spec.F("length");
        B = spec.F("beam");
        Bow = PyDict.Merge(PyDict.Of(("taper", 0.33), ("power", 1.6), ("shape", "pointed")), spec.Get("bow") as PyDict);
        Stern = PyDict.Merge(PyDict.Of(("taper", 0.18), ("power", 2.0), ("shape", "round"), ("transom", 0.45)),
            spec.Get("stern") as PyDict);
        bowTaper = Bow.F("taper");
        bowPower = Bow.F("power");
        bowShape = Bow.S("shape");
        flare = Bow.B("flare") ? Bow.F("flare") : null;
        sternTaper = Stern.F("taper");
        sternPower = Stern.F("power");
        sternShape = Stern.S("shape");
        transom = Stern.F("transom");
    }

    static double End(double t, double power, string shape)
    {
        double b = Py.Max(0.0, 1.0 - Py.Pow(t, power));
        return shape == "round" ? Math.Sqrt(b) : b;
    }

    /// <summary>The mean of _end over a taper (0..1): the share of the taper's L x B box its end fills.</summary>
    public static double EndFill(double power, string shape)
    {
        if (shape == "round")
            return Py.Gamma(1 + 1 / power) * Py.Gamma(1.5) / Py.Gamma(1 / power + 1.5);
        return power / (power + 1);
    }

    public double HalfWidth(double x)
    {
        double u = (x + L / 2) / L;
        double w;
        if (u >= 1 - bowTaper)
        {
            double t = Py.Min(1.0, (u - (1 - bowTaper)) / bowTaper);
            w = End(t, bowPower, bowShape);
            if (flare is double f)
                w = Py.Min(1.0, w + f * Math.Sin(Math.PI * t) * (1 - t));
        }
        else if (u <= sternTaper)
        {
            double t = Py.Min(1.0, Py.Max(0.0, 1 - u / sternTaper));
            w = transom + (1 - transom) * End(t, sternPower, sternShape);
        }
        else
            w = 1.0;
        return B / 2 * w;
    }

    /// <summary>Closed outline as a point list (port side stern-&gt;bow, then starboard bow-&gt;stern).</summary>
    public List<Pt> Points(double inset = 0.0, double? maxHw = null, double? xMin = null, double? xMax = null, int n = 260)
    {
        double lo = xMin ?? -L / 2;
        double hi = xMax ?? L / 2;
        var pts = new List<(double X, double W)>();
        for (int i = 0; i <= n; i++)
        {
            double x = lo + (hi - lo) * (1 - Math.Cos(Math.PI * i / n)) / 2;
            double w = HalfWidth(x) - inset;
            if (maxHw is double m)
                w = Py.Min(w, m);
            if (w > 0.01)
                pts.Add((x, w));
        }
        var out_ = new List<Pt>(2 * pts.Count);
        foreach (var (x, w) in pts)
            out_.Add(new Pt(x, -w));
        for (int i = pts.Count - 1; i >= 0; i--)
            out_.Add(new Pt(pts[i].X, pts[i].W));
        return out_;
    }
}

/// <summary>geometry.HullForm: the hull's cross-sections, its half-breadth at x and height z (metres above the keel).
/// See the Python docstring for the model; this is a line-for-line port.</summary>
public sealed class HullForm
{
    const int N = 400;
    const double C_MIN = 0.35, C_MAX = 0.995, KAPPA = 1.2;
    const double FOREFOOT = 0.10, FOREFOOT_FN = 0.2, FOREFOOT_K = 0.25, FOREFOOT_MAX = 0.16;
    const double ROCKER = 0.45, PLANING_CM = 0.6, CUT_CLEAR = 0.1, CUT_RUN = 7.0, TRANSOM_DEEP = 0.2;
    const double FORE_V = -0.6, FORE_U = 0.8, FORE_FN = 0.225, FORE_FN_W = 0.04;
    const double AFT_K = 0.8, CHAR_RUN = 0.3;
    const double PMB_K = 1.8, PMB_CP = 0.54, PMB_MAX = 0.5;
    const double LAMBDA = 0.5, LAMBDA_K = 0.2, LAM_MIN = 0.35, LAM_MAX = 0.75;
    const int ROUNDS = 3;
    const double TRANSOM_C = 0.75;
    const int SMOOTH = 6;
    const double C_END = 0.75, C_END_POW = 2.0, KEEL_K = 0.06, KEEL_SHARE = 0.5;
    static readonly double[] HEIGHTS = [0.0, 0.01, 0.03, 0.08, 0.15, 0.25, 0.4, 0.55, 0.7, 0.85, 1.0];

    public readonly Hull Hull;
    public readonly double Cb, T, D, Fn, CwpTarget;
    readonly bool planing;
    readonly long screws;
    readonly double kFore, kAft, forefoot;
    readonly double? post;
    readonly (double Xc, double Xs, double Rise, double End)? cut;
    readonly double[] xs, ws, ds;
    public double Cm;
    public readonly double Lcb;
    readonly (double Aft, double Fore) endsE;
    double lam;
    public double Pmb, Split, ReachedLcb, Nexp, Volume, Cwp;
    (double Xa, double Xf) mid;
    double[] us = [], cs = [];
    double[] cHi = [];
    (double, double, double, double)? staticKey;
    StaticVal? staticVal;
    readonly Dictionary<(double, double), (double P, double Q)> sectionCache = [];

    sealed record StaticVal(List<(int I, int End, double R2, double E, double Dk, double W, double Pw)> Ends,
        List<int> Fore, List<int> Aft, double[] Us, double[] Cs, double[] Area)
    {
        public readonly List<int>[] ForeAft = [Fore, Aft];
    }

    /// <summary>x in lo..hi where the increasing f(x) meets target (an end when it doesn't): Illinois regula falsi.</summary>
    static double Solve(Func<double, double> f, double lo, double hi, double target)
    {
        double fLo = f(lo) - target, fHi = f(hi) - target;
        if (fLo >= 0)
            return lo;
        if (fHi <= 0)
            return hi;
        double tol = (hi - lo) * Math.Pow(2.0, -40);
        int side = 0;
        for (int it = 0; it < 200; it++)
        {
            if (hi - lo <= tol)
                break;
            double x = (lo * fHi - hi * fLo) / (fHi - fLo);
            if (!(lo < x && x < hi))
                x = (lo + hi) / 2;
            double fx = f(x) - target;
            if (fx == 0)
                return x;
            if (fx < 0)
            {
                lo = x;
                fLo = fx;
                if (side < 0)
                    fHi /= 2;
                side = -1;
            }
            else
            {
                hi = x;
                fHi = fx;
                if (side > 0)
                    fLo /= 2;
                side = 1;
            }
        }
        return (lo + hi) / 2;
    }

    /// <summary>cwp: the waterplane coefficient to aim for; fn: the Froude number at the design speed; gear:
    /// propulsion.gear (null: a flat keel aft); lcb: the centre of buoyancy's x.</summary>
    public HullForm(Hull hull, double cb, double cwp, double T, double D, double fn = 0.0, PyDict? gear = null, double lcb = 0.0)
    {
        Hull = hull;
        Cb = cb;
        this.T = T;
        this.D = D;
        Fn = fn;
        CwpTarget = cwp;
        double L = hull.L, B = hull.B;
        gear ??= new PyDict();
        planing = gear.B("planing");
        screws = gear.I("screws", 0);
        kFore = FORE_V + (FORE_U - FORE_V) * Math.Exp(-Py.Pow((fn - FORE_FN) / FORE_FN_W, 2));
        kAft = screws == 1 ? AFT_K : screws != 0 ? -AFT_K : 0.0;
        forefoot = L * (planing ? ROCKER : Py.Min(FOREFOOT_MAX, FOREFOOT + FOREFOOT_K * Py.Max(0.0, fn - FOREFOOT_FN)));
        var props = gear.Or("propellers", null) is List<object?> pl ? pl.Cast<PyDict>().ToList() : [];
        if (!planing && screws == 1 && gear.B("rudders"))
            post = Py.Min(gear.L("rudders").Cast<PyDict>().Select(r => r.F("x0")));
        else if (!planing && props.Count > 0)
        {
            double rise = Py.Min(0.95 * T, Py.Max(props.Select(p => p.F("z") + p.F("diameter") * (0.5 + CUT_CLEAR))));
            double xC = Py.Max(props.Select(p => p.F("x") + 0.5 * p.F("diameter")));
            double end = Py.Max(rise, T * (1.0 - TRANSOM_DEEP * Py.Min(1.0, hull.HalfWidth(-L / 2) / (B / 2))));
            cut = (xC, xC + CUT_RUN * rise, rise, end);
        }
        xs = new double[N];
        ws = new double[N];
        ds = new double[N];
        for (int k = 0; k < N; k++)
            xs[k] = -L / 2 + L * (k + 0.5) / N;
        for (int k = 0; k < N; k++)
            ws[k] = Py.Min(1.0, hull.HalfWidth(xs[k]) / (B / 2));
        for (int k = 0; k < N; k++)
            ds[k] = Py.Max(0.0, T - Keel(xs[k])) / T;
        Cm = planing ? PLANING_CM : Geometry.MidshipCoefficient(cb);
        Lcb = Py.Max(-0.2 * L, Py.Min(0.2 * L, lcb));
        double loEnd = Py.Min(0.99, ws[0] * ds[0] * TRANSOM_C / Cm);
        endsE = (loEnd, 0.0);
        lam = LAMBDA;
        for (int r = 0; r < ROUNDS; r++)
        {
            Fit();
            lam = Solve(l => -WaterplaneCoef(l), LAM_MIN, LAM_MAX, -cwp);
        }
        Fit();
        if (Math.Abs(Volume - cb) > 1e-4)
        {
            Cm = Solve(cm => { Fit(cm); return Volume; }, C_MIN, C_MAX, cb);
            Fit(Cm);
        }
        Cwp = WaterplaneCoef(lam);
    }

    double WaterplaneCoef(double l)
    {
        var u = Stations(Nexp, Split, l).Us;
        int k = 0;
        for (int i = 0; i < N; i++)
            if (ds[i] > 0)
                bufSel[k++] = u[i];
        return Py.Sum(bufSel.AsSpan(0, k)) / N;
    }

    void Fit(double? cmArg = null)
    {
        double cm = cmArg ?? Cm;
        double L = Hull.L;
        Pmb = Py.Max(0.0, Py.Min(PMB_MAX, PMB_K * (Cb / cm - PMB_CP))) * L;
        double xa = Lcb - Pmb / 2, xf = Lcb + Pmb / 2;
        (xa, xf) = (Py.Max(xa, -0.45 * L), Py.Min(xf, 0.45 * L));
        mid = (xa, xf);

        (double[] Us, double[] Cs, double[] Area) St(double n, double d) => Stations(n, d, lam, cm, xa, xf);

        double Vol(double n, double d) => Py.Sum(St(n, d).Area.AsSpan()) / N;

        (double C, double N) Centre(double d)
        {
            double n = Solve(nn => Vol(nn, d), 0.3, 20.0, Cb);
            var a = St(n, d).Area;
            double tot = Py.Sum(a.AsSpan());
            for (int i = 0; i < N; i++)
                bufSel[i] = xs[i] * a[i];
            return (tot > 0 ? Py.Sum(bufSel.AsSpan()) / tot : 0.0, n);
        }

        Split = Solve(d => Centre(d).C, -2.0, 2.0, Lcb);
        (ReachedLcb, Nexp) = Centre(Split);
        var fin = St(Nexp, Split);
        us = (double[])fin.Us.Clone();
        cs = (double[])fin.Cs.Clone();
        Volume = Py.Sum(fin.Area.AsSpan()) / N;
    }

    // Stations' scratch: valid until the next call (the callers sum them or copy them out). The midbody and dry
    // stations come from the _static arrays and never change for a static key; each call rewrites the same end
    // stations, so the buffers are refilled from _static only when its key changes.
    readonly double[] bufUs = new double[N], bufCs = new double[N], bufArea = new double[N];
    readonly double[] bufS = new double[N], bufU = new double[N], bufRaw = new double[N], bufSel = new double[N];
    StaticVal? bufFrom;

    /// <summary>The stations' waterlines, fullness and areas (shared scratch arrays, see above).</summary>
    (double[] Us, double[] Cs, double[] Area) Stations(double n, double d, double lam0, double? cmArg = null,
        double? xaArg = null, double? xfArg = null)
    {
        double cm = cmArg ?? Cm;
        double xa, xf;
        if (xaArg is null)
            (xa, xf) = mid;
        else
            (xa, xf) = (xaArg.Value, xfArg!.Value);
        var st = Static(lam0, cm, xa, xf);
        if (!ReferenceEquals(bufFrom, st))
        {
            Array.Copy(st.Us, bufUs, N);
            Array.Copy(st.Cs, bufCs, N);
            Array.Copy(st.Area, bufArea, N);
            bufFrom = st;
        }
        double inv0 = 1 / (n * Math.Exp(d)), inv1 = 1 / (n * Math.Exp(-d));
        var rawS = bufS;
        var uOf = bufU;
        foreach (var (i, end, rr, e, dk, w, pw) in st.Ends)
        {
            double s = e + (1 - e) * Py.Pow(1 - rr, end == 0 ? inv0 : inv1);
            double rho = dk > 0 ? s / dk : 0.0;
            rawS[i] = s;
            uOf[i] = rho > 0 ? Py.Min(w, Py.Pow(rho, pw)) : 0.0;
        }
        int h = SMOOTH;
        var raw = bufRaw;
        foreach (var endList in st.ForeAft)
        {
            double uMax = 0.0;
            for (int j = 0; j < endList.Count; j++)
            {
                int i = endList[j];
                uMax = Py.Min(ws[i], Py.Max(uMax, uOf[i]));
                raw[j] = uMax;
            }
            for (int j = 0; j < endList.Count; j++)
            {
                int i = endList[j];
                int a = Math.Max(0, j - h), b = Math.Min(endList.Count, j + h + 1);
                uOf[i] = Py.Min(ws[i], Py.Sum(raw.AsSpan(a, b - a)) / (b - a));
            }
        }
        var usOut = bufUs;
        var csOut = bufCs;
        var areaOut = bufArea;
        foreach (var (i, end, rr, e, dk, w, pw) in st.Ends)
        {
            if (dk <= 0 || w <= 0)
                continue;
            double s = rawS[i], u = uOf[i];
            double chi = cHi[i];
            double c = u > 0 ? cm * s / (u * dk) : C_MIN;
            if (c > chi)
                (c, u) = (chi, Py.Min(u, cm * s / (dk * chi)));
            else if (c < C_MIN)
                (c, u) = (C_MIN, Py.Min(u, cm * s / (dk * C_MIN)));
            usOut[i] = u;
            csOut[i] = c;
            areaOut[i] = u * dk * c;
        }
        return (usOut, csOut, areaOut);
    }

    StaticVal Static(double lam0, double cm, double xa, double xf)
    {
        var key = (lam0, cm, xa, xf);
        if (staticKey is { } k && k.Equals(key))
            return staticVal!;
        double L = Hull.L;
        var ends = new List<(int, int, double, double, double, double, double)>();
        var usA = new double[N];
        var csA = new double[N];
        var areaA = new double[N];
        var chis = new double[N];
        for (int i = 0; i < N; i++)
        {
            double x = xs[i], w = ws[i], dk = ds[i];
            double r, e, kk;
            int end;
            if (x > xf)
                (r, end, e, kk) = ((x - xf) / (L / 2 - xf), 0, endsE.Fore, kFore);
            else if (x < xa)
                (r, end, e, kk) = ((xa - x) / (xa + L / 2), 1, endsE.Aft, kAft);
            else
                (r, end, e, kk) = (0.0, -1, 1.0, 0.0);
            r = Py.Min(1.0, r);
            double chi = Py.Min(C_MAX, cm - Py.Max(0.0, cm - C_END) * Py.Pow(r, C_END_POW));
            chis[i] = chi;
            double pw = Py.Max(0.05, lam0 + LAMBDA_K * kk);
            double u = double.NaN, c = double.NaN, a = double.NaN;
            if (dk <= 0 || w <= 0)
                (u, c, a) = (w, cm, 0.0);
            else if (end == -1)
            {
                double s = e + (1 - e) * Py.Pow(1 - r * r, 1.0);
                double rho = s / dk;
                u = rho > 0 ? Py.Min(w, Py.Pow(rho, pw)) : 0.0;
                c = u > 0 ? cm * s / (u * dk) : C_MIN;
                if (c > chi)
                    (c, u) = (chi, Py.Min(u, cm * s / (dk * chi)));
                else if (c < C_MIN)
                    (c, u) = (C_MIN, Py.Min(u, cm * s / (dk * C_MIN)));
                a = u * dk * c;
            }
            if (end != -1)
                ends.Add((i, end, r * r, e, dk, w, pw));
            usA[i] = u;
            csA[i] = c;
            areaA[i] = a;
        }
        var fore = new List<int>();
        for (int i = N - 1; i >= 0; i--)
            if (xs[i] > xf && ds[i] > 0)
                fore.Add(i);
        var aft = new List<int>();
        for (int i = 0; i < N; i++)
            if (xs[i] < xa && ds[i] > 0)
                aft.Add(i);
        cHi = chis;
        staticKey = key;
        staticVal = new StaticVal(ends, fore, aft, usA, csA, areaA);
        return staticVal;
    }

    /// <summary>A station table's value at x, linear between the stations' centres.</summary>
    double At(double[] vals, double x)
    {
        double f = (x + Hull.L / 2) / Hull.L * N - 0.5;
        int i = (int)Math.Max(0, Math.Min(N - 2, Py.Floor(f)));
        double t = Py.Max(0.0, Py.Min(1.0, f - i));
        return vals[i] + (vals[i + 1] - vals[i]) * t;
    }

    /// <summary>The keel's height over the baseline at x.</summary>
    public double Keel(double x)
    {
        double L = Hull.L;
        double z = 0.0;
        double run = L / 2 - x;
        if (run < forefoot)
        {
            double r = 1.0 - run / forefoot;
            z = planing ? T * r * r : T * (1.0 - Math.Sqrt(Py.Max(0.0, 1.0 - r * r)));
        }
        if (post is double p && x < p)
            z = T;
        else if (cut is var (xC, xS, rise, end))
        {
            if (xC <= x && x < xS)
                z = Py.Max(z, rise * Py.Pow((xS - x) / (xS - xC), 2));
            else if (x < xC)
            {
                double run2 = xC + L / 2;
                double k = end > rise + 1e-6 ? Py.Max(1.0, 2.0 * rise / (xS - xC) * run2 / (end - rise)) : 1.0;
                z = Py.Max(z, rise + (end - rise) * (1.0 - Py.Pow(1.0 - Py.Min(1.0, (xC - x) / run2), k)));
            }
        }
        return Py.Min(T, z);
    }

    /// <summary>The waterplane's half-breadth at x.</summary>
    public double Waterline(double x) => Py.Min(Hull.HalfWidth(x), Hull.B / 2 * At(us, x));

    /// <summary>The section's fullness below the waterline at x.</summary>
    public double Fullness(double x) => Py.Max(C_MIN, Py.Min(C_MAX, At(cs, x)));

    /// <summary>The section's character, -1 (V) .. 1 (U), at x for its fullness c.</summary>
    public double Character(double x, double c)
    {
        double k = x > 0 ? kFore : kAft;
        return k * Py.Min(1.0, Math.Abs(x) / (CHAR_RUN * Hull.L)) * Py.Max(0.0, Py.Min(1.0, (c - 0.5) / 0.15));
    }

    static double SuperellipseFillStatic(double p, double q) => Geometry.SuperellipseFill(p, q);

    /// <summary>(p, q) of the superellipse section of fullness c and character k (memoised; a pure function).</summary>
    (double P, double Q) SectionExponents(double c, double k)
    {
        if (sectionCache.TryGetValue((c, k), out var v))
            return v;
        double e = Math.Exp(k * KAPPA);
        double lo = 0.1, hi = 500.0;
        for (int i = 0; i < 60; i++)
        {
            double nn = Math.Sqrt(lo * hi);
            (lo, hi) = SuperellipseFillStatic(nn / e, Py.Max(1.0, nn * e)) > c ? (lo, nn) : (nn, hi);
        }
        double n = Math.Sqrt(lo * hi);
        v = (n / e, Py.Max(1.0, n * e));
        sectionCache[(c, k)] = v;
        return v;
    }

    /// <summary>The half-breadth at x and z metres above the keel.</summary>
    public double HalfWidth(double x, double z)
    {
        double deck = Hull.HalfWidth(x);
        if (z >= D)
            return deck;
        double wl = Waterline(x);
        if (z >= T)
            return D > T ? wl + (deck - wl) * (z - T) / (D - T) : deck;
        double zk = Keel(x);
        if (z <= zk)
            return 0.0;
        double d = T - zk;
        double c = Fullness(x);
        if (planing && c >= 0.5)
        {
            double chine = 2.0 * d * (1.0 - c);
            return Py.Min(deck, chine > 1e-9 ? wl * Py.Min(1.0, (z - zk) / chine) : wl);
        }
        double kw = planing ? 0.0 : Py.Min(KEEL_K * Hull.B, KEEL_SHARE * wl * c);
        if (wl - kw < 1e-6)
            return Py.Min(deck, wl);
        double cCurve = Py.Max(0.2, Py.Min(C_MAX, (wl * c - kw) / (wl - kw)));
        var (p, q) = SectionExponents(Py.Round(cCurve, 3), Py.Round(planing ? 0.0 : Character(x, c), 3));
        return Py.Min(deck, kw + (wl - kw) * Py.Pow(Py.Max(0.0, 1.0 - Py.Pow((T - z) / d, q)), 1.0 / p));
    }

    /// <summary>The design waterplane: (area m², lcf, i_l, i_t m⁴), by the midpoint rule.</summary>
    public (double Area, double Lcf, double IL, double IT) Waterplane(int n = 400)
    {
        double L = Hull.L;
        double dx = L / n;
        var xsW = new double[n];
        var hw = new double[n];
        for (int i = 0; i < n; i++)
        {
            xsW[i] = -L / 2 + (i + 0.5) * dx;
            hw[i] = Waterline(xsW[i]);
        }
        double area = Py.Sum(hw.Select(y => 2 * y)) * dx;
        double lcf = Py.Sum(Enumerable.Range(0, n).Select(i => 2 * hw[i] * xsW[i])) * dx / area;
        double iL = Py.Sum(Enumerable.Range(0, n).Select(i => 2 * hw[i] * Py.Pow(xsW[i] - lcf, 2))) * dx;
        double iT = Py.Sum(hw.Select(y => 2.0 / 3 * Py.Pow(y, 3))) * dx;
        return (area, lcf, iL, iT);
    }

    /// <summary>Sampled sections for the hitboxes: [(x, z[], y[])], z on the keel scale.</summary>
    public List<(double X, List<double> Z, List<double> Y)> Table(int stations = 48)
    {
        double L = Hull.L;
        double top = Py.Min(T, D);
        var out_ = new List<(double, List<double>, List<double>)>();
        for (int k = 0; k <= stations; k++)
        {
            double x = -L / 2 + L * (1 - Math.Cos(Math.PI * k / stations)) / 2;
            double zk = Py.Min(Keel(x), top);
            var set = new HashSet<double>();
            foreach (var f in HEIGHTS)
                set.Add(zk + (top - zk) * f);
            set.Add(D);
            var zs = set.ToList();
            zs.Sort();
            out_.Add((x, zs, zs.Select(z => HalfWidth(x, z)).ToList()));
        }
        return out_;
    }
}
