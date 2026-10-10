namespace Fleetwright;

public static class Program
{
    public static int Main(string[] args)
    {
        // Parse command line arguments
        int screenshotFrame = -1;
        string screenshotPath = "screenshot.png";
        string? shipPath = null, navy = null, era = null;

        foreach (string arg in args)
        {
            if (arg.StartsWith("-screenshot="))
            {
                string value = arg.Substring("-screenshot=".Length);
                if (int.TryParse(value, out int frame))
                {
                    screenshotFrame = frame;
                }
            }
            else if (arg.StartsWith("-ship="))
            {
                shipPath = arg.Substring("-ship=".Length);
            }
            else if (arg.StartsWith("-navy="))
            {
                navy = arg.Substring("-navy=".Length);
            }
            else if (arg.StartsWith("-era="))
            {
                era = arg.Substring("-era=".Length);
            }
            else if (arg.StartsWith("-output="))
            {
                screenshotPath = arg.Substring("-output=".Length);
            }
        }

        try
        {
            using var engine = new Sdl3GpuEngine();
            engine.Init();

            // Configure screenshot if requested
            if (screenshotFrame >= 0)
            {
                engine.SetScreenshotCapture(screenshotFrame, screenshotPath);
            }

            // The ship viewer: build, bake and show a Shipgen design
            shipPath ??= FindDefaultDesign();
            if (shipPath == null)
            {
                Console.Error.WriteLine($"No design given and {DefaultDesign} not found; pass -ship=path/to/design.json");
                return 1;
            }
            engine.Viewer = new ShipViewer(engine, shipPath, navy, era);

            // Run the engine
            engine.Run();

            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Engine error: {ex.Message}");
            Console.Error.WriteLine(ex.StackTrace);
            return 1;
        }
    }

    const string DefaultDesign = "shipgen/designs/bismarck.json";

    /// <summary>The design shown without -ship=: looked up from the working directory and from the exe's folder
    /// upwards, so it works from the repo root (dotnet run) and from bin/ (the IDE).</summary>
    static string? FindDefaultDesign()
    {
        foreach (var start in new[] { Directory.GetCurrentDirectory(), AppContext.BaseDirectory })
        {
            for (var dir = new DirectoryInfo(start); dir != null; dir = dir.Parent)
            {
                var path = Path.Combine(dir.FullName, DefaultDesign);
                if (File.Exists(path))
                    return path;
            }
        }
        return null;
    }
}
