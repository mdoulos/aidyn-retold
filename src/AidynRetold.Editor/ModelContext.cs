using AidynRetold.Plugins;
using AidynRetold.Plugins.Assets;
using AidynRetold.Plugins.World;
using AidynRetold.Views;
using Godot;

namespace AidynRetold.Editor;

/// <summary>What a model's inspector sections are given: the plugins, the view, the model and its actor, and the browser's hands.</summary>
public sealed class ModelContext
{
    public required PluginCatalog Catalog { get; init; }
    public required ModelView View { get; init; }

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
/// The editor's ways to pick a file or folder, and to write into a plugin: the person names the plugin, a mod in the plugins
/// folder or a base plugin in its own (<see cref="ModFolder.OpenOrCreate"/>), which is remembered for the next write.
/// </summary>
public static class PluginFiles
{
    private static string _lastPlugin = "";

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
    /// Asks which plugin to write <paramref name="what"/> into, writes it with <paramref name="write"/>, and then runs
    /// <paramref name="after"/>, the plugins' reload. What goes wrong is said in a dialog.
    /// </summary>
    public static void Write(Node parent, PluginSet plugins, string what, Action<ModFolder> write, Func<Task> after)
    {
        var dialog = new ConfirmationDialog { Title = "Write into a plugin", OkButtonText = "Write" };
        var box = new VBoxContainer { CustomMinimumSize = new Vector2(480, 0) };
        box.AddChild(new Label
        {
            Text = $"Which plugin to write {what} into: its ID, lower-case letters, digits and underscores. A mod that isn't there is made " +
                "in the plugins folder; a base plugin's ID writes into its own folder.",
            AutowrapMode = TextServer.AutowrapMode.WordSmart, CustomMinimumSize = new Vector2(480, 0),
        });
        var id = new LineEdit { Text = _lastPlugin, PlaceholderText = "my_mod" };
        box.AddChild(id);
        dialog.AddChild(box);
        dialog.Confirmed += () =>
        {
            try
            {
                _lastPlugin = id.Text.Trim();
                write(ModFolder.OpenOrCreate(PluginsDirectory(plugins), _lastPlugin, basePluginsDirectory: DataPaths.BasePlugins));
                _ = BackgroundTask.Report(after());
            }
            catch (Exception e) when (e is IOException or InvalidDataException or UnauthorizedAccessException)
            {
                Tell(parent, "Couldn't write it", e.Message);
            }
        };
        dialog.Canceled += dialog.QueueFree;
        parent.AddChild(dialog);
        dialog.PopupCentered();
        id.GrabFocus();
    }

    /// <summary>Takes <paramref name="id"/>'s asset out of the plugin <paramref name="plugin"/>, after asking, so the one below it shows again.</summary>
    public static void Remove(Node parent, PluginSet plugins, string plugin, RecordId id, string extension, string what, Func<Task> after)
    {
        var dialog = new ConfirmationDialog { Title = "Take it out", OkButtonText = "Take it out", DialogText = $"Take {what} out of {plugin}? The one below it shows again." };
        dialog.Confirmed += () =>
        {
            try
            {
                ModFolder.OpenOrCreate(PluginsDirectory(plugins), plugin, basePluginsDirectory: DataPaths.BasePlugins).RemoveAsset(id, extension);
                _ = BackgroundTask.Report(after());
            }
            catch (Exception e) when (e is IOException or InvalidDataException or UnauthorizedAccessException)
            {
                Tell(parent, "Couldn't take it out", e.Message);
            }
        };
        dialog.Canceled += dialog.QueueFree;
        parent.AddChild(dialog);
        dialog.PopupCentered();
    }

    /// <summary>A message in a dialog, with OK.</summary>
    public static void Tell(Node parent, string title, string text)
    {
        var dialog = new AcceptDialog { Title = title, DialogText = text, DialogAutowrap = true, MinSize = new Vector2I(420, 0) };
        dialog.Confirmed += dialog.QueueFree;
        dialog.Canceled += dialog.QueueFree;
        parent.AddChild(dialog);
        dialog.PopupCentered();
    }
}
