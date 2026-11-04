using AidynRetold.Plugins;
using AidynRetold.Plugins.Assets;
using AidynRetold.Plugins.World;
using AidynRetold.Views;
using Godot;

namespace AidynRetold.Editor;

/// <summary>
/// What a model's inspector sections are given: the plugins, the view, the model and its actor, and the browser's hands. An armor's
/// page gives its shield's model with no view, its file parsed (<see cref="Glb"/>), for the sections that need only the file: the
/// model's buttons and its textures.
/// </summary>
public sealed class ModelContext
{
    public required PluginCatalog Catalog { get; init; }

    /// <summary>The model view, on a model's page; none on an armor's, whose sections read the file alone.</summary>
    public ModelView? View { get; init; }

    /// <summary>The model's glTF as parsed: the view's, or one of its own where there's no view.</summary>
    public GlbScene? Glb
    {
        get => _glb ?? View?.File;
        init => _glb = value;
    }

    private readonly GlbScene? _glb;

    /// <summary>The view, for the sections only a model's page has: parts, what's held, the body.</summary>
    public ModelView ViewOrThrow => View ?? throw new InvalidOperationException("This section needs the model view.");

    /// <summary>The model: an actor's scene, or a scene of its own.</summary>
    public required RecordId Scene { get; init; }

    /// <summary>The actor, when the model is one's.</summary>
    public RecordId? ActorId { get; init; }

    /// <summary>Opens a record's page.</summary>
    public required Action<RecordId> Open { get; init; }

    /// <summary>Loads the plugins again after a write, and opens this record's page again.</summary>
    public required Func<Task> Reload { get; init; }

    /// <summary>Which models use which textures, built once for the plugins, off the main thread.</summary>
    public required Func<Task<TextureUsage>> Usage { get; init; }

    /// <summary>The loaded models, for what's held.</summary>
    public required Models? Models { get; init; }

    /// <summary>Whether the view shows the original model and actor rather than the plugins' replacement.</summary>
    public bool Original { get; init; }

    /// <summary>Shows the original or the replacement, when the model is replaced.</summary>
    public required Action<bool> ShowOriginal { get; init; }

    public PluginSet Plugins => Catalog.Plugins;

    /// <summary>The model's file in effect, and the original's when a later plugin replaces it.</summary>
    public string? File => Plugins.FindAsset(Scene, "glb");

    public string? OriginalFile => Plugins.FindOriginalAsset(Scene, "glb");

    /// <summary>Whether a plugin after the one that first gave the model replaces it.</summary>
    public bool Replaced => File is { } f && f != OriginalFile;
}

/// <summary>
/// The editor's ways to pick a file or folder, and to save into a mod: the person names the mod, which is made in the plugins folder
/// if it isn't there and remembered for the next save (<see cref="ModFolder.OpenOrCreate"/>, which saves the ACRE plugin's ID into its
/// own folder).
/// </summary>
public static class PluginFiles
{
    /// <summary>The folder the plugins are installed in, which holds the ROM plugin and the mods.</summary>
    public static string PluginsDirectory(PluginSet plugins) => System.IO.Path.GetDirectoryName(plugins.Plugins[0].Directory.TrimEnd('/', '\\'))!;

    /// <summary>A file to open, in the native dialog.</summary>
    public static void Open(Node parent, string title, string filter, Action<string> picked) =>
        Show(parent, new FileDialog { FileMode = FileDialog.FileModeEnum.OpenFile, Title = title, Filters = [filter] }, picked);

    /// <summary>A file to save, in the native dialog, named <paramref name="name"/> to start with.</summary>
    public static void Save(Node parent, string title, string name, string filter, Action<string> picked) =>
        Show(parent, new FileDialog { FileMode = FileDialog.FileModeEnum.SaveFile, Title = title, CurrentFile = name, Filters = [filter] }, picked);

    /// <summary>A folder, in the native dialog.</summary>
    public static void Folder(Node parent, string title, Action<string> picked) =>
        Show(parent, new FileDialog { FileMode = FileDialog.FileModeEnum.OpenDir, Title = title }, picked);

    private static void Show(Node parent, FileDialog dialog, Action<string> picked)
    {
        dialog.Access = FileDialog.AccessEnum.Filesystem;
        dialog.UseNativeDialog = true;
        dialog.FileSelected += path => picked(path);
        dialog.DirSelected += path => picked(path);
        dialog.VisibilityChanged += () =>
        {
            if (!dialog.Visible)
                dialog.QueueFree();
        };
        parent.AddChild(dialog);
        dialog.PopupCentered(new Vector2I(800, 500));
    }

