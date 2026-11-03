using System.Text.Json.Nodes;
using AidynRetold.Plugins.World;

namespace AidynRetold.Plugins.Tests;

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

    /// <summary>
    /// The editor's undo: a journal notes each file a save writes or removes as it was before the first time, and restoring it puts
    /// them back, deleting what the save made.
    /// </summary>
    [Fact]
    public void A_journal_puts_back_what_a_save_changed()
    {
        using var temp = new TempPlugins();
        var mod = ModFolder.OpenOrCreate(temp.Root, "my_village");
        var kept = Plugins.RecordId.Parse("aidyn:texture/0x0001");
        var made = Plugins.RecordId.Parse("aidyn:texture/0x0002");
        var gone = Plugins.RecordId.Parse("aidyn:texture/0x0003");
        mod.WriteAsset(kept, "png", [1]);
        mod.WriteAsset(gone, "png", [3]);
        mod.WriteRecord("maps/Overworld.json", new JsonObject { ["patch"] = "aidyn:map/0x0001", ["noJump"] = true });
        var before = File.ReadAllText(Path.Combine(mod.Directory, "records", "maps", "Overworld.json"));

        mod.Journal = [];
        mod.WriteAsset(kept, "png", [9]);
        mod.WriteAsset(kept, "png", [10]);
        mod.WriteAsset(made, "png", [2]);
        mod.RemoveAsset(gone, "png");
        mod.WriteRecord("maps/Overworld.json", new JsonObject { ["patch"] = "aidyn:map/0x0001", ["noJump"] = false });
        Assert.Equal(4, mod.Journal.Count);

        ModFolder.Restore(mod.Journal);
        Assert.Equal([1], File.ReadAllBytes(mod.AssetPath(kept, "png")));
        Assert.False(File.Exists(mod.AssetPath(made, "png")));
        Assert.Equal([3], File.ReadAllBytes(mod.AssetPath(gone, "png")));
        Assert.Equal(before, File.ReadAllText(Path.Combine(mod.Directory, "records", "maps", "Overworld.json")));
    }

    /// <summary>A tool writing to a base plugin's ID writes into the base plugin, not a mod of the same ID beside it (ADR 0039).</summary>
    [Fact]
    public void Opens_a_base_plugin_of_the_id_where_it_is()
    {
        using var data = new TempPlugins();
        using var shipped = new TempPlugins();
        shipped.AddRaw("music", TempPlugins.Manifest("music"));
        Directory.CreateDirectory(data.Root);

        var opened = ModFolder.OpenOrCreate(data.Root, "music", basePluginsDirectory: shipped.Root);
        Assert.Equal(Path.Combine(shipped.Root, "music"), opened.Directory);
        Assert.False(opened.Made);
        Assert.False(Directory.Exists(Path.Combine(data.Root, "music")));

        // Any other ID is a mod in the data folder's plugins, made as it's opened.
        var mod = ModFolder.OpenOrCreate(data.Root, "my_village", basePluginsDirectory: shipped.Root);
        Assert.Equal(Path.Combine(data.Root, "my_village"), mod.Directory);
        Assert.True(mod.Made);
    }

    [Theory]
    [InlineData("My Village")]
    [InlineData("aidyn")]
    [InlineData("aidyn.rom")]
    [InlineData("")]
    public void Refuses_a_bad_id(string id)
    {
        using var temp = new TempPlugins();
        Assert.Throws<InvalidDataException>(() => ModFolder.OpenOrCreate(temp.Root, id));
    }

    [Fact]
    public void Keeps_a_hand_written_file_of_one_record_when_writing_beside_it()
    {
        using var temp = new TempPlugins();
        var mod = ModFolder.OpenOrCreate(temp.Root, "m");
        var path = Path.Combine(mod.Directory, "records", "maps", "Overworld.json");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, """{ "patch": "aidyn:map/0x0E96", "drawnNeighbours": "North" }""");
        mod.WriteRecord("maps/Overworld.json", new JsonObject { ["patch"] = "aidyn:map/0x0E97", ["grid"] = null });
        var written = (JsonArray)JsonNode.Parse(File.ReadAllText(path))!;
        Assert.Equal(["aidyn:map/0x0E96", "aidyn:map/0x0E97"], written.Select(o => o!["patch"]!.GetValue<string>()));
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
