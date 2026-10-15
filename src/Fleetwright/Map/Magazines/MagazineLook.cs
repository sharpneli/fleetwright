using System.Globalization;

namespace Fleetwright.Map.Magazines;

/// <summary>The world view's magazine explosions' knobs (docs/visuals/magazine-explosions.md), one place, for
/// side-by-side captures: <c>-magazine=at:20,knee:300</c>. Each departure from the research is named here, beside the
/// number it departs from.</summary>
public static class MagazineLook
{
    /// <summary>The scatter on each event's fireball (research §2.2: the correlations' ~30% on D and ~80% on its life),
    /// lognormal σ, from its seed.</summary>
    public static double ScatterD = 0.15, ScatterS = 0.25;

    /// <summary>How long the body (fireball, cap, stem) is drawn after the main event, s on the effects' clock; it fades
    /// over the last third (the after-smoke's puffs take the pall on, X6).</summary>
    public static double VolumeS = 120;

    /// <summary>The soot skin (research §6: the top and outside cool first, the crimson stays under): how much of the
    /// heat it takes off there by the fireball's end. Vidgen's 1.8 (the reference's 0.9 was tuned at night; by day the
    /// whole cap glowed for 10 s).</summary>
    public static double Skin = 1.8;

    /// <summary>The fire's temperature from the drawing's heat (vidgen's departure): 900 + 1100 × heat K, so heat 1 is
    /// a white-yellow 2000 K, 0.5 an orange-red 1450 K, 0.25 a deep crimson 1175 K.</summary>
    public static double ColdK = 900, HotK = 2000;

    /// <summary>The soot's albedo (research §1.2: ~0.07, a little warm) and the smoke's optical depth's scale (1 the
    /// body's own: 6 through the fireball's middle at its end; vidgen drew its particles at 0.4 of the reference's).</summary>
    public static double SootAlbedo = 0.07, Opacity = 1;

    /// <summary>The mushroom: how much of the cap's smoke rides its vortex ring (0 a plain ball), and the ring's turns
    /// a second at the fireball's end (the advected noise standing in for the circulation, decision 2).</summary>
    public static double Mushroom = 0.6, Spin = 0.12;

    /// <summary>The stem: its width over the cap's, its optical depth, and its fires' fading, s.</summary>
    public static double StemShare = 0.18, StemTau = 3, StemFadeS = 40;

    /// <summary>A gain on the fire's brightness over the physical contrast (as the flashes': 1 physical).</summary>
    public static double Gain = 1;

    /// <summary>How far the hottest fire's hue whitens, toward a warm white (a departure: the flashes roll all the way
    /// to white by brightness; a fireball at night, thousands of times the moonlit deck, then read as a flat white blob,
    /// so the fire's hue goes by its temperature and is kept past white, as the eye sees a fire).</summary>
    public static double Whiten = 0.7;

    /// <summary>The march's steps' scale (1: one every ~3 px of the stretch of the ray on the screen, 8 to 40).</summary>
    public static double Steps = 0.6;

    /// <summary>Which parts draw, for measuring each's cost: 1 the body, 2 its shadow, 4 the jets and the hit's flash, 8
    /// the debris, its trails and the sparks.</summary>
    public static int Parts = 15;

    /// <summary>A dev aid for captures: the newest event drawn at this age, s after the hit (-1: off), and kept while it
    /// is.</summary>
    public static double AtS = -1;

    /// <summary>Off: no world view magazine explosions (the table's mark stays).</summary>
    public static bool On = true;

    public static bool Set(string kv)
    {
        var p = kv.Split(':');
        if (p.Length != 2 || !double.TryParse(p[1], CultureInfo.InvariantCulture, out double v))
            return false;
        switch (p[0])
        {
            case "scatterd": ScatterD = Math.Max(v, 0); break;
            case "scatters": ScatterS = Math.Max(v, 0); break;
            case "life": VolumeS = Math.Max(v, 1); break;
            case "skin": Skin = Math.Max(v, 0); break;
            case "cold": ColdK = Math.Max(v, 300); break;
            case "hot": HotK = Math.Max(v, ColdK + 1); break;
            case "soot": SootAlbedo = Math.Clamp(v, 0, 1); break;
            case "opacity": Opacity = Math.Max(v, 0); break;
            case "mushroom": Mushroom = Math.Clamp(v, 0, 1); break;
            case "spin": Spin = v; break;
            case "stem": StemShare = Math.Max(v, 0); break;
            case "stemtau": StemTau = Math.Max(v, 0); break;
            case "stemfade": StemFadeS = Math.Max(v, 1); break;
            case "gain": Gain = Math.Max(v, 0); break;
            case "whiten": Whiten = Math.Clamp(v, 0, 1); break;
            case "steps": Steps = Math.Max(v, 0.1); break;
            case "knee": Effects.KneeM = Math.Max(v, 1); break;
            case "kneehc": Effects.KneeHcM = Math.Max(v, 1); break;
            case "parts": Parts = (int)v; break;
            case "at": AtS = v; break;
            case "on": On = v != 0; break;
            default: return false;
        }
        return true;
    }

    public const string Knobs = "scatterd, scatters, life, skin, cold, hot, soot, opacity, mushroom, spin, stem, stemtau, stemfade, gain, whiten, steps, knee, kneehc, parts (1 body, 2 shadow, 4 jets, 8 debris), at (a still at that age, s), on";
}
