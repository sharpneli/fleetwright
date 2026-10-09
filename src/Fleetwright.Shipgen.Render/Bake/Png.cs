using System.Buffers.Binary;
using System.IO.Compression;

namespace Fleetwright.Shipgen.Render.Bake;

/// <summary>An 8-bit image: RGBA (straight alpha) or grey, rows top to bottom.</summary>
public sealed class Image8(int width, int height, int channels)
{
    public readonly int Width = width, Height = height, Channels = channels;
    public readonly byte[] Data = new byte[width * height * channels];
}

/// <summary>Just enough PNG: writes 8-bit RGBA or grey, reads the same (non-interlaced, any filter), which is what
/// cairosvg and PIL wrote for the goldens.</summary>
public static class Png
{
    static readonly byte[] Sig = [0x89, (byte)'P', (byte)'N', (byte)'G', 0x0d, 0x0a, 0x1a, 0x0a];

    public static void Save(string path, Image8 im)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        using var fs = File.Create(path);
        fs.Write(Sig);
        var ihdr = new byte[13];
        BinaryPrimitives.WriteInt32BigEndian(ihdr, im.Width);
        BinaryPrimitives.WriteInt32BigEndian(ihdr.AsSpan(4), im.Height);
        ihdr[8] = 8;
        ihdr[9] = (byte)(im.Channels == 4 ? 6 : 0);
        Chunk(fs, "IHDR", ihdr);
        int stride = im.Width * im.Channels;
        using var raw = new MemoryStream();
        using (var z = new ZLibStream(raw, CompressionLevel.Optimal, leaveOpen: true))
        {
            var row = new byte[stride + 1];
            var prev = new byte[stride];
            for (int y = 0; y < im.Height; y++)
            {
                // filter "up" for every row but the first: cheap and good on flat sprites
                var cur = im.Data.AsSpan(y * stride, stride);
                row[0] = (byte)(y == 0 ? 0 : 2);
                for (int i = 0; i < stride; i++)
                    row[i + 1] = (byte)(cur[i] - (y == 0 ? 0 : prev[i]));
                z.Write(row);
                cur.CopyTo(prev);
            }
        }
        Chunk(fs, "IDAT", raw.ToArray());
        Chunk(fs, "IEND", []);
    }

    static void Chunk(Stream s, string type, byte[] data)
    {
        Span<byte> b4 = stackalloc byte[4];
        BinaryPrimitives.WriteInt32BigEndian(b4, data.Length);
        s.Write(b4);
        var td = new byte[4 + data.Length];
        for (int i = 0; i < 4; i++)
            td[i] = (byte)type[i];
        data.CopyTo(td, 4);
        s.Write(td);
        BinaryPrimitives.WriteUInt32BigEndian(b4, HullArt.Crc32(td));
        s.Write(b4);
    }

    public static Image8 Load(string path)
    {
        var f = File.ReadAllBytes(path);
        if (!f.AsSpan(0, 8).SequenceEqual(Sig))
            throw new InvalidDataException($"{path}: not a PNG");
        int pos = 8, w = 0, h = 0, ch = 0;
        using var idat = new MemoryStream();
        while (pos < f.Length)
        {
            int len = BinaryPrimitives.ReadInt32BigEndian(f.AsSpan(pos));
            string type = System.Text.Encoding.ASCII.GetString(f, pos + 4, 4);
            var data = f.AsSpan(pos + 8, len);
            if (type == "IHDR")
            {
                w = BinaryPrimitives.ReadInt32BigEndian(data);
                h = BinaryPrimitives.ReadInt32BigEndian(data[4..]);
                if (data[8] != 8 || data[12] != 0 || data[9] is not (6 or 0 or 2 or 4))
                    throw new InvalidDataException($"{path}: only 8-bit non-interlaced RGBA, RGB, grey or grey+alpha");
                ch = data[9] switch { 6 => 4, 2 => 3, 4 => 2, _ => 1 };
            }
            else if (type == "IDAT")
                idat.Write(data);
            pos += 12 + len;
        }
        idat.Position = 0;
        using var z = new ZLibStream(idat, CompressionMode.Decompress);
        int stride = w * ch;
        var raw = new byte[(stride + 1) * h];
        z.ReadExactly(raw);
        var px = new byte[stride * h];
        for (int y = 0; y < h; y++)
        {
            int ft = raw[y * (stride + 1)];
            var src = raw.AsSpan(y * (stride + 1) + 1, stride);
            var dst = px.AsSpan(y * stride, stride);
            var up = y > 0 ? px.AsSpan((y - 1) * stride, stride) : new byte[stride];
            for (int i = 0; i < stride; i++)
            {
                int a = i >= ch ? dst[i - ch] : 0, b = up[i], c = i >= ch ? up[i - ch] : 0;
                int pred = ft switch
                {
                    0 => 0,
                    1 => a,
                    2 => b,
                    3 => (a + b) / 2,
                    4 => Paeth(a, b, c),
                    _ => throw new InvalidDataException($"{path}: filter {ft}"),
                };
                dst[i] = (byte)(src[i] + pred);
            }
        }
        // widen grey+alpha and RGB to the two layouts the rest uses
        if (ch == 4 || ch == 1)
        {
            var im = new Image8(w, h, ch);
            px.CopyTo(im.Data, 0);
            return im;
        }
        var o = new Image8(w, h, 4);
        for (int i = 0; i < w * h; i++)
            if (ch == 3)
                (o.Data[i * 4], o.Data[i * 4 + 1], o.Data[i * 4 + 2], o.Data[i * 4 + 3]) = (px[i * 3], px[i * 3 + 1], px[i * 3 + 2], 255);
            else
                (o.Data[i * 4], o.Data[i * 4 + 1], o.Data[i * 4 + 2], o.Data[i * 4 + 3]) = (px[i * 2], px[i * 2], px[i * 2], px[i * 2 + 1]);
        return o;
    }

    static int Paeth(int a, int b, int c)
    {
        int p = a + b - c, pa = Math.Abs(p - a), pb = Math.Abs(p - b), pc = Math.Abs(p - c);
        return pa <= pb && pa <= pc ? a : pb <= pc ? b : c;
    }
}

