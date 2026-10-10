namespace Fleetwright;

public static unsafe class Program
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
            shipPath ??= Path.Combine(AppContext.BaseDirectory, DefaultDesign);
            if (!File.Exists(shipPath))
            {
                Console.Error.WriteLine($"Design not found: {shipPath}");
                return 1;
            }
            engine.Scene = new ShipViewer(engine.Device, shipPath, navy, era);

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

    /// <summary>The design shown without -ship=: a game asset (GameAsset in the csproj), next to the exe.</summary>
    const string DefaultDesign = "Content/Designs/bismarck.json";
}