    /// <summary>
    /// Saves <paramref name="what"/> with <paramref name="write"/> to the mod the editor saves to, and then runs
    /// <paramref name="after"/>, the plugins' reload. What goes wrong is said in a dialog. The ACRE plugin's ID saves into the ACRE
    /// plugin's own folder, as the owner's own changes are kept (<see cref="ModFolder.OpenOrCreate"/>).
    /// </summary>
    public static void Write(Node parent, PluginSet plugins, string what, Action<ModFolder> write, Func<Task> after) =>
        _ = BackgroundTask.Report(WriteAsync(parent, plugins, what, write, after));

    private static async Task WriteAsync(Node parent, PluginSet plugins, string what, Action<ModFolder> write, Func<Task> after)
    {
        if (await ModAsync(parent, plugins) is { } mod)
            Save(parent, plugins, mod, what, "was saved to", write, after);
    }

    /// <summary>
    /// The mod to save to: the editor's, or with none yet, as when the person was just looking around, the one the mod dialog loads or
    /// makes (<see cref="ModSetup"/>), which is the one from then on; null if it's closed without one.
    /// </summary>
    public static async Task<string?> ModAsync(Node parent, PluginSet plugins) =>
        CurrentMod.Id ?? await ModSetup.AskAsync(parent, PluginsDirectory(plugins), blocking: false);

    /// <summary>
    /// Writes into <paramref name="mod"/>, noting what it overwrote, loads the plugins again, and shows a toast of what was done with
    /// Undo, which puts the files back as they were and loads the plugins again. False, with a dialog saying why, if it couldn't.
    /// </summary>
    private static bool Save(Node parent, PluginSet plugins, string mod, string what, string done, Action<ModFolder> write, Func<Task> after)
    {
        var root = parent.GetTree().Root;
        ModFolder? folder = null;
        try
        {
            folder = ModFolder.OpenOrCreate(PluginsDirectory(plugins), mod, acrePlugin: DataPaths.AcrePlugin);
            folder.Journal = [];
            write(folder);
            var journal = folder.Journal;
            _ = BackgroundTask.Report(after());
            Toast.Show(root, $"{char.ToUpperInvariant(what[0])}{what[1..]} {done} {EditorStyle.ModName(mod)}.", () =>
            {
                try
                {
                    ModFolder.Restore(journal);
                    _ = BackgroundTask.Report(after());
                    Toast.Show(root, "Undone.", seconds: 1.5);
                }
                catch (Exception e) when (e is IOException or UnauthorizedAccessException)
                {
                    Tell(root, "Couldn't undo it", e.Message);
                }
            });
            return true;
        }
        catch (Exception e) when (e is IOException or InvalidDataException or UnauthorizedAccessException or InvalidOperationException
            or ArgumentException or System.Text.Json.JsonException)
        {
            // What was written before it went wrong, such as a picture before its record, is put back, so nothing's left half saved.
            var undone = "";
            if (folder?.Journal is { Count: > 0 } written)
            {
                try
                {
                    ModFolder.Restore(written);
                }
                catch (Exception again) when (again is IOException or UnauthorizedAccessException)
                {
                    undone = $"\n\nWhat was written before it couldn't be put back either: {again.Message}";
                }
            }
            Tell(root, "Couldn't save it", e.Message + undone);
            return false;
        }
    }

    /// <summary>Takes <paramref name="id"/>'s asset out of the mod <paramref name="mod"/>, after asking, so the one below it shows again.</summary>
    public static void Remove(Node parent, PluginSet plugins, string mod, RecordId id, string extension, string what, Func<Task> after) =>
        EditorDialog.Confirm(parent, "Take it out of the mod", $"Take {what} out of {EditorStyle.ModName(mod)}?\n\nWhat it replaced shows again: another mod's, or the original's.",
            "Take it out", () => Save(parent, plugins, mod, what, "was taken out of", folder => folder.RemoveAsset(id, extension), after), danger: true);

    /// <summary>A message in a dialog, with OK.</summary>
    public static void Tell(Node parent, string title, string text) => EditorDialog.Tell(parent, title, text);
}
