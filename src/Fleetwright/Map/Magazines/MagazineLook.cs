using System.Globalization;

namespace Fleetwright.Map.Magazines;

/// <summary>The world view's magazine explosions' knobs (docs/visuals/magazine-explosions.md), one place, for
/// side-by-side captures: <c>-magazine=at:20,fadeto:2</c>. Each departure from the research is named here, beside the
/// number it departs from.</summary>
public static class MagazineLook
{
    /// <summary>The scatter on each event's fireball (research §2.2: the correlations' ~30% on D and ~80% on its life),
    /// lognormal σ, from its seed.</summary>
    public static double ScatterD = 0.15, ScatterS = 0.25;

    /// <summary>When the body (the fireball and its cloud) fades out after the main event, in its fireball's lives (the
    /// user, 2026-10-15: the cap climbing a kilometre on its stem read as a flower on its stalk, so it goes early, and
    /// low): from 1 to 1.8 lives (Bismarck's: 12-22 s). The wreck's fires' soot plume carries the smoke on (X6).</summary>
    public static double FadeFrom = 1.0, FadeTo = 1.8;

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

    /// <summary>The cloud turning over at its edge: its turns a second at the fireball's end (the advected noise standing
    /// in for the circulation, decision 2).</summary>
    public static double Spin = 0.12;

    /// <summary>The billows boiling on as the cloud rises (an effect, as the gun smoke's churn): noise cells over
    /// ln(1 + t / 0.3 life), fast at first and slowing.</summary>
    public static double Churn = 1.5;

    /// <summary>The after-smoke (X6) on the smoke's puffs: a fire's puff every so many seconds while they burn (three
    /// of their fadings; far enough apart that the wind lays them out as a plume, not one blob merged at the wreck); the
    /// fires' smoke and its fading are the field's (<c>SmokeSources.WreckSmokeM2s</c>, docs/battle/smoke.md Z4), times a
    /// gain for side-by-side captures; each of the boilers' four steam puffs', m², as water fog.</summary>
    public static double FireEveryS = 6, FireSmokeGain = 1, SteamM2 = 8_000;

    /// <summary>A gain on the fire's brightness over the physical contrast (as the flashes': 1 physical).</summary>
    public static double Gain = 1;

    /// <summary>How far the hottest fire's hue whitens, toward a warm white (a departure: the flashes roll all the way
    /// to white by brightness; a fireball at night, thousands of times the moonlit deck, then read as a flat white blob,
    /// so the fire's hue goes by its temperature and is kept past white, as the eye sees a fire).</summary>
    public static double Whiten = 0.7;

    /// <summary>The march's steps' scale (1: one every ~3 px of the stretch of the ray on the screen, 8 to 40).</summary>
    public static double Steps = 0.6;

    /// <summary>Which parts draw, for measuring each's cost: 1 the body, 2 its shadow, 4 the jets and the hit's flash, 8
    /// the debris, its trails and the sparks, 16 the fire's light on the sea and the ships, 32 the after-smoke.</summary>
    public static int Parts = 63;

    /// <summary>The share of the fire's light as drawn that it throws on the sea and the ships (the research: restraint,
    /// a local pool; at full the night sea went beige).</summary>
    public static double LightShare = 0.3;

    /// <summary>A dev aid for captures: the events of the newest 2 s drawn at this age, s after the hit (-1: off), and
    /// kept while it is.</summary>
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
            case "fadefrom": FadeFrom = Math.Max(v, 0); break;
            case "fadeto": FadeTo = Math.Max(v, FadeFrom + 0.01); break;
            case "skin": Skin = Math.Max(v, 0); break;
            case "cold": ColdK = Math.Max(v, 300); break;
            case "hot": HotK = Math.Max(v, ColdK + 1); break;
            case "soot": SootAlbedo = Math.Clamp(v, 0, 1); break;
            case "opacity": Opacity = Math.Max(v, 0); break;
            case "spin": Spin = v; break;
            case "churn": Churn = Math.Max(v, 0); break;
            case "fireevery": FireEveryS = Math.Max(v, 0.2); break;
            case "firesmoke": FireSmokeGain = Math.Max(v, 0); break;
            case "steam": SteamM2 = Math.Max(v, 0); break;
            case "gain": Gain = Math.Max(v, 0); break;
            case "whiten": Whiten = Math.Clamp(v, 0, 1); break;
            case "light": LightShare = Math.Max(v, 0); break;
            case "steps": Steps = Math.Max(v, 0.1); break;
            case "parts": Parts = (int)v; break;
            case "at": AtS = v; break;
            case "on": On = v != 0; break;
            default: return false;
        }
        return true;
    }

    public const string Knobs = "scatterd, scatters, fadefrom, fadeto, skin, cold, hot, soot, opacity, spin, churn, fireevery, firesmoke (a gain), steam, gain, whiten, steps, parts (1 body, 2 shadow, 4 jets, 8 debris, 16 light, 32 after-smoke), light, at (a still at that age, s), on";
}
