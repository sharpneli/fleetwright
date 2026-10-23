using Fleetwright.Map;
using System.Numerics;

namespace Fleetwright.Tests;

/// <summary>The map's label placer (docs/map-ui/symbols.md Y5): a close formation drifting across the screen by parts of
/// a pixel a frame keeps its labels where they are; none jumps side or blinks out and back (one that didn't fit may
/// settle in once, when room opens clear).</summary>
public class MapLabelsTests
{
    const float W = 1920, H = 1080, Cell = 6;

    /// <summary>Ships in a line, <paramref name="dx"/>, <paramref name="dy"/> px apart, drifting by a part of a pixel a
    /// frame with a pixel's wobble between them (station keeping), their squadron's label at the guide: frames on which
    /// a label changed its side or showing, the most for any one label.</summary>
    static int Changes(int ships, float dx, float dy, int frames)
    {
        float fade = SymbolLook.LabelFade;
        SymbolLook.LabelFade = 0;   // shown or not at once: a blink is a change
        try
        {
            var labels = new MapLabels();
            var rng = new Random(7);
            var last = new (LabelPlace, bool)[ships + 1];
            var changes = new int[ships + 1];
            for (int f = 0; f < frames; f++)
            {
                labels.Begin(Vector2.Zero, new Vector2(W, H), Cell);
                float ox = 600 + f * 0.37f, oy = 400 + f * 0.23f;
                for (int k = 0; k < ships; k++)
                    labels.Add(LabelKind.Ship, 100 + k, k, 1, ox + k * dx + (float)(rng.NextDouble() - 0.5),
                        oy + k * dy + (float)(rng.NextDouble() - 0.5), 70, 30, 14, 6,
                        LabelPlace.Left, LabelPlace.Right, LabelPlace.Below, LabelPlace.Above);
                labels.Add(LabelKind.Group, 1, ships, 3, ox, oy, 90, 14, 12, 0,
                    LabelPlace.Right, LabelPlace.Below, LabelPlace.Left, LabelPlace.Above);
                labels.Place(1 / 60f);
                for (int k = 0; k <= ships; k++)
                {
                    var (_, place, alpha) = labels.Result(k);
                    var now = (place, alpha > 0);
                    changes[k] += f > 0 && now != last[k] ? 1 : 0;
                    last[k] = now;
                }
            }
            return changes.Max();
        }
        finally
        {
            SymbolLook.LabelFade = fade;
        }
    }

    [Theory]
    [InlineData(6, 0, 24)]     // abreast, closer than a label's height
    [InlineData(6, 0, 34)]     // abreast, a few px between labels
    [InlineData(8, 30, 12)]    // echelon
    [InlineData(8, 40, 20)]
    [InlineData(8, 52, 3)]     // astern, the names' width apart
    public void A_drifting_formation_keeps_its_labels(int ships, float dx, float dy)
    {
        int changes = Changes(ships, dx, dy, 600);
        Assert.True(changes <= 1, $"a label moved or blinked {changes} times in 600 frames");
    }
}
