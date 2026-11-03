using System.Text.Json;
using System.Text.Json.Nodes;
using AidynRetold.Plugins.Assets;

namespace AidynRetold.Plugins.World;

/// <summary>
/// Copies what a glTF file holds under some of its nodes into a <see cref="GltfBuilder"/>, renumbering every index as
/// it goes: the nodes and their children, their meshes with the accessors, buffer views, materials, textures, samplers
/// and images those use, their cameras, lights and skins, and the animations that move any of them, cut down to the
/// channels that do. A chunk's export carries the chunk's scenery and each placed model into one file with it
/// (<see cref="ChunkExport"/>), and the import carries the scenery out again (<see cref="ChunkImport"/>). Extras go
/// with everything, as they are.
/// </summary>
public sealed class GltfCopy
{
    private readonly GlbReader _source;
    private readonly JsonElement _root;
    private readonly GltfBuilder _into;
    private readonly Func<string, string>? _renameNode;
    // Meshes, materials and the rest are copied once however many times the nodes that use them are; the nodes are new each copy.
    private readonly Dictionary<int, int> _meshes = [], _accessors = [], _views = [], _materials = [], _textures = [], _samplers = [],
        _images = [], _cameras = [], _lights = [];
    // Skins name their joints, which are nodes, so they're copied afresh with each copy's nodes.
    private Dictionary<int, int> _nodes = [], _skins = [];

    /// <summary>
    /// A copier from <paramref name="source"/> into <paramref name="into"/>. <paramref name="renameNode"/>, if given, renames
    /// every node copied. One copier serves every copy from the same file, so that a model placed many times has its
    /// meshes and textures in the file once.
    /// </summary>
    public GltfCopy(GlbReader source, GltfBuilder into, Func<string, string>? renameNode = null)
    {
        _source = source;
        _root = source.Root;
        _into = into;
        _renameNode = renameNode;
        if (_root.TryGetProperty("extensionsUsed", out var used))
        {
            foreach (var e in used.EnumerateArray())
                into.UseExtension(e.GetString()!);
        }
    }

    /// <summary>The nodes the file's scene shows, which are what <see cref="Copy(IEnumerable{int})"/> takes to carry the whole file over.</summary>
    public static IReadOnlyList<int> SceneRoots(GlbReader glb) =>
        glb.Scene is { } scene && scene.TryGetProperty("nodes", out var nodes) ? [.. nodes.EnumerateArray().Select(n => n.GetInt32())] : [];

    /// <summary>The file copied from, which the caller opened and closes once the copies are done.</summary>
    public GlbReader Source => _source;

    /// <summary>The nodes the source's scene shows.</summary>
    public IReadOnlyList<int> SceneRoots() => SceneRoots(_source);

    /// <summary>Copies <paramref name="roots"/> and everything under and used by them, once, and returns the roots' new indices, in order.</summary>
    public static IReadOnlyList<int> Copy(GlbReader source, GltfBuilder into, IEnumerable<int> roots, Func<string, string>? renameNode = null) =>
        new GltfCopy(source, into, renameNode).Copy(roots);

    /// <summary>
    /// Copies <paramref name="roots"/> and everything under them, as new nodes, with the meshes and the rest they use copied
    /// the first time they're met, and the animations that move any of the nodes, cut to the channels that do. Returns the
    /// roots' new indices, in order.
    /// </summary>
    public IReadOnlyList<int> Copy(IEnumerable<int> roots)
    {
        _nodes = [];
        _skins = [];
        var rootList = roots.ToList();
        // Every node gets its number first, so that children, skins and animations can name nodes copied later.
        var order = new List<int>();
        foreach (var root in rootList)
            Number(root, order);
        foreach (var node in order)
            Fill(node);
        Animations();
        return [.. rootList.Select(r => _nodes[r])];
    }

