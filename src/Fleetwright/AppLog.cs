using System.Reflection;
using System.Text;

namespace Fleetwright;

/// <summary>The game's log and crash reports (the alpha's diagnostics): everything written to the console also goes to a
/// file in Documents/My Games/Fleetwright/Logs, flushed line by line, so a Release build (no console) and a native crash
/// (no managed handler runs) still leave it behind. An exception nothing caught, the engine's error and the sim thread's
/// stop each write a crash report beside it, naming the log and the battle's replay
/// (<see cref="Sim.Missions.ReplayWriter"/>); in Release a message box says where they are.</summary>
public static unsafe class AppLog
{
    /// <summary>The newest logs and crash reports kept; older ones are deleted at the start.</summary>
    const int Keep = 20;

    public static string Folder { get; } =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "My Games", "Fleetwright", "Logs");

    /// <summary>This run's log file, or null if it couldn't be opened (the game runs on without one).</summary>
    public static string? LogPath { get; private set; }

    /// <summary>The build's version and commit ("1.0.0+df91dc66").</summary>
    public static string Version { get; } =
        typeof(AppLog).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "unknown";

    /// <summary>What the crash report adds about the game's state when it's written (the scenario, the world's time, the
    /// replay): the app sets it once the world exists. Called from whatever thread crashed, so it must not throw.</summary>
    public static Func<string>? State { get; set; }

    static StreamWriter? file;
    static int crashed;

    /// <summary>Opens the log and tees the console into it; hooks the unhandled-exception handlers. First thing in Main.</summary>
    public static void Start(string[] args)
    {
        try
        {
            Directory.CreateDirectory(Folder);
            Prune("fleetwright-*.log");
            Prune("crash-*.txt");
            LogPath = Path.Combine(Folder, $"fleetwright-{DateTime.Now:yyyyMMdd-HHmmss}-{Environment.ProcessId}.log");
            file = new StreamWriter(new FileStream(LogPath, FileMode.Create, FileAccess.Write, FileShare.Read), new UTF8Encoding(false)) { AutoFlush = true };
            var log = TextWriter.Synchronized(file);
            Console.SetOut(TextWriter.Synchronized(new Tee(Console.Out, log)));
            Console.SetError(TextWriter.Synchronized(new Tee(Console.Error, log)));
        }
        catch (Exception x) when (x is IOException or UnauthorizedAccessException)
        {
            LogPath = null;
            Console.Error.WriteLine($"log: couldn't open one in {Folder}: {x.Message}");
        }
        Console.WriteLine($"Fleetwright {Version}, {DateTime.Now:yyyy-MM-dd HH:mm:ss}; {Environment.OSVersion}, .NET {Environment.Version}, " +
                          $"{Environment.ProcessorCount} threads; args: {(args.Length == 0 ? "none" : string.Join(' ', args))}");
        AppDomain.CurrentDomain.UnhandledException += (_, e) => Crash(e.ExceptionObject as Exception, "an exception nothing caught" +
            (e.IsTerminating ? "" : " (not terminating)"), fatal: e.IsTerminating);
        TaskScheduler.UnobservedTaskException += (_, e) =>
            Console.Error.WriteLine($"log: a task's exception nobody looked at: {e.Exception.GetBaseException()}");
    }

    /// <summary>Writes a crash report (once a run): what failed, the build, the game's state, the log and replay; in
    /// Release a message box tells the player where. <paramref name="fatal"/>: the game is going down (else it runs on, as
    /// after the sim thread's stop). Never throws.</summary>
    public static void Crash(Exception? x, string what, bool fatal = true, string? detail = null)
    {
        if (Interlocked.Exchange(ref crashed, 1) != 0)
        {
            Console.Error.WriteLine($"log: and {what}: {x?.ToString() ?? detail}");
            return;
        }
        try
        {
            string state;
            try
            {
                state = State?.Invoke() ?? "";
            }
            catch (Exception sx)
            {
                state = $"(the state couldn't be read: {sx.Message})";
            }
            var text = new StringBuilder()
                .AppendLine($"Fleetwright {Version} stopped: {what}, {DateTime.Now:yyyy-MM-dd HH:mm:ss}")
                .AppendLine($"{Environment.OSVersion}, .NET {Environment.Version}, {Environment.ProcessorCount} threads")
                .AppendLine($"log: {LogPath ?? "none"}")
                .AppendLine(state)
                .AppendLine()
                .AppendLine(x?.ToString() ?? detail ?? "(no exception)")
                .ToString();
            Console.Error.WriteLine($"log: crash: {what}");
            Console.Error.WriteLine(x?.ToString() ?? detail);
            Directory.CreateDirectory(Folder);
            string report = Path.Combine(Folder, $"crash-{DateTime.Now:yyyyMMdd-HHmmss}-{Environment.ProcessId}.txt");
            File.WriteAllText(report, text);
        }
        catch (Exception)
        {
            // nothing more can be done: the console has what it has
        }
#if !TRACY
        // Release (the players' build: no console) tells them; Debug and DevRelease have the console, and a capture run mustn't wait on a box
        try
        {
            string body = (fatal ? "Fleetwright has stopped" : "The battle has stopped") + $" ({what}).\n\n" +
                          $"A report, the log and the battle's replay are in\n{Folder}\nand {Sim.Missions.ReplayWriter.Folder}\n\n" +
                          "Please send them with a word on what you were doing.";
            SDL.SDL3.SDL_ShowSimpleMessageBox(SDL.SDL_MessageBoxFlags.SDL_MESSAGEBOX_ERROR, "Fleetwright", body, null);
        }
        catch (Exception)
        {
        }
#endif
    }

    static void Prune(string pattern)
    {
        try
        {
            foreach (var old in new DirectoryInfo(Folder).GetFiles(pattern).OrderByDescending(f => f.LastWriteTimeUtc).Skip(Keep - 1))
                old.Delete();
        }
        catch (Exception x) when (x is IOException or UnauthorizedAccessException)
        {
        }
    }

    /// <summary>Writes to two writers: the console as it was, and the log (each guarded, so a closed console never loses
    /// the log). No closures: a write allocates nothing of its own.</summary>
    sealed class Tee(TextWriter a, TextWriter b) : TextWriter
    {
        public override Encoding Encoding => a.Encoding;

        public override void Write(char value)
        {
            try { a.Write(value); } catch (Exception x) when (x is IOException or ObjectDisposedException) { }
            try { b.Write(value); } catch (Exception x) when (x is IOException or ObjectDisposedException) { }
        }

        public override void Write(string? value)
        {
            try { a.Write(value); } catch (Exception x) when (x is IOException or ObjectDisposedException) { }
            try { b.Write(value); } catch (Exception x) when (x is IOException or ObjectDisposedException) { }
        }

        public override void Write(char[] buffer, int index, int count)
        {
            try { a.Write(buffer, index, count); } catch (Exception x) when (x is IOException or ObjectDisposedException) { }
            try { b.Write(buffer, index, count); } catch (Exception x) when (x is IOException or ObjectDisposedException) { }
        }

        public override void WriteLine(string? value)
        {
            try { a.WriteLine(value); } catch (Exception x) when (x is IOException or ObjectDisposedException) { }
            try { b.WriteLine(value); } catch (Exception x) when (x is IOException or ObjectDisposedException) { }
        }

        public override void Flush()
        {
            try { a.Flush(); } catch (Exception x) when (x is IOException or ObjectDisposedException) { }
            try { b.Flush(); } catch (Exception x) when (x is IOException or ObjectDisposedException) { }
        }
    }
}
