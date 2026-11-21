using System.Text.Json;

namespace Fleetwright;

/// <summary>The player's options, kept across sessions in Documents/My Games/Fleetwright/options.json: today the UI's size
/// (<see cref="UiFonts.UserScale"/>, the app menu's slider), over the scale the window's height gives. A capture run
/// (-screenshot) neither reads nor writes them, as it reads no imgui.ini: it shows the defaults. A missing or broken file
/// is the defaults.</summary>
public static class AppOptions
{
    /// <summary>The UI size's range: a share of the window-height scale.</summary>
    public const float MinUiScale = 0.5f, MaxUiScale = 2f;

    static string Path => System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "My Games", "Fleetwright", "options.json");

    static bool capture;

    /// <summary>Reads the options into the app (none in a capture run). Before the engine starts.</summary>
    public static void Start(bool captureRun)
    {
        capture = captureRun;
        if (capture)
            return;
        try
        {
            if (!File.Exists(Path))
                return;
            using var doc = JsonDocument.Parse(File.ReadAllText(Path));
            if (doc.RootElement.TryGetProperty("ui_scale", out var s) && s.TryGetSingle(out float f) && float.IsFinite(f))
            {
                UiFonts.UserScale = Math.Clamp(f, MinUiScale, MaxUiScale);
                Console.WriteLine($"options: the UI's size {UiFonts.UserScale * 100:0}% ({Path})");
            }
        }
        catch (Exception x) when (x is IOException or JsonException or UnauthorizedAccessException)
        {
            Console.Error.WriteLine($"options: {Path}: {x.Message} (the defaults)");
        }
    }

    /// <summary>The UI's size, set now (the fonts reload once the frame's done) and kept.</summary>
    public static void SetUiScale(float scale)
    {
        UiFonts.UserScale = Math.Clamp(scale, MinUiScale, MaxUiScale);
        Save();
    }

    static void Save()
    {
        if (capture)
            return;
        try
        {
            Directory.CreateDirectory(System.IO.Path.GetDirectoryName(Path)!);
            File.WriteAllText(Path, JsonSerializer.Serialize(new Dictionary<string, float> { ["ui_scale"] = UiFonts.UserScale }));
        }
        catch (Exception x) when (x is IOException or UnauthorizedAccessException)
        {
            Console.Error.WriteLine($"options: not saved ({x.Message})");
        }
    }
}
