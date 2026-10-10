using Fleetwright.Shipgen;

namespace Fleetwright.Designer;

/// <summary>
/// The design being edited: where it came from (the baseline, "since opened"), what it is now, and the history of
/// edits between, with undo and redo. Designs are immutable records, so each history entry is a whole design and an
/// edit is a <c>Design -> Design</c>. Edits by the same knob in a row merge into one entry ("Speed 21 -> 23 kn" rather
/// than four steps). No threads, no GPU.
/// </summary>
public sealed class DesignDoc
{
    /// <summary>A history entry: the design, its JSON (for comparisons and the cache), a label and its merge key.</summary>
    public sealed record Entry(Design Design, string Json, string Label, string? Key);

    readonly List<Entry> history = [];

    /// <summary>The design as opened (Reset goes back to it).</summary>
    public Design Baseline { get; }

    /// <summary>The file it came from, if any.</summary>
    public string? SourcePath { get; private set; }

    /// <summary>The design now.</summary>
    public Design Current => history[Index].Design;

    /// <summary>The current entry in <see cref="History"/>; entries after it are the redo stack.</summary>
    public int Index { get; private set; }

    public IReadOnlyList<Entry> History => history;

    /// <summary>The current design differs from the last one saved or opened.</summary>
    public bool Dirty => history[Index].Json != savedJson;
    string savedJson;

    public DesignDoc(Design design, string? sourcePath = null, string label = "Opened")
    {
        Baseline = design;
        savedJson = design.ToJson();
        SourcePath = sourcePath;
        history.Add(new Entry(design, savedJson, label, null));
    }

    /// <summary>The empty ship: an 8 kn hull and nothing else.</summary>
    public static Design Empty() => new() { Id = "new_design", Name = "New design", SpeedKn = 8 };

    public static DesignDoc New() => new(Empty(), null, "New design");

    public static DesignDoc Open(string path) => new(Design.Load(path), path);

    /// <summary>Makes <paramref name="next"/> the current design. With a <paramref name="key"/> equal to the current
    /// entry's (the same knob again), the entry is replaced instead of a new one pushed. Drops the redo stack.</summary>
    public void Apply(Design next, string label, string? key = null)
    {
        string json = next.ToJson();
        if (json == history[Index].Json)
            return;
        history.RemoveRange(Index + 1, history.Count - Index - 1);
        if (key != null && history[Index].Key == key && Index > 0)
            history[Index] = new Entry(next, json, label, key);
        else
        {
            history.Add(new Entry(next, json, label, key));
            Index++;
        }
    }

    /// <summary>Ends the current merge: the next edit by the same knob starts a new entry.</summary>
    public void Seal()
    {
        if (history[Index].Key != null)
            history[Index] = history[Index] with { Key = null };
    }

    public bool CanUndo => Index > 0;
    public bool CanRedo => Index < history.Count - 1;

    public void Undo()
    {
        if (CanUndo)
            Index--;
        Seal();
    }

    public void Redo()
    {
        if (CanRedo)
            Index++;
        Seal();
    }

    /// <summary>Jumps to a history entry (the later ones stay, as redo).</summary>
    public void Jump(int index)
    {
        Index = Math.Clamp(index, 0, history.Count - 1);
        Seal();
    }

    /// <summary>Back to the design as opened, as a new history entry.</summary>
    public void Reset()
    {
        Apply(Baseline, "Reset to opened");
        Seal();
    }

    /// <summary>The player's designs: My Games/Fleetwright/Designs in Documents (until campaign saves own them).</summary>
    public static string UserFolder =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "My Games", "Fleetwright", "Designs");

    /// <summary>A file name from the design's name: letters, digits and underscores.</summary>
    public static string FileName(Design d)
    {
        var name = new string((d.Name ?? d.Id ?? "design").Select(c => char.IsLetterOrDigit(c) ? char.ToLowerInvariant(c) : '_').ToArray()).Trim('_');
        return (name.Length > 0 ? name : "design") + ".json";
    }

    /// <summary>Writes the current design to <paramref name="path"/> (indented JSON, the engine's own format),
    /// with its id taken from the file name so the folder's ids stay unique.</summary>
    public void Save(string path)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        var d = Current with { Id = Path.GetFileNameWithoutExtension(path) };
        File.WriteAllText(path, d.ToJson(indented: true));
        Apply(d, "Saved");
        savedJson = history[Index].Json;
        SourcePath = path;
    }
}