    private void Number(int node, List<int> order)
    {
        if (_nodes.ContainsKey(node))
            return;
        var source = _root.GetProperty("nodes")[node];
        var name = source.TryGetProperty("name", out var n) ? n.GetString() ?? "" : "";
        var placeholder = new JsonObject();
        if (name.Length > 0 || _renameNode is not null)
            placeholder["name"] = _renameNode is null ? name : _renameNode(name);
        _nodes[node] = _into.AddNode(placeholder);
        order.Add(node);
        if (source.TryGetProperty("children", out var children))
        {
            foreach (var child in children.EnumerateArray())
                Number(child.GetInt32(), order);
        }
    }

    private void Fill(int node)
    {
        var source = _root.GetProperty("nodes")[node];
        var target = _into.Node(_nodes[node]);
        foreach (var field in source.EnumerateObject())
        {
            switch (field.Name)
            {
                case "name":
                    break;
                case "children":
                    target["children"] = new JsonArray(field.Value.EnumerateArray().Select(c => (JsonNode)_nodes[c.GetInt32()]).ToArray());
                    break;
                case "mesh":
                    target["mesh"] = Mesh(field.Value.GetInt32());
                    break;
                case "camera":
                    target["camera"] = Camera(field.Value.GetInt32());
                    break;
                case "skin":
                    target["skin"] = Skin(field.Value.GetInt32());
                    break;
                case "extensions":
                    var extensions = Clone(field.Value);
                    if (extensions["KHR_lights_punctual"] is JsonObject punctual && punctual["light"] is JsonValue light)
                        punctual["light"] = Light(light.GetValue<int>());
                    target["extensions"] = extensions;
                    break;
                default:
                    target[field.Name] = JsonNode.Parse(field.Value.GetRawText());
                    break;
            }
        }
    }

    private int Mesh(int index) => Memo(_meshes, index, () =>
    {
        var mesh = Clone(_root.GetProperty("meshes")[index]);
        foreach (var primitive in mesh["primitives"]!.AsArray().OfType<JsonObject>())
        {
            Remap(primitive["attributes"] as JsonObject, Accessor);
            if (primitive["indices"] is JsonValue indices)
                primitive["indices"] = Accessor(indices.GetValue<int>());
            if (primitive["material"] is JsonValue material)
                primitive["material"] = Material(material.GetValue<int>());
            foreach (var morph in (primitive["targets"] as JsonArray)?.OfType<JsonObject>() ?? [])
                Remap(morph, Accessor);
        }
        return _into.AddMesh(mesh);
    });

    private int Accessor(int index) => Memo(_accessors, index, () =>
    {
        var accessor = Clone(_root.GetProperty("accessors")[index]);
        if (accessor["bufferView"] is JsonValue view)
            accessor["bufferView"] = View(view.GetValue<int>());
        if (accessor["sparse"] is JsonObject sparse)
        {
            foreach (var part in new[] { "indices", "values" })
            {
                if (sparse[part] is JsonObject p && p["bufferView"] is JsonValue v)
                    p["bufferView"] = View(v.GetValue<int>());
            }
        }
        return _into.AddAccessor(accessor);
    });

    private int View(int index) => Memo(_views, index, () =>
    {
        var view = _root.GetProperty("bufferViews")[index];
        return _into.AddBufferView(_source.BufferViewBytes(index),
            view.TryGetProperty("target", out var target) ? target.GetInt32() : null,
            view.TryGetProperty("byteStride", out var stride) ? stride.GetInt32() : null);
    });

    private int Material(int index) => Memo(_materials, index, () =>
    {
        var material = Clone(_root.GetProperty("materials")[index]);
        RemapTextures(material);
        return _into.AddMaterial(material);
    });

    /// <summary>Renumbers every <c>{ "index": n }</c> texture reference in a material, wherever it is: the PBR block, the normal and other maps, and extensions'.</summary>
    private void RemapTextures(JsonObject o)
    {
        foreach (var (field, value) in o.ToList())
        {
            if (value is not JsonObject nested)
                continue;
            if (field.EndsWith("Texture", StringComparison.Ordinal) && nested["index"] is JsonValue index)
                nested["index"] = Texture(index.GetValue<int>());
            else
                RemapTextures(nested);
        }
    }

