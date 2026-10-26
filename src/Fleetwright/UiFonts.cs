using System.Runtime.InteropServices;
using ImGuiNET;

namespace Fleetwright;

/// <summary>
/// The game UI's fonts (Content/Fonts, OFL; docs/ui-themes/ui-style-gunmetal-natural.md "three type roles"): Barlow
/// Condensed for the UI (and ImGui's default, so the tool scenes use it too), Saira Extra Condensed in tracked capitals
/// for the panes' title strips (<see cref="Head"/>) and Red Hat Mono for figures; the ship card's poster keeps IBM Plex
/// Sans Condensed. Added to ImGui's atlas at the UI scale, which follows the window: a 1080 px tall window is 1.0,
/// 1440 px 1.33, 2160 px 2.0, times <see cref="UserScale"/>. The renderer loads them again when the window settles at a
/// height that changes the scale. ImGui's spacing scales with them. A font that fails to load falls back to ImGui's own.
/// </summary>
public static unsafe class UiFonts
{
    public static ImFontPtr Sans, SansBold, SansSmall, Title, Mono, MonoBig;

    /// <summary>A work surface's tags and kbd chips (docs/ui-language.md "The pieces").</summary>
    public static ImFontPtr MonoSmall;

    /// <summary>A pane's title strip: engraved capitals, the tracking baked into the glyphs' advance (write the title in
    /// capitals).</summary>
    public static ImFontPtr Head;

    /// <summary>The ship card's poster type (ProfileScene): the name, the headline figures and the particulars.</summary>
    public static ImFontPtr PosterTitle, PosterFigure, PosterText;

    /// <summary>The window height the sizes below are designed at (scale 1).</summary>
    public const float ReferenceHeight = 1080;

    /// <summary>The UI's pixels per design pixel: lay fixed sizes out in multiples of it.</summary>
    public static float Scale { get; private set; } = 1;

    /// <summary>A factor over the display's scale (-uiscale=); set before the engine starts.</summary>
    public static float UserScale { get; set; } = 1;

    /// <summary>The UI scale for a window <paramref name="heightPx"/> tall: in sixteenths, so a resize only reloads the
    /// fonts when the scale moves a step.</summary>
    public static float ScaleFor(float heightPx) =>
        Math.Clamp(MathF.Round(Math.Max(heightPx, 1) / ReferenceHeight * UserScale * 16) / 16, 0.5f, 4f);

    static ImGuiStyle baseStyle;   // the style before any scaling, so a reload scales from it, not from the last one
    static bool haveBase;

    // Latin-1, the general punctuation (dashes, primes, ellipsis) and the minus sign; ImGui keeps the pointer
    static ushort* ranges, arrowRanges;

    // the font Barlow's arrows come from, and its pixel size over Barlow's for the same em (ascent + descent: 1.574, 1.2)
    const string ArrowsFrom = "saira-extra-condensed-latin-500-normal.ttf";
    const float ArrowsEm = 1.574f / 1.2f;

    /// <summary>Adds the fonts to an empty <paramref name="atlas"/> at <paramref name="scale"/> (from
    /// <see cref="ScaleFor"/>) and scales ImGui's style to match.</summary>
    public static void Load(ImFontAtlasPtr atlas, float scale)
    {
        Scale = scale;
        if (ranges == null)
        {
            ushort[] r = [0x0020, 0x00FF, 0x2010, 0x2044, 0x2190, 0x2193, 0x2212, 0x2212, 0];
            ranges = (ushort*)NativeMemory.Alloc((nuint)(r.Length * sizeof(ushort)));
            r.CopyTo(new Span<ushort>(ranges, r.Length));
            ushort[] up = [0x2191, 0x2191, 0x2193, 0x2193, 0];
            arrowRanges = (ushort*)NativeMemory.Alloc((nuint)(up.Length * sizeof(ushort)));
            up.CopyTo(new Span<ushort>(arrowRanges, up.Length));
        }
        var dir = Path.Combine(AppContext.BaseDirectory, "Content", "Fonts");
        ImFontPtr fallback = default;
        var cfg = new ImFontConfigPtr(ImGuiNative.ImFontConfig_ImFontConfig());
        ImFontPtr Add(string file, float px, float tracking = 0, bool arrows = false)
        {
            var path = Path.Combine(dir, file);
            if (!File.Exists(path))
            {
                Console.Error.WriteLine($"font missing: {path}");
                if (fallback.NativePtr == null)
                    fallback = atlas.AddFontDefault();
                return fallback;
            }
            cfg.MergeMode = false;
            cfg.GlyphExtraSpacing = new System.Numerics.Vector2(MathF.Round(tracking * Scale), 0);
            var font = atlas.AddFontFromFileTTF(path, MathF.Round(px * Scale), cfg, (nint)ranges);
            var merge = Path.Combine(dir, ArrowsFrom);
            if (arrows && File.Exists(merge))
            {
                // Barlow has no ↑ ↓ (the superfiring marks): Saira's, its em matched to Barlow's
                cfg.MergeMode = true;
                cfg.GlyphExtraSpacing = default;
                atlas.AddFontFromFileTTF(merge, MathF.Round(px * Scale * ArrowsEm), cfg, (nint)arrowRanges);
            }
            return font;
        }
        Sans = Add("barlow-condensed-latin-400-normal.ttf", 14, arrows: true);   // first: ImGui's default font
        SansSmall = Add("barlow-condensed-latin-400-normal.ttf", 12, arrows: true);
        SansBold = Add("barlow-condensed-latin-600-normal.ttf", 13, arrows: true);
        Title = Add("barlow-condensed-latin-600-normal.ttf", 16, arrows: true);
        Head = Add("saira-extra-condensed-latin-700-normal.ttf", 18, tracking: 1.8f);   // an em of 11.5 px, .16 em apart
        Mono = Add("red-hat-mono-latin-400-normal.ttf", 13);
        MonoBig = Add("red-hat-mono-latin-500-normal.ttf", 22);
        MonoSmall = Add("red-hat-mono-latin-400-normal.ttf", 11);
        PosterTitle = Add("ibm-plex-sans-condensed-latin-600-normal.ttf", 46);
        PosterFigure = Add("ibm-plex-sans-condensed-latin-600-normal.ttf", 28);
        PosterText = Add("ibm-plex-sans-condensed-latin-400-normal.ttf", 16);
        cfg.Destroy();
        var style = ImGui.GetStyle();
        if (!haveBase)
        {
            baseStyle = *style.NativePtr;
            haveBase = true;
        }
        *style.NativePtr = baseStyle;
        style.ScaleAllSizes(Scale);
    }
}
