using System.Text.Json;
using System.Text.Json.Nodes;

namespace AidynRetold.Plugins.World;

/// <summary>
/// A plugin's folder as a tool writes it (ADR 0005), a mod's or a base plugin's: its manifest, a record file of patches and new
/// records that the tool keeps up to date an object at a time, and its assets. The folder is named for the plugin's ID, as
/// <see cref="PluginSet.LoadInstalled"/> requires, and a folder that isn't there yet is made as a mod, with a manifest that depends
/// on the ROM plugin.
/// </summary>
public sealed class ModFolder
{
    private ModFolder(string directory, PluginManifest manifest, bool made)
    {
        Directory = directory;
        Manifest = manifest;
        Made = made;
    }

    public string Directory { get; }
    public PluginManifest Manifest { get; }

    /// <summary>The plugin's ID, which is its records' namespace.</summary>
    public string Id => Manifest.Id;

    /// <summary>Whether <see cref="OpenOrCreate"/> made the folder, so a tool whose write then fails can take it away again.</summary>
    public bool Made { get; }

    /// <summary>
    /// Opens the plugin <paramref name="id"/>: the base plugin of that ID in <paramref name="basePluginsDirectory"/> if there's one
    /// (ADR 0039), so a tool writes into it rather than making a mod of the same ID beside it, else the mod in
    /// <paramref name="pluginsDirectory"/>, made if it isn't there. A plugin ID is <c>[a-z0-9_]+</c>, and can't be the ROM plugin's.
    /// </summary>
    public static ModFolder OpenOrCreate(string pluginsDirectory, string id, string? name = null, string? basePluginsDirectory = null)
    {
        if (id.Length == 0 || !id.All(c => c is >= 'a' and <= 'z' or >= '0' and <= '9' or '_'))
            throw new InvalidDataException($"\"{id}\" isn't a mod ID: use lower-case letters, digits and underscores, such as my_village.");
        if (id == "aidyn" || string.Equals(id, RomPlugin.Folder, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("The ROM plugin isn't a mod to write to: name your own.");
        var directory = basePluginsDirectory is not null && File.Exists(Path.Combine(basePluginsDirectory, id, PluginManifest.FileName))
            ? Path.Combine(basePluginsDirectory, id)
            : Path.Combine(pluginsDirectory, id);
        var manifestPath = Path.Combine(directory, PluginManifest.FileName);
        PluginManifest manifest;
        var made = !System.IO.Directory.Exists(directory);
        if (File.Exists(manifestPath))
        {
            manifest = PluginJson.ReadFile<PluginManifest>(manifestPath) ?? throw new InvalidDataException($"{manifestPath} is empty.");
            if (manifest.Id != id)
                throw new InvalidDataException($"The plugin in {directory} says its ID is \"{manifest.Id}\"; a plugin's folder must be named for its ID.");
        }
        else
        {
            manifest = new PluginManifest(id, name ?? id, "0.1.0", Dependencies: ["aidyn"]);
            System.IO.Directory.CreateDirectory(directory);
            File.WriteAllBytes(manifestPath, JsonSerializer.SerializeToUtf8Bytes(manifest, PluginJson.Options));
        }
        return new ModFolder(directory, manifest, made);
    }

    /// <summary>
    /// <paramref name="plugins"/> without this mod, loaded again in the same order, or <paramref name="plugins"/> itself when
    /// the mod isn't among them: what a patch of the mod's is made against, since it replaces the mod's own.
    /// </summary>
    public PluginSet Without(PluginSet plugins)
    {
        var mine = Path.GetFullPath(Directory).TrimEnd('/', '\\');
        var others = plugins.Plugins.Where(p => !string.Equals(Path.GetFullPath(p.Directory).TrimEnd('/', '\\'), mine, StringComparison.OrdinalIgnoreCase))
            .Select(p => p.Directory).ToList();
        return others.Count == plugins.Plugins.Count ? plugins : PluginSet.Load(others);
    }

    /// <summary>A record ID in the mod's namespace.</summary>
    public RecordId Own(string type, string key) => RecordId.Parse($"{Id}:{type}/{key}");

    /// <summary>
    /// Puts <paramref name="record"/>, a patch (<c>"patch": id</c>) or a new record (<c>"id": id</c>), in the record file
    /// <c>records/<paramref name="file"/></c>, in place of the one there for the same ID, or at the end. The file's other
    /// objects stay as they are.
    /// </summary>
    public void WriteRecord(string file, JsonObject record)
    {
        var path = Path.Combine(Directory, "records", file);
        var target = (record["patch"] ?? record["id"])?.GetValue<string>() ?? throw new ArgumentException("A record names what it patches or its ID.");
        var array = File.Exists(path) ? ReadRecords(path) : [];
        var at = -1;
        for (var i = 0; i < array.Count && at < 0; i++)
        {
            if (array[i] is JsonObject o && ((o["patch"] ?? o["id"])?.GetValue<string>()) == target)
                at = i;
        }
        var copy = (JsonObject)record.DeepClone();
        if (at >= 0)
            array[at] = copy;
        else
            array.Add(copy);
        System.IO.Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        PluginJson.WriteFile(path, JsonSerializer.SerializeToUtf8Bytes(array, PluginJson.Options));
    }

    /// <summary>Takes the record or patch of <paramref name="id"/> out of <c>records/<paramref name="file"/></c>, if it's there.</summary>
    public void RemoveRecord(string file, RecordId id)
    {
        var path = Path.Combine(Directory, "records", file);
        if (!File.Exists(path))
            return;
        var array = ReadRecords(path);
        var text = id.ToString();
        for (var i = array.Count - 1; i >= 0; i--)
        {
            if (array[i] is JsonObject o && ((o["patch"] ?? o["id"])?.GetValue<string>()) == text)
                array.RemoveAt(i);
        }
        if (array.Count == 0)
            File.Delete(path);
        else
            PluginJson.WriteFile(path, JsonSerializer.SerializeToUtf8Bytes(array, PluginJson.Options));
    }

    /// <summary>
    /// A record file's objects: its array, or the one object a hand-written file may hold, as <see cref="PluginSet"/> reads
    /// either, so writing beside it keeps it.
    /// </summary>
    private static JsonArray ReadRecords(string path) =>
        PluginJson.ReadNode(path) switch
        {
            JsonArray array => array,
            JsonObject one => [one],
            _ => throw new InvalidDataException($"{path} holds neither a record nor a list of them."),
        };

    /// <summary>Where the asset of <paramref name="id"/> goes: <c>assets/&lt;namespace&gt;/&lt;type&gt;/&lt;key&gt;.&lt;extension&gt;</c>, as <see cref="PluginSet.FindAsset"/> looks.</summary>
    public string AssetPath(RecordId id, string extension) =>
        Path.Combine([Directory, "assets", id.Namespace, id.Type, .. id.Key.Split('/')]) + "." + extension;

    /// <summary>Writes the asset of <paramref name="id"/>, replacing the mod's own if it has one.</summary>
    public void WriteAsset(RecordId id, string extension, byte[] bytes)
    {
        var path = AssetPath(id, extension);
        System.IO.Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        PluginJson.WriteFile(path, bytes);
    }

    /// <summary>Deletes the mod's asset of <paramref name="id"/>, if it has one, so the one below it shows again.</summary>
    public void RemoveAsset(RecordId id, string extension)
    {
        var path = AssetPath(id, extension);
        if (File.Exists(path))
            File.Delete(path);
    }
}
