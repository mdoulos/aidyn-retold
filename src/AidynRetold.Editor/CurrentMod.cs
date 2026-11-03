using AidynRetold.Plugins;
using Godot;

namespace AidynRetold.Editor;

/// <summary>
/// The mod the editor saves to, which the person picks when the editor opens (<see cref="ModSetup"/>) or at their first save, and
/// which every save after goes to without asking, a toast saying so (<see cref="PluginFiles.Write"/>). It's remembered for each
/// plugins folder in <c>user://editor.cfg</c>, and forgotten when its folder is gone.
/// </summary>
public static class CurrentMod
{
    private const string Config = "user://editor.cfg", Section = "mods";

    /// <summary>The mod's ID, or null while no mod is picked, as while the person is just looking around.</summary>
    public static string? Id { get; private set; }

    /// <summary>The mod picked changed: the toolbar says so.</summary>
    public static event Action? Changed;

    /// <summary>The mod remembered for <paramref name="pluginsDirectory"/>, if its folder is still there, or null.</summary>
    public static string? Remembered(string pluginsDirectory)
    {
        var config = new ConfigFile();
        if (config.Load(Config) != Error.Ok || config.GetValue(Section, Key(pluginsDirectory), "").AsString() is not { Length: > 0 } id)
            return null;
        return Exists(pluginsDirectory, id) ? id : null;
    }

    /// <summary>Saves go to <paramref name="id"/> from now on, remembered for <paramref name="pluginsDirectory"/> when <paramref name="keep"/>.</summary>
    public static void Use(string pluginsDirectory, string id, bool keep = true)
    {
        Id = id;
        if (keep)
        {
            var config = new ConfigFile();
            config.Load(Config);
            config.SetValue(Section, Key(pluginsDirectory), id);
            config.Save(Config);
        }
        Changed?.Invoke();
    }

    /// <summary>Whether a mod of <paramref name="id"/> is there: in the plugins folder, or a base plugin's own folder.</summary>
    public static bool Exists(string pluginsDirectory, string id) =>
        File.Exists(Path.Combine(pluginsDirectory, id, PluginManifest.FileName))
        || DataPaths.BasePlugins is { } basePlugins && File.Exists(Path.Combine(basePlugins, id, PluginManifest.FileName));

    /// <summary>A config key for a plugins folder: its full path, every character that isn't a letter or a digit an underscore.</summary>
    private static string Key(string pluginsDirectory) =>
        new([.. Path.GetFullPath(pluginsDirectory).TrimEnd('/', '\\').ToLowerInvariant().Select(c => char.IsAsciiLetterOrDigit(c) ? c : '_')]);
}
