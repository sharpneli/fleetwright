namespace Fleetwright;

public static class Program
{
    public static int Main(string[] args)
    {
        // Parse command line arguments
        string? modelPath = null;
        int screenshotFrame = -1;
        string screenshotPath = "screenshot.png";
        string? shipPath = null, navy = null, era = null;

        foreach (string arg in args)
        {
            if (arg.StartsWith("-model="))
            {
                modelPath = arg.Substring("-model=".Length);
            }
            else if (arg.StartsWith("-screenshot="))
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
            else if (!arg.StartsWith("-") && (arg.EndsWith(".gltf") || arg.EndsWith(".glb")))
            {
                modelPath = arg;
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

            // The ship viewer: build, bake and show a Shipgen design instead of the 3D scene
            if (!string.IsNullOrEmpty(shipPath))
            {
                engine.Viewer = new ShipViewer(engine, shipPath, navy, era);
            }

            // Load model if specified
            if (!string.IsNullOrEmpty(modelPath))
            {
                var scene = GltfLoader.Load(engine, modelPath);
                if (scene != null)
                {
                    engine.SceneRenderables.Add(scene);
                }
            }

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
}