    private int Texture(int index) => Memo(_textures, index, () =>
    {
        var texture = Clone(_root.GetProperty("textures")[index]);
        if (texture["source"] is JsonValue source)
            texture["source"] = Image(source.GetValue<int>());
        if (texture["sampler"] is JsonValue sampler)
            texture["sampler"] = Sampler(sampler.GetValue<int>());
        return _into.AddTexture(texture);
    });

    private int Image(int index) => Memo(_images, index, () =>
    {
        var image = Clone(_root.GetProperty("images")[index]);
        if (image["bufferView"] is JsonValue view)
            image["bufferView"] = View(view.GetValue<int>());
        return _into.AddImage(image);
    });

    private int Sampler(int index) => Memo(_samplers, index, () => _into.AddSampler(Clone(_root.GetProperty("samplers")[index])));
    private int Camera(int index) => Memo(_cameras, index, () => _into.AddCamera(Clone(_root.GetProperty("cameras")[index])));

    private int Light(int index) => Memo(_lights, index, () =>
        _into.AddLight(Clone(_root.GetProperty("extensions").GetProperty("KHR_lights_punctual").GetProperty("lights")[index])));

    private int Skin(int index) => Memo(_skins, index, () =>
    {
        var skin = Clone(_root.GetProperty("skins")[index]);
        if (skin["inverseBindMatrices"] is JsonValue matrices)
            skin["inverseBindMatrices"] = Accessor(matrices.GetValue<int>());
        if (skin["skeleton"] is JsonValue skeleton && _nodes.TryGetValue(skeleton.GetValue<int>(), out var root))
            skin["skeleton"] = root;
        else
            skin.Remove("skeleton");
        skin["joints"] = new JsonArray(skin["joints"]!.AsArray().Select(j => _nodes.GetValueOrDefault(j!.GetValue<int>(), -1)).Where(j => j >= 0).Select(j => (JsonNode)j).ToArray());
        return _into.AddSkin(skin);
    });

    /// <summary>The source's animations, each cut to the channels that move a copied node, left out when none do.</summary>
    private void Animations()
    {
        if (!_root.TryGetProperty("animations", out var animations))
            return;
        foreach (var animation in animations.EnumerateArray())
        {
            var samplers = animation.GetProperty("samplers");
            var channels = new JsonArray();
            var samplerMap = new Dictionary<int, int>();
            var newSamplers = new JsonArray();
            foreach (var channel in animation.GetProperty("channels").EnumerateArray())
            {
                var target = channel.GetProperty("target");
                if (!target.TryGetProperty("node", out var node) || !_nodes.TryGetValue(node.GetInt32(), out var newNode))
                    continue;
                var copy = Clone(channel);
                copy["target"]!["node"] = newNode;
                var sampler = channel.GetProperty("sampler").GetInt32();
                if (!samplerMap.TryGetValue(sampler, out var newSampler))
                {
                    var s = Clone(samplers[sampler]);
                    s["input"] = Accessor(s["input"]!.GetValue<int>());
                    s["output"] = Accessor(s["output"]!.GetValue<int>());
                    newSamplers.Add(s);
                    samplerMap[sampler] = newSampler = newSamplers.Count - 1;
                }
                copy["sampler"] = newSampler;
                channels.Add(copy);
            }
            if (channels.Count == 0)
                continue;
            var result = Clone(animation);
            result["channels"] = channels;
            result["samplers"] = newSamplers;
            _into.AddAnimation(result);
        }
    }

    private void Remap(JsonObject? o, Func<int, int> map)
    {
        if (o is null)
            return;
        foreach (var (key, value) in o.ToList())
        {
            if (value is JsonValue v)
                o[key] = map(v.GetValue<int>());
        }
    }

    private static int Memo(Dictionary<int, int> map, int index, Func<int> add)
    {
        if (!map.TryGetValue(index, out var copied))
            map[index] = copied = add();
        return copied;
    }

    // Parsed afresh rather than wrapped, so the copy outlives the source's document.
    private static JsonObject Clone(JsonElement element) => JsonNode.Parse(element.GetRawText())!.AsObject();
}
