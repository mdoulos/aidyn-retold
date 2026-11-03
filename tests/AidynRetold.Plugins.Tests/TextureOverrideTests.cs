using System.Text.Json.Nodes;
using AidynRetold.Plugins;
using AidynRetold.Plugins.Assets;

namespace AidynRetold.Plugins.Tests;

/// <summary>
/// A model's textures from a plugin's PNGs (ADR 0044): a model texture on one model, a texture's own PNG from a later plugin on
/// every model, and which models use which textures (<see cref="TextureUsage"/>).
/// </summary>
public sealed class TextureOverrideTests : IDisposable
{
    private static readonly RecordId Brenna = RecordId.Hex(RecordTypes.Scene, 0x29C8), Goblin = RecordId.Hex(RecordTypes.Scene, 0x1001);
    private readonly TempPlugins _temp = new();

    public void Dispose() => _temp.Dispose();

    private static void Write(string plugin, string path, byte[] bytes)
    {
        var full = Path.Combine([plugin, "assets", .. path.Split('/')]);
        Directory.CreateDirectory(Path.GetDirectoryName(full)!);
        File.WriteAllBytes(full, bytes);
    }

    /// <summary>A model whose materials name these textures, as the importer writes them.</summary>
    private static byte[] Model(params int[] textures)
    {
        var gltf = new GltfBuilder();
        foreach (var t in textures)
            gltf.AddMaterial(new JsonObject { ["name"] = $"m{t}", ["extras"] = new JsonObject { [ExtrasKeys.Borg1] = t } });
        return gltf.ToGlb([gltf.AddNode(new JsonObject { ["name"] = "transform 0" })]);
    }

    [Fact]
    public void AModelTexturesIdIsItsModelThenItsTexture()
    {
        Assert.Equal("aidyn:modeltexture/0x29C8/0x29A9", RecordId.ModelTexture(Brenna, 0x29A9).ToString());
        Assert.Equal("my_mod:modeltexture/brenna/0x29A9", RecordId.ModelTexture(RecordId.Parse("my_mod:scene/brenna"), 0x29A9).ToString());
        Assert.True(RecordId.TryParse("aidyn:modeltexture/0x29C8/0x29A9", out _));
        Assert.False(RecordId.TryParse("aidyn:modeltexture/0x29C8", out _));
        Assert.False(RecordId.TryParse("aidyn:modeltexture/0x29C8/41", out _));
        Assert.False(RecordId.TryParse("aidyn:modeltexture/brenna/0x29A9", out _));
    }

    [Fact]
    public void AModelTextureComesBeforeATextureEverywhereAndEitherBeforeTheModelsOwn()
    {
        var rom = _temp.Add("aidyn", null);
        Write(rom, "aidyn/texture/0x29A9.png", [1]);
        Write(rom, "aidyn/texture/0x29AA.png", [1]);
        Write(rom, "aidyn/texture/0x29AB.png", [1]);
        var mod = _temp.Add("my_mod", null);
        Write(mod, "aidyn/modeltexture/0x29C8/0x29A9.png", [2]);
        Write(mod, "aidyn/texture/0x29A9.png", [3]);
        Write(mod, "aidyn/texture/0x29AA.png", [3]);
        var plugins = PluginSet.Load([rom, mod]);

        Assert.Equal((TextureReach.Model, "my_mod"), Reach(TextureOverrides.Find(plugins, Brenna, 0x29A9)));
        Assert.Equal((TextureReach.Everywhere, "my_mod"), Reach(TextureOverrides.Find(plugins, Goblin, 0x29A9)));
        Assert.Equal((TextureReach.Everywhere, "my_mod"), Reach(TextureOverrides.Find(plugins, Brenna, 0x29AA)));
        // The ROM plugin's own PNG of a texture is the original's, which the model's copy already is.
        Assert.Null(TextureOverrides.Find(plugins, Brenna, 0x29AB));
        Assert.Null(TextureOverrides.Find(PluginSet.Load([rom]), Brenna, 0x29A9));

        static (TextureReach, string)? Reach(TextureOverride? o) => o is null ? null : (o.Reach, o.Plugin);
    }

    [Fact]
    public void AModelTextureIsListedAsAnAssetWithItsTexturesBorg()
    {
        var rom = _temp.Add("aidyn", null);
        Write(rom, "aidyn/modeltexture/0x29C8/0x29A9.png", [1]);
        var asset = Assert.Single(PluginSet.Load([rom]).Assets());
        Assert.Equal("aidyn:modeltexture/0x29C8/0x29A9", asset.Id.ToString());
        Assert.Equal(0x29A9, new CatalogEntry(asset.Id, false, [asset], "").Borg);
    }

    [Fact]
    public void TheUsageIndexListsEachModelsTexturesAndEachTexturesModels()
    {
        var rom = _temp.Add("aidyn", null);
        Write(rom, "aidyn/scene/0x29C8.glb", Model(0x29A9, 0x2003, 0x29A9));
        Write(rom, "aidyn/scene/0x1001.glb", Model(0x2003));
        Write(rom, "aidyn/scene/0x1002.glb", []);
        var usage = TextureUsage.Build(PluginSet.Load([rom]));

        Assert.Equal([0x29A9, 0x2003], usage.TexturesOf(Brenna));
        Assert.Equal([Goblin, Brenna], usage.ModelsUsing(0x2003));
        Assert.Equal([Brenna], usage.ModelsUsing(0x29A9));
        Assert.Empty(usage.TexturesOf(RecordId.Hex(RecordTypes.Scene, 0x1002)));
        Assert.Empty(usage.ModelsUsing(0x0001));
    }
}
