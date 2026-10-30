using System.Text.Json;
using System.Text.Json.Nodes;

namespace AidynEnhanced.Plugins.World;

/// <summary>
/// A mod's folder as a tool writes it (ADR 0005): its manifest, a record file of patches and new records that the tool
/// keeps up to date an object at a time, and its assets. The folder is named for the mod's ID, as
/// <see cref="PluginSet.LoadInstalled"/> requires, and a folder that isn't there yet is made with a manifest that depends
/// on the base plugin.
/// </summary>
public sealed class ModFolder
{
    private ModFolder(string directory, PluginManifest manifest)
    {
        Directory = directory;
        Manifest = manifest;
    }

    public string Directory { get; }
    public PluginManifest Manifest { get; }

    /// <summary>The mod's ID, which is its records' namespace.</summary>
    public string Id => Manifest.Id;

    /// <summary>
    /// Opens the mod <paramref name="id"/> in <paramref name="pluginsDirectory"/>, making it if it isn't there: a plugin ID is
    /// <c>[a-z0-9_]+</c>, and can't be the base plugin's.
    /// </summary>
    public static ModFolder OpenOrCreate(string pluginsDirectory, string id, string? name = null)
    {
        if (id.Length == 0 || !id.All(c => c is >= 'a' and <= 'z' or >= '0' and <= '9' or '_'))
            throw new InvalidDataException($"\"{id}\" isn't a mod ID: use lower-case letters, digits and underscores, such as my_village.");
        if (id == "aidyn" || string.Equals(id, BasePlugin.Folder, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("The base plugin isn't a mod to write to: name your own.");
        var directory = Path.Combine(pluginsDirectory, id);
        var manifestPath = Path.Combine(directory, PluginManifest.FileName);
        PluginManifest manifest;
        if (File.Exists(manifestPath))
        {
            manifest = JsonSerializer.Deserialize<PluginManifest>(File.ReadAllBytes(manifestPath), PluginJson.Options)
                ?? throw new InvalidDataException($"{manifestPath} is empty.");
            if (manifest.Id != id)
                throw new InvalidDataException($"The plugin in {directory} says its ID is \"{manifest.Id}\"; a plugin's folder must be named for its ID.");
        }
        else
        {
            manifest = new PluginManifest(id, name ?? id, "0.1.0", Dependencies: ["aidyn"]);
            System.IO.Directory.CreateDirectory(directory);
            File.WriteAllBytes(manifestPath, JsonSerializer.SerializeToUtf8Bytes(manifest, PluginJson.Options));
        }
        return new ModFolder(directory, manifest);
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
        var array = File.Exists(path) ? JsonNode.Parse(File.ReadAllBytes(path), documentOptions: PluginJson.DocumentOptions) as JsonArray ?? [] : [];
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
        File.WriteAllBytes(path, JsonSerializer.SerializeToUtf8Bytes(array, PluginJson.Options));
    }

    /// <summary>Takes the record or patch of <paramref name="id"/> out of <c>records/<paramref name="file"/></c>, if it's there.</summary>
    public void RemoveRecord(string file, RecordId id)
    {
        var path = Path.Combine(Directory, "records", file);
        if (!File.Exists(path) || JsonNode.Parse(File.ReadAllBytes(path), documentOptions: PluginJson.DocumentOptions) is not JsonArray array)
            return;
        var text = id.ToString();
        for (var i = array.Count - 1; i >= 0; i--)
        {
            if (array[i] is JsonObject o && ((o["patch"] ?? o["id"])?.GetValue<string>()) == text)
                array.RemoveAt(i);
        }
        if (array.Count == 0)
            File.Delete(path);
        else
            File.WriteAllBytes(path, JsonSerializer.SerializeToUtf8Bytes(array, PluginJson.Options));
    }

    /// <summary>Where the asset of <paramref name="id"/> goes: <c>assets/&lt;namespace&gt;/&lt;type&gt;/&lt;key&gt;.&lt;extension&gt;</c>, as <see cref="PluginSet.FindAsset"/> looks.</summary>
    public string AssetPath(RecordId id, string extension) =>
        Path.Combine([Directory, "assets", id.Namespace, id.Type, .. id.Key.Split('/')]) + "." + extension;

    /// <summary>Writes the asset of <paramref name="id"/>, replacing the mod's own if it has one.</summary>
    public void WriteAsset(RecordId id, string extension, byte[] bytes)
    {
        var path = AssetPath(id, extension);
        System.IO.Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllBytes(path, bytes);
    }

    /// <summary>Deletes the mod's asset of <paramref name="id"/>, if it has one, so the one below it shows again.</summary>
    public void RemoveAsset(RecordId id, string extension)
    {
        var path = AssetPath(id, extension);
        if (File.Exists(path))
            File.Delete(path);
    }
}
