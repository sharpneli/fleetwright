namespace Fleetwright;

/// <summary>A request to save the window as seen, UI included, to a PNG: how a scene exports what it shows (the ship
/// card's "Save as PNG") without knowing the engine. The scene asks; the engine looks once a frame and, when one is
/// due, captures that frame (cropped to the given rectangle, window pixels) and writes the file. A delay of a frame or
/// two lets a menu that asked close first.</summary>
public static class FrameCapture
{
    public sealed record Request(string Path, int X, int Y, int Width, int Height);

    static Request? pending;
    static int delay;

    /// <summary>The newest result: the path written, or why not (null before the first).</summary>
    public static string? LastResult { get; private set; }

    /// <summary>Asks for the frame <paramref name="frames"/> frames from now (a newer request replaces an older one).</summary>
    public static void Ask(Request r, int frames = 2)
    {
        pending = r;
        delay = frames;
        LastResult = null;
    }

    /// <summary>The engine, once a frame: the request due now, if any (taking it).</summary>
    public static Request? Due()
    {
        if (pending == null || delay-- > 0)
            return null;
        var r = pending;
        pending = null;
        return r;
    }

    /// <summary>The engine, after writing (or failing to).</summary>
    public static void Done(string result) => LastResult = result;
}
