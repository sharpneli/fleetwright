namespace Fleetwright;

public static class Program
{
    public static int Main(string[] args)
    {
        // Parse command line arguments
        string? modelPath = null;
        int screenshotFrame = -1;
        string screenshotPath = "screenshot.png";

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
