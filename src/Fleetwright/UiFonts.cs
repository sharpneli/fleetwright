using System.Runtime.InteropServices;
using ImGuiNET;

namespace Fleetwright;

/// <summary>
/// The game UI's fonts (Content/Fonts, OFL): IBM Plex Sans Condensed for the UI, IBM Plex Mono for figures, Libre
/// Caslon Text for the period-style Legend. Added to ImGui's atlas once, before its texture is made, after ImGui's own
/// default font (which stays the default, for the tool windows). A font that fails to load falls back to the default.
/// </summary>
public static unsafe class UiFonts
{
    public static ImFontPtr Sans, SansBold, SansSmall, Title, Mono, MonoBig, Caslon, CaslonBig;

    // Latin-1, the general punctuation (dashes, primes, ellipsis) and the minus sign; ImGui keeps the pointer
    static ushort* ranges;

    public static void Load(ImFontAtlasPtr atlas)
    {
        atlas.AddFontDefault();
        if (ranges == null)
        {
            ushort[] r = [0x0020, 0x00FF, 0x2010, 0x2044, 0x2212, 0x2212, 0];
            ranges = (ushort*)NativeMemory.Alloc((nuint)(r.Length * sizeof(ushort)));
            r.CopyTo(new Span<ushort>(ranges, r.Length));
        }
        var dir = Path.Combine(AppContext.BaseDirectory, "Content", "Fonts");
        ImFontPtr Add(string file, float px)
        {
            var path = Path.Combine(dir, file);
            if (!File.Exists(path))
            {
                Console.Error.WriteLine($"font missing: {path}");
                return atlas.Fonts[0];
            }
            return atlas.AddFontFromFileTTF(path, px, null, (nint)ranges);
        }
        Sans = Add("ibm-plex-sans-condensed-latin-400-normal.ttf", 17);
        SansSmall = Add("ibm-plex-sans-condensed-latin-400-normal.ttf", 14);
        SansBold = Add("ibm-plex-sans-condensed-latin-600-normal.ttf", 14);
        Title = Add("ibm-plex-sans-condensed-latin-600-normal.ttf", 20);
        Mono = Add("ibm-plex-mono-latin-400-normal.ttf", 16);
        MonoBig = Add("ibm-plex-mono-latin-400-normal.ttf", 30);
        Caslon = Add("libre-caslon-text-latin-400-normal.ttf", 16);
        CaslonBig = Add("libre-caslon-text-latin-700-normal.ttf", 26);
    }
}
