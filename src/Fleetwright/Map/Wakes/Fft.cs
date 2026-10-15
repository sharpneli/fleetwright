namespace Fleetwright.Map.Wakes;

/// <summary>A radix-2 complex FFT of one power-of-two size, in place on split real and imaginary floats: just enough for
/// the wake bake (docs/visuals/wakes.md K1). The twiddles and the bit reversal are made once per size.</summary>
public sealed class Fft
{
    readonly int n;
    readonly float[] cos, sin;
    readonly int[] rev;

    public int Size => n;

    public Fft(int n)
    {
        if (n < 2 || (n & (n - 1)) != 0)
            throw new ArgumentOutOfRangeException(nameof(n), n, "a power of two");
        this.n = n;
        cos = new float[n / 2];
        sin = new float[n / 2];
        for (int k = 0; k < n / 2; k++)
        {
            double a = 2 * Math.PI * k / n;
            (cos[k], sin[k]) = ((float)Math.Cos(a), (float)Math.Sin(a));
        }
        rev = new int[n];
        int bits = System.Numerics.BitOperations.Log2((uint)n);
        for (int i = 0; i < n; i++)
        {
            int r = 0;
            for (int b = 0; b < bits; b++)
                r |= ((i >> b) & 1) << (bits - 1 - b);
            rev[i] = r;
        }
    }

    /// <summary>The transform of (re, im), n long, in place: e^(-i...) forward, e^(+i...) inverse, unscaled.</summary>
    public void Transform(Span<float> re, Span<float> im, bool inverse)
    {
        for (int i = 0; i < n; i++)
        {
            int j = rev[i];
            if (j > i)
            {
                (re[i], re[j]) = (re[j], re[i]);
                (im[i], im[j]) = (im[j], im[i]);
            }
        }
        float sg = inverse ? 1 : -1;
        for (int size = 2; size <= n; size <<= 1)
        {
            int half = size >> 1, step = n / size;
            for (int start = 0; start < n; start += size)
                for (int k = 0; k < half; k++)
                {
                    float wr = cos[k * step], wi = sg * sin[k * step];
                    int a = start + k, b = a + half;
                    float tr = wr * re[b] - wi * im[b], ti = wr * im[b] + wi * re[b];
                    re[b] = re[a] - tr;
                    im[b] = im[a] - ti;
                    re[a] += tr;
                    im[a] += ti;
                }
        }
    }
}
