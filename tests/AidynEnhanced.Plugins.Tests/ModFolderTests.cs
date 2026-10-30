using System.Text.Json.Nodes;
using AidynEnhanced.Plugins.World;

namespace AidynEnhanced.Plugins.Tests;

public sealed class ModFolderTests
{
    [Fact]
    public void Makes_a_mod_with_a_manifest_that_depends_on_the_base_and_opens_it_again()
    {
        using var temp = new TempPlugins();
        var mod = ModFolder.OpenOrCreate(temp.Root, "my_village", "My village");
        Assert.Equal(Path.Combine(temp.Root, "my_village"), mod.Directory);
        Assert.Equal("My village", mod.Manifest.Name);
        Assert.Equal(["aidyn"], mod.Manifest.Dependencies);
        Assert.True(File.Exists(Path.Combine(mod.Directory, "plugin.json")));

        var again = ModFolder.OpenOrCreate(temp.Root, "my_village");
        Assert.Equal("My village", again.Manifest.Name);
    }

    [Theory]
    [InlineData("My Village")]
    [InlineData("aidyn")]
    [InlineData("aidyn.base")]
    [InlineData("")]
    public void Refuses_a_bad_id(string id)
    {
        using var temp = new TempPlugins();
        Assert.Throws<InvalidDataException>(() => ModFolder.OpenOrCreate(temp.Root, id));
    }

    [Fact]
    public void Writes_a_patch_in_place_of_the_one_for_the_same_record_and_keeps_the_others()
    {
        using var temp = new TempPlugins();
        var mod = ModFolder.OpenOrCreate(temp.Root, "m");
        mod.WriteRecord("maps/Overworld.json", new JsonObject { ["patch"] = "aidyn:map/0x0E96", ["objects"] = new JsonArray() });
        mod.WriteRecord("maps/Overworld.json", new JsonObject { ["patch"] = "aidyn:map/0x0E97", ["grid"] = null });
        mod.WriteRecord("maps/Overworld.json", new JsonObject { ["patch"] = "aidyn:map/0x0E96", ["drawnNeighbours"] = "North" });
        var file = JsonNode.Parse(File.ReadAllText(Path.Combine(mod.Directory, "records", "maps", "Overworld.json")))!.AsArray();
        Assert.Equal(2, file.Count);
        Assert.Equal("aidyn:map/0x0E96", file[0]!["patch"]!.GetValue<string>());
        Assert.Null(file[0]!["objects"]);
        Assert.Equal("North", file[0]!["drawnNeighbours"]!.GetValue<string>());
        Assert.Equal("aidyn:map/0x0E97", file[1]!["patch"]!.GetValue<string>());

        mod.RemoveRecord("maps/Overworld.json", RecordId.Parse("aidyn:map/0x0E96"));
        mod.RemoveRecord("maps/Overworld.json", RecordId.Parse("aidyn:map/0x0E97"));
        Assert.False(File.Exists(Path.Combine(mod.Directory, "records", "maps", "Overworld.json")));
    }

    [Fact]
    public void Assets_go_where_the_loader_looks()
    {
        using var temp = new TempPlugins();
        var mod = ModFolder.OpenOrCreate(temp.Root, "m");
        var id = RecordId.Parse("aidyn:scene/0x0CF6");
        mod.WriteAsset(id, "glb", [1, 2, 3]);
        Assert.Equal(Path.Combine(mod.Directory, "assets", "aidyn", "scene", "0x0CF6.glb"), mod.AssetPath(id, "glb"));
        Assert.Equal(new byte[] { 1, 2, 3 }, File.ReadAllBytes(mod.AssetPath(id, "glb")));

        temp.Add("aidyn", null);
        var set = PluginSet.Load([Path.Combine(temp.Root, "aidyn"), mod.Directory]);
        Assert.Equal(mod.AssetPath(id, "glb"), set.FindAsset(id, "glb"));

        mod.RemoveAsset(id, "glb");
        Assert.False(File.Exists(mod.AssetPath(id, "glb")));
    }
}
