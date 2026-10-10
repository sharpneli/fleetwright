using System.Runtime.InteropServices;
using ImGuiNET;

namespace Fleetwright;

/// <summary>
/// The game UI's fonts (Content/Fonts, OFL): IBM Plex Sans Condensed for the UI (and ImGui's default, so the tool
/// scenes use it too), IBM Plex Mono for figures, Libre Caslon Text for the period-style Legend. Added to ImGui's atlas
/// once, before its texture is made, at the UI scale: the display's scale (a high-DPI screen's 150 % or 200 %) times
/// <see cref="UserScale"/>. ImGui's spacing scales with them. A font that fails to load falls back to ImGui's own.
/// </summary>
public static unsafe class UiFonts
{
    public static ImFontPtr Sans, SansBold, SansSmall, Title, Mono, MonoBig, Caslon, CaslonBig;

    /// <summary>The UI's pixels per design pixel: lay fixed sizes out in multiples of it.</summary>
    public static float Scale { get; private set; } = 1;

    /// <summary>A factor over the display's scale (-uiscale=); set before the engine starts.</summary>
    public static float UserScale { get; set; } = 1;

    // Latin-1, the general punctuation (dashes, primes, ellipsis) and the minus sign; ImGui keeps the pointer
    static ushort* ranges;

    public static void Load(ImFontAtlasPtr atlas, float displayScale)
    {
        Scale = Math.Clamp((displayScale > 0 ? displayScale : 1) * UserScale, 0.5f, 4f);
        if (ranges == null)
        {
            ushort[] r = [0x0020, 0x00FF, 0x2010, 0x2044, 0x2212, 0x2212, 0];
            ranges = (ushort*)NativeMemory.Alloc((nuint)(r.Length * sizeof(ushort)));
            r.CopyTo(new Span<ushort>(ranges, r.Length));
        }
        var dir = Path.Combine(AppContext.BaseDirectory, "Content", "Fonts");
        ImFontPtr fallback = default;
        ImFontPtr Add(string file, float px)
        {
            var path = Path.Combine(dir, file);
            if (File.Exists(path))
                return atlas.AddFontFromFileTTF(path, MathF.Round(px * Scale), null, (nint)ranges);
            Console.Error.WriteLine($"font missing: {path}");
            if (fallback.NativePtr == null)
                fallback = atlas.AddFontDefault();
            return fallback;
        }
        Sans = Add("ibm-plex-sans-condensed-latin-400-normal.ttf", 18);   // first: ImGui's default font
        SansSmall = Add("ibm-plex-sans-condensed-latin-400-normal.ttf", 15);
        SansBold = Add("ibm-plex-sans-condensed-latin-600-normal.ttf", 15);
        Title = Add("ibm-plex-sans-condensed-latin-600-normal.ttf", 21);
        Mono = Add("ibm-plex-mono-latin-400-normal.ttf", 17);
        MonoBig = Add("ibm-plex-mono-latin-400-normal.ttf", 32);
        Caslon = Add("libre-caslon-text-latin-400-normal.ttf", 17);
        CaslonBig = Add("libre-caslon-text-latin-700-normal.ttf", 27);
        ImGui.GetStyle().ScaleAllSizes(Scale);
    }
}