/// <summary>Mip atlases (ShipSprites.MipRects' packing): colour layers halve with a 2x2 box on premultiplied alpha (no
/// dark fringes), the height map with a 2x2 max (a coarser level never lowers a column, so shadows stay long
/// enough).</summary>
public static class Mips
{
    public static Image8 Half(Image8 im, bool max)
    {
        if (im.Width % 2 != 0 || im.Height % 2 != 0)
            throw new ArgumentException($"odd size {im.Width}x{im.Height}");
        var o = new Image8(im.Width / 2, im.Height / 2, im.Channels);
        int c = im.Channels;
        for (int y = 0; y < o.Height; y++)
            for (int x = 0; x < o.Width; x++)
            {
                int i00 = ((2 * y) * im.Width + 2 * x) * c, i01 = i00 + c, i10 = i00 + im.Width * c, i11 = i10 + c;
                int oi = (y * o.Width + x) * c;
                if (max)
                {
                    for (int k = 0; k < c; k++)
                        o.Data[oi + k] = Math.Max(Math.Max(im.Data[i00 + k], im.Data[i01 + k]), Math.Max(im.Data[i10 + k], im.Data[i11 + k]));
                    continue;
                }
                // premultiply, average, unpremultiply
                double a = 0, r = 0, g = 0, b = 0;
                foreach (int i in new[] { i00, i01, i10, i11 })
                {
                    double al = im.Data[i + 3] / 255.0;
                    a += al;
                    r += im.Data[i] * al;
                    g += im.Data[i + 1] * al;
                    b += im.Data[i + 2] * al;
                }
                if (a > 0)
                {
                    o.Data[oi] = (byte)Math.Clamp(Math.Round(r / a), 0, 255);
                    o.Data[oi + 1] = (byte)Math.Clamp(Math.Round(g / a), 0, 255);
                    o.Data[oi + 2] = (byte)Math.Clamp(Math.Round(b / a), 0, 255);
                }
                o.Data[oi + 3] = (byte)Math.Clamp(Math.Round(a / 4 * 255), 0, 255);
            }
        return o;
    }

    /// <summary>Level 0 and its halvings packed at rects ([x, y, w, h] each, level 0 first).</summary>
    public static Image8 Atlas(Image8 im, IReadOnlyList<long[]> rects, bool max)
    {
        var r1 = rects.Count > 1 ? rects[1] : null;
        int w = r1 != null ? (int)(r1[0] + r1[2]) : im.Width;
        var atlas = new Image8(w, im.Height, im.Channels);
        var level = im;
        for (int k = 0; k < rects.Count; k++)
        {
            var r = rects[k];
            if (k > 0)
                level = Half(level, max);
            int x0 = (int)r[0], y0 = (int)r[1];
            if (level.Width != r[2] || level.Height != r[3])
                throw new InvalidOperationException($"mip {k}: {level.Width}x{level.Height} vs rect [{string.Join(", ", r)}]");
            int c = im.Channels;
            for (int y = 0; y < level.Height; y++)
                Buffer.BlockCopy(level.Data, y * level.Width * c, atlas.Data, ((y0 + y) * atlas.Width + x0) * c, level.Width * c);
        }
        return atlas;
    }
}
