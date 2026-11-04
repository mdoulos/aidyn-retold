using AidynRetold.Plugins;
using AidynRetold.Plugins.World;
using Godot;

namespace AidynRetold.Editor;

/// <summary>
/// Which mod the editor saves to (<see cref="CurrentMod"/>), asked when it opens with none known, as the import dialog asks for the
/// ROM, before anything loads, and from the toolbar to change it: <b>Load Existing Mod</b> picks a mod's <c>plugin.json</c> in the
/// plugins folder, <b>Create New Mod</b> names a new one and makes its folder. The first asking has <b>Just looking around</b>,
/// which goes on with none, so the first save asks (<see cref="PluginFiles.Write"/>); the toolbar's closes by its cross, keeping
/// the mod there was.
/// </summary>
public static class ModSetup
{
    private const float Width = 520;

    /// <summary>
    /// Asks, and completes with the mod picked or made, which it uses from then on, or null for none. A <paramref name="blocking"/>
    /// dialog, the editor's first, isn't closed by Escape or the backdrop; the toolbar's is, and keeps the mod there was.
    /// </summary>
    public static Task<string?> AskAsync(Node near, string pluginsDirectory, bool blocking)
    {
        var done = new TaskCompletionSource<string?>();
        Choose(near.GetTree().Root, pluginsDirectory, blocking, done);
        return done.Task;
    }

    private static void Choose(Node root, string pluginsDirectory, bool blocking, TaskCompletionSource<string?> done)
    {
        EditorDialog? dialog = null;
        // The first asking goes on without a mod by Just looking around; the toolbar's by its cross, keeping the mod there was.
        Button? later = null;
        if (blocking)
        {
            later = new Button { Text = "Just looking around", ThemeTypeVariation = "FlatButton" };
            later.AddThemeColorOverride("font_color", EditorTheme.Current.Accent);
            later.Pressed += () =>
            {
                dialog?.Close();
                done.TrySetResult(null);
            };
        }
        dialog = new EditorDialog("Choose a mod to save to",
            EditorDialog.Paragraphs("Which mod would you like to save your changes to?\n\nEverything you change in the editor is saved to it, and each " +
                "save can be undone. You can change it from the toolbar.", Width - 28),
            [
                new DialogButton("Load Existing Mod", DialogButtonKind.Secondary, () => Load(root, pluginsDirectory, blocking, done, dialog!), Closes: false),
                new DialogButton("Create New Mod", DialogButtonKind.Primary, () =>
                {
                    dialog!.Close();
                    Create(root, pluginsDirectory, blocking, done);
                }, Closes: false),
            ], Width, dismissable: !blocking, under: later);
        dialog.Dismissed += () => done.TrySetResult(CurrentMod.Id);
        dialog.Open(root);
    }

    /// <summary>A mod's manifest to pick, in the plugins folder, which must be a mod's, loaded with the others.</summary>
    private static void Load(Node root, string pluginsDirectory, bool blocking, TaskCompletionSource<string?> done, EditorDialog choice)
    {
        var files = new FileDialog
        {
            FileMode = FileDialog.FileModeEnum.OpenFile, Access = FileDialog.AccessEnum.Filesystem, UseNativeDialog = true,
            Title = "A mod's plugin.json, in its folder in the plugins folder", Filters = ["plugin.json ; A mod's manifest"], CurrentDir = pluginsDirectory,
        };
        files.FileSelected += path =>
        {
            if (Check(pluginsDirectory, path, out var id) is { } wrong)
            {
                EditorDialog.Tell(root, "That isn't a mod the editor can save to", wrong);
                return;
            }
            CurrentMod.Use(pluginsDirectory, id);
            choice.Close();
            done.TrySetResult(id);
        };
        files.VisibilityChanged += () =>
        {
            if (!files.Visible)
                files.QueueFree();
        };
        root.AddChild(files);
        files.PopupCentered(new Vector2I(800, 500));
    }

