using System.Buffers.Binary;
using System.Text.Json;

namespace AidynRetold.Plugins.Assets;

/// <summary>
/// Which models use which textures: each material's texture Borg (<see cref="ExtrasKeys.Borg1"/>) in every model the plugins have,
/// the file <see cref="PluginSet.FindAsset"/> takes. It reads only each file's glTF JSON, not its binary chunk, so the ROM plugin's
/// 1,400 models take a few seconds.
/// </summary>
public sealed class TextureUsage
{
    private readonly Dictionary<RecordId, IReadOnlyList<int>> _byModel;
    private readonly Dictionary<int, List<RecordId>> _byTexture = [];

    private TextureUsage(Dictionary<RecordId, IReadOnlyList<int>> byModel)
    {
        _byModel = byModel;
        foreach (var (model, textures) in byModel.OrderBy(m => m.Key.ToString(), StringComparer.Ordinal))
        {
            foreach (var texture in textures)
            {
                if (!_byTexture.TryGetValue(texture, out var list))
                    _byTexture[texture] = list = [];
                list.Add(model);
            }
        }
    }

    /// <summary>Every model the plugins have, as the plugins have it.</summary>
    public static TextureUsage Build(PluginSet plugins) =>
        new(plugins.Assets().Where(a => a.Id.Type == RecordTypes.Scene && a.Extension == "glb")
            .ToDictionary(a => a.Id, a => (IReadOnlyList<int>)TexturesIn(a.Path)));

    /// <summary>The models that use <paramref name="texture"/>, by ID.</summary>
    public IReadOnlyList<RecordId> ModelsUsing(int texture) => _byTexture.GetValueOrDefault(texture) ?? [];

    /// <summary>The textures <paramref name="model"/>'s materials use, each once, in order.</summary>
    public IReadOnlyList<int> TexturesOf(RecordId model) => _byModel.GetValueOrDefault(model) ?? [];

    /// <summary>The textures a model file's materials use, each once, in order; none for a file that isn't a glTF binary.</summary>
    public static IReadOnlyList<int> TexturesIn(string path)
    {
        using var file = File.OpenRead(path);
        Span<byte> header = stackalloc byte[20];
        if (file.Read(header) < 20 || BinaryPrimitives.ReadUInt32LittleEndian(header) != 0x46546C67)
            return [];
        var length = (int)BinaryPrimitives.ReadUInt32LittleEndian(header[12..]);
        var json = new byte[length];
        file.ReadExactly(json);
        return TexturesIn(json);
    }

    /// <summary>The textures a glTF's JSON names on its materials, each once, in order.</summary>
    public static IReadOnlyList<int> TexturesIn(ReadOnlySpan<byte> gltfJson)
    {
        var reader = new Utf8JsonReader(gltfJson);
        using var doc = JsonDocument.ParseValue(ref reader);
        var found = new List<int>();
        if (!doc.RootElement.TryGetProperty("materials", out var materials))
            return found;
        foreach (var material in materials.EnumerateArray())
        {
            if (material.TryGetProperty("extras", out var extras) && extras.ValueKind == JsonValueKind.Object
                && extras.TryGetProperty(ExtrasKeys.Borg1, out var borg1) && borg1.TryGetInt32(out var texture) && !found.Contains(texture))
                found.Add(texture);
        }
        return found;
    }
}