    /// <summary>What's wrong with a picked manifest as the mod to save to, or null, with its ID.</summary>
    private static string? Check(string pluginsDirectory, string manifest, out string id)
    {
        id = "";
        PluginManifest? read;
        try
        {
            read = PluginJson.ReadFile<PluginManifest>(manifest);
        }
        catch (Exception e) when (e is IOException or InvalidDataException or System.Text.Json.JsonException)
        {
            return $"It can't be read: {e.Message}";
        }
        if (read is null)
            return "It's empty.";
        id = read.Id;
        var folder = Path.GetDirectoryName(Path.GetFullPath(manifest))!;
        var parent = Path.GetDirectoryName(folder) ?? "";
        bool Same(string? a, string? b) => a is not null && b is not null
            && string.Equals(Path.GetFullPath(a).TrimEnd('/', '\\'), Path.GetFullPath(b).TrimEnd('/', '\\'), StringComparison.OrdinalIgnoreCase);
        if (read.Import is not null || id == RecordId.RomNamespace)
            return "It's the original game's, which the editor never changes. Pick a mod, or create one.";
        // The ACRE plugin's folder is the one ACRE_PLUGIN names, whatever it's called.
        if (Same(folder, DataPaths.AcrePlugin))
            return null;
        if (!Same(parent, pluginsDirectory))
            return $"It's outside the plugins folder, so the editor wouldn't load what's saved to it. Move its folder into {pluginsDirectory}, and pick it there.";
        if (!string.Equals(Path.GetFileName(folder), id, StringComparison.Ordinal))
            return $"Its folder is {Path.GetFileName(folder)}, but it says its ID is {id}: a mod's folder is named for its ID.";
        return null;
    }

    /// <summary>A name for the new mod, whose ID is made from it, and its folder made in the plugins folder.</summary>
    private static void Create(Node root, string pluginsDirectory, bool blocking, TaskCompletionSource<string?> done)
    {
        var name = new LineEdit { PlaceholderText = "My Village", CustomMinimumSize = new Vector2(Width - 28, 0) };
        var saved = EditorStyle.DimLabel(Saved(""), 13);
        var problem = new Label { ThemeTypeVariation = "ProblemLabel", AutowrapMode = TextServer.AutowrapMode.WordSmart, CustomMinimumSize = new Vector2(Width - 28, 0), Visible = false };
        name.TextChanged += text =>
        {
            saved.Text = Saved(text);
            problem.Visible = false;
        };
        EditorDialog? dialog = null;
        void Make()
        {
            var id = Slug(name.Text);
            if (id.Length == 0)
                Say("Give the mod a name.");
            else if (id == AcrePlugin.Id)
                Say($"{id} is the ACRE plugin's ID: give the mod another name.");
            else if (id == RecordId.RomNamespace || CurrentMod.Exists(pluginsDirectory, id))
                Say($"There's a mod called {id} already: load it instead, or give this one another name.");
            else
            {
                try
                {
                    ModFolder.OpenOrCreate(pluginsDirectory, id, name: name.Text.Trim(), acrePlugin: DataPaths.AcrePlugin);
                }
                catch (Exception e) when (e is IOException or InvalidDataException or UnauthorizedAccessException)
                {
                    Say(e.Message);
                    return;
                }
                CurrentMod.Use(pluginsDirectory, id);
                dialog!.Close();
                done.TrySetResult(id);
            }
        }
        void Say(string text)
        {
            problem.Text = text;
            problem.Visible = true;
        }
        name.TextSubmitted += _ => Make();
        dialog = new EditorDialog("Create a new mod", [.. EditorDialog.Paragraphs("Name your new mod.", Width - 28), name, saved, problem],
            [
                new DialogButton("Back", DialogButtonKind.Cancel, () => Choose(root, pluginsDirectory, blocking, done)),
                new DialogButton("Create", DialogButtonKind.Primary, Make, Closes: false),
            ], Width, dismissable: !blocking) { FocusOnOpen = name };
        dialog.Open(root);
    }

    private static string Saved(string name) => Slug(name) is { Length: > 0 } id ? $"Saved in the plugins folder as {id}." : "Saved in the plugins folder, named for it.";

    /// <summary>A mod's ID made from its name: lower-case letters and digits, each run of anything else an underscore.</summary>
    public static string Slug(string name)
    {
        var chars = new System.Text.StringBuilder();
        foreach (var c in name.Trim().ToLowerInvariant())
        {
            if (char.IsAsciiLetterOrDigit(c))
                chars.Append(c);
            else if (chars.Length > 0 && chars[^1] != '_')
                chars.Append('_');
        }
        return chars.ToString().Trim('_');
    }
}
