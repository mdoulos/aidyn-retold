using System.Text.Json;
using System.Text.Json.Nodes;
using AidynRetold.Plugins.Records;

namespace AidynRetold.Plugins.Tests;

public sealed class PluginSetTests : IDisposable
{
    private readonly TempPlugins _temp = new();

    public void Dispose() => _temp.Dispose();

    private string Plugin(string id, string[]? dependencies, params (string File, string Json)[] files) => _temp.Add(id, dependencies, files);

    private string Base() => Plugin("aidyn", null, ("weapons.json", """
        [
          // Comments and trailing commas are allowed.
          { "id": "aidyn:weapon/0x0712", "name": "Club", "damage": 3, "stats": { "hit": 5, "range": 1 }, },
          { "id": "aidyn:weapon/0x0713", "name": "Mace", "damage": 5 },
        ]
        """));

    [Fact]
    public void PatchesMergeFieldByField()
    {
        var set = PluginSet.Load([
            Base(),
            Plugin("mymod", ["aidyn"], ("club.json", """{ "patch": "aidyn:weapon/0x0712", "damage": 9, "stats": { "range": null } }""")),
        ]);

        var club = set.GetJson(RecordId.Parse("aidyn:weapon/0x0712"))!;
        Assert.Equal(9, (int)club["damage"]!);
        Assert.Equal("Club", (string)club["name"]!);
        Assert.Equal(5, (int)club["stats"]!["hit"]!);
        Assert.False(club["stats"]!.AsObject().ContainsKey("range"));
    }

    [Fact]
    public void FieldsSetByTwoPluginsAreReported()
    {
        var set = PluginSet.Load([
            Base(),
            Plugin("one", ["aidyn"], ("a.json", """{ "patch": "aidyn:weapon/0x0712", "damage": 9 }""")),
            Plugin("two", ["aidyn"], ("b.json", """{ "patch": "aidyn:weapon/0x0712", "damage": 7, "name": "Big club" }""")),
        ]);

        // The base sets every field of its records, and "name" patched by one mod is a patch, not a conflict.
        var conflict = Assert.Single(set.Conflicts);
        Assert.Equal("damage", conflict.Path);
        Assert.Equal(["one", "two"], conflict.Plugins);
        Assert.Equal("two", conflict.Winner);
        Assert.Equal(7, (int)set.GetJson(RecordId.Parse("aidyn:weapon/0x0712"))!["damage"]!);
    }

    private const string MapWithThreeObjects = """
        [{ "id": "aidyn:map/0x0E87", "objects": [
          { "type": "container", "index": 0, "position": [1, 0, 1], "radius": 1, "pak": "With", "kind": "Chest", "loot": "aidyn:loot/0x0401", "reach": 2 },
          { "type": "savePoint", "index": 1, "position": [2, 0, 2], "radius": 1, "pak": "With" },
          { "type": "scene", "index": 2, "position": [3, 0, 3], "radius": 1, "pak": "With", "models": [{ "model": "aidyn:scene/0x0B48", "drawDistance": 15 }] },
        ] }]
        """;

    [Fact]
    public void KeyedListsMergeElementByElement()
    {
        var set = PluginSet.Load([
            Plugin("aidyn", null, ("maps.json", MapWithThreeObjects)),
            Plugin("mymod", ["aidyn"], ("map.json", """
                { "patch": "aidyn:map/0x0E87", "objects": [
                  { "index": 0, "reach": 5 },
                  { "index": 1, "$remove": true },
                  { "type": "savePoint", "index": 7, "position": [9, 0, 9], "radius": 2, "pak": "With" },
                ] }
                """)),
        ]);
        var id = RecordId.Parse("aidyn:map/0x0E87");
        var objects = (JsonArray)set.GetJson(id)!["objects"]!;

        // Object 0 keeps every field but the one patched, 1 is gone, 2 survives untouched and 7 is added after it.
        Assert.Equal([0, 2, 7], objects.Select(o => (int)o!["index"]!));
        Assert.Equal(5, (int)objects[0]!["reach"]!);
        Assert.Equal("Chest", (string)objects[0]!["kind"]!);
        Assert.Equal("aidyn:loot/0x0401", (string)objects[0]!["loot"]!);
        Assert.Equal(15, (int)objects[1]!["models"]![0]!["drawDistance"]!);
        Assert.False(objects[2]!.AsObject().ContainsKey("$remove"));

        var map = set.Get<MapRecord>(id);
        Assert.Equal(5, Assert.IsType<ContainerObject>(map.Objects[0]).Reach);
        Assert.IsType<SavePointObject>(map.Objects[2]);

        // The setters are kept per element and field, and a field's include whatever set what encloses it.
        Assert.Equal(["aidyn", "mymod"], set.Setters(id, "objects[0].reach"));
        Assert.Equal(["aidyn"], set.Setters(id, "objects[0].kind"));
        Assert.Equal(["aidyn", "mymod"], set.Setters(id, "objects[1]"));
        Assert.Equal(["aidyn", "mymod"], set.Setters(id, "objects[7].position"));
        Assert.Equal(["aidyn"], set.Setters(id, "objects"));
        Assert.Empty(set.Conflicts);
    }

    [Fact]
    public void KeyedElementsPatchedByTwoModsConflictAndOthersDoNot()
    {
        var set = PluginSet.Load([
            Plugin("aidyn", null, ("maps.json", MapWithThreeObjects)),
            Plugin("one", ["aidyn"], ("map.json", """{ "patch": "aidyn:map/0x0E87", "objects": [{ "index": 0, "reach": 5 }, { "index": 2, "radius": 3 }] }""")),
            Plugin("two", ["aidyn"], ("map.json", """{ "patch": "aidyn:map/0x0E87", "objects": [{ "index": 0, "reach": 6 }, { "index": 1, "$remove": true }] }""")),
        ]);
        var conflict = Assert.Single(set.Conflicts);
        Assert.Equal("objects[0].reach", conflict.Path);
        Assert.Equal(["one", "two"], conflict.Plugins);
        Assert.Equal(6, (int)set.GetJson(RecordId.Parse("aidyn:map/0x0E87"))!["objects"]![0]!["reach"]!);
    }

    [Fact]
    public void CompositeKeysAndNestedObjectsInKeyedElementsMerge()
    {
        var set = PluginSet.Load([
            Plugin("aidyn", null, ("mapsets.json", """
                [{ "id": "aidyn:mapset/0", "label": "Overworld", "cellSize": 100, "chunks": [
                  { "row": 9, "column": 8, "map": "aidyn:map/0x0E87", "terrain": 1, "music": "aidyn:sequence/0x0001" },
                  { "row": 9, "column": 9, "map": "aidyn:map/0x0E88", "terrain": 1 },
                ] }]
                """)),
            Plugin("mymod", ["aidyn"], ("mapset.json", """
                { "patch": "aidyn:mapset/0", "chunks": [{ "row": 9, "column": 8, "music": null }, { "row": 9, "column": 9, "terrain": 4 }] }
                """)),
        ]);
        var mapSet = set.Get<MapSetRecord>(RecordId.Parse("aidyn:mapset/0"));
        Assert.Equal(2, mapSet.Chunks.Count);
        Assert.Null(mapSet.Chunk(9, 8)!.Music);
        Assert.Equal("aidyn:map/0x0E87", mapSet.Chunk(9, 8)!.Map.ToString());
        Assert.Equal(4, mapSet.Chunk(9, 9)!.Terrain);
        Assert.Equal(["aidyn", "mymod"], set.Setters(mapSet.Id, "chunks[9,8].music"));
    }

    [Fact]
    public void UnkeyedListsAreReplacedWhole()
    {
        var set = PluginSet.Load([
            Plugin("aidyn", null, ("maps.json", MapWithThreeObjects)),
            Plugin("mymod", ["aidyn"], ("map.json", """
                { "patch": "aidyn:map/0x0E87", "objects": [{ "index": 2, "models": [{ "model": "aidyn:scene/0x0B49", "drawDistance": 20 }] }] }
                """)),
        ]);
        var scene = Assert.IsType<SceneObject>(set.Get<MapRecord>(RecordId.Parse("aidyn:map/0x0E87")).Objects[2]);
        var model = Assert.Single(scene.Models);
        Assert.Equal("aidyn:scene/0x0B49", model.Model.ToString());
        Assert.Equal(20, model.DrawDistance);

        // A list without a class, or a keyed list patched with something other than a list, is replaced too.
        var weapons = PluginSet.Load([
            Base(),
            Plugin("mymod", ["aidyn"], ("club.json", """{ "patch": "aidyn:weapon/0x0712", "tags": ["a"] }""")),
            Plugin("other", ["mymod"], ("club.json", """{ "patch": "aidyn:weapon/0x0712", "tags": ["b", "c"] }""")),
        ]);
        Assert.Equal(["b", "c"], ((JsonArray)weapons.GetJson(RecordId.Parse("aidyn:weapon/0x0712"))!["tags"]!).Select(t => (string)t!));
    }

    [Fact]
    public void AKeyedElementWithoutItsKeyIsRefused()
    {
        var error = Assert.Throws<InvalidDataException>(() => PluginSet.Load([
            Plugin("aidyn", null, ("maps.json", MapWithThreeObjects)),
            Plugin("mymod", ["aidyn"], ("map.json", """{ "patch": "aidyn:map/0x0E87", "objects": [{ "reach": 5 }] }""")),
        ]));
        Assert.Contains("keyed by index", error.Message, StringComparison.Ordinal);
        Assert.Contains("aidyn:map/0x0E87", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void AnEditorsSchemaPointerIsIgnored()
    {
        var set = PluginSet.Load([
            Plugin("aidyn", null, ("weapons.json", """{ "$schema": "weapon.schema.json", "id": "aidyn:weapon/0x0712", "name": "Club" }""")),
            Plugin("mymod", ["aidyn"], ("club.json", """{ "$schema": "weapon.patch.schema.json", "patch": "aidyn:weapon/0x0712", "name": "Big club" }""")),
        ]);
        var club = set.GetJson(RecordId.Parse("aidyn:weapon/0x0712"))!;
        Assert.False(club.ContainsKey("$schema"));
        Assert.Equal("Big club", (string)club["name"]!);
        Assert.Empty(set.Setters(RecordId.Parse("aidyn:weapon/0x0712"), "$schema"));
    }

    [Fact]
    public void ModsCanOnlyAddRecordsInTheirOwnNamespace()
    {
        var error = Assert.Throws<InvalidDataException>(() => PluginSet.Load([
            Base(),
            Plugin("mymod", ["aidyn"], ("new.json", """{ "id": "aidyn:weapon/0x0799", "damage": 1 }""")),
        ]));
        Assert.Contains("own namespace", error.Message, StringComparison.Ordinal);

        Directory.Delete(_temp.Root, recursive: true);
        var set = PluginSet.Load([
            Base(),
            Plugin("mymod", ["aidyn"], ("new.json", """{ "id": "mymod:weapon/sword_of_tests", "damage": 1 }""")),
        ]);
        Assert.True(set.Contains(RecordId.Parse("mymod:weapon/sword_of_tests")));
    }

    [Fact]
    public void PatchesNeedATarget()
    {
        Assert.Throws<InvalidDataException>(() => PluginSet.Load([
            Base(), Plugin("mymod", ["aidyn"], ("p.json", """{ "patch": "aidyn:weapon/0x0799", "damage": 1 }""")),
        ]));
    }

    [Fact]
    public void DependenciesLoadFirst()
    {
        Assert.Throws<InvalidDataException>(() => PluginSet.Load([Plugin("mymod", ["aidyn"]), Base()]));
    }

    [Fact]
    public void RecordsAreDefinedOnce()
    {
        Assert.Throws<InvalidDataException>(() => PluginSet.Load([
            Plugin("aidyn", null, ("a.json", """[{ "id": "aidyn:flag/1" }, { "id": "aidyn:flag/1" }]""")),
        ]));
    }

    [Fact]
    public void AssetsComeFromTheLastPluginThatHasThem()
    {
        var baseDir = Base();
        var modDir = Plugin("mymod", ["aidyn"]);
        foreach (var dir in new[] { baseDir, modDir })
        {
            Directory.CreateDirectory(Path.Combine(dir, "assets", "aidyn", "scene"));
            File.WriteAllText(Path.Combine(dir, "assets", "aidyn", "scene", "0x0A0E.glb"), "");
        }
        Directory.CreateDirectory(Path.Combine(baseDir, "assets", "aidyn", "voxel", "0x1A20"));
        File.WriteAllText(Path.Combine(baseDir, "assets", "aidyn", "voxel", "0x1A20", "12.png"), "");

        var set = PluginSet.Load([baseDir, modDir]);
        Assert.StartsWith(modDir, set.FindAsset(RecordId.Parse("aidyn:scene/0x0A0E"), "glb"), StringComparison.Ordinal);
        Assert.StartsWith(baseDir, set.FindAsset(RecordId.Parse("aidyn:voxel/0x1A20/12"), "png"), StringComparison.Ordinal);
        Assert.Null(set.FindAsset(RecordId.Parse("aidyn:scene/0x0A0F"), "glb"));
    }

    [Fact]
    public void FieldsRememberWhichPluginsSetThem()
    {
        var set = PluginSet.Load([
            Base(),
            Plugin("mymod", ["aidyn"], ("club.json", """{ "patch": "aidyn:weapon/0x0712", "damage": 9 }""")),
        ]);
        var club = RecordId.Parse("aidyn:weapon/0x0712");
        Assert.Equal("aidyn", set.DefinedBy(club));
        Assert.Equal(["aidyn", "mymod"], set.Setters(club, "damage"));
        Assert.Equal(["aidyn"], set.Setters(club, "name"));
        Assert.Empty(set.Setters(club, "weight"));
        Assert.Null(set.DefinedBy(RecordId.Parse("aidyn:weapon/0x0799")));
    }

    [Fact]
    public void TheCatalogListsRecordsAndAssetsAndWhatNamesThem()
    {
        var baseDir = Plugin("aidyn", null,
            ("mapsets.json", """[{ "id": "aidyn:mapset/0", "label": "Overworld", "cellSize": 100, "chunks": [{ "row": 9, "column": 8, "map": "aidyn:map/0x0E87" }] }]"""),
            ("maps.json", """
                [{ "id": "aidyn:map/0x0E87", "objects": [
                  { "type": "scene", "index": 0, "visible": { "flag": "aidyn:flag/5" }, "models": [{ "model": "aidyn:scene/0x0B48" }] },
                  { "type": "scene", "index": 1, "visible": { "flag": "aidyn:flag/0" }, "models": [{ "model": "aidyn:scene/0x0B48" }] },
                ] }]
                """));
        var modDir = Plugin("mymod", ["aidyn"]);
        void Asset(string dir, string path, byte[] bytes)
        {
            var full = Path.Combine([dir, "assets", .. path.Split('/')]);
            Directory.CreateDirectory(Path.GetDirectoryName(full)!);
            File.WriteAllBytes(full, bytes);
        }
        Asset(baseDir, "aidyn/scene/0x0B48.glb", []);
        Asset(baseDir, "aidyn/map/0x0E87.glb", []);
        Asset(modDir, "aidyn/scene/0x0B48.glb", []);
        Asset(baseDir, "aidyn/scene/0x0B49.glb", []);
        Asset(baseDir, "aidyn/image/0x019F.png", [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0, 0, 0, 13, .. "IHDR"u8, 0, 0, 0, 12, 0, 0, 0, 37]);
        Asset(baseDir, "aidyn/scene/not-an-id.glb", []);

        var catalog = PluginCatalog.Build(PluginSet.Load([baseDir, modDir]));

        Assert.Equal(["aidyn:image/0x019F", "aidyn:map/0x0E87", "aidyn:mapset/0", "aidyn:scene/0x0B48", "aidyn:scene/0x0B49"],
            catalog.Entries.Select(e => e.Id.ToString()));
        var map = catalog.Find(RecordId.Parse("aidyn:map/0x0E87"))!;
        Assert.True(map.HasRecord);
        Assert.Equal(0x0E87, map.Borg);
        Assert.Equal("Overworld I08, 2 scenes", map.Summary);
        Assert.Equal("aidyn", map.Asset("glb")!.Plugin);
        Assert.Equal("Overworld, 1 chunk", catalog.Find(RecordId.Parse("aidyn:mapset/0"))!.Summary);
        Assert.Null(catalog.Find(RecordId.Parse("aidyn:mapset/0"))!.Borg);

        var tree = catalog.Find(RecordId.Parse("aidyn:scene/0x0B48"))!;
        Assert.False(tree.HasRecord);
        Assert.Equal("mymod", tree.Asset("glb")!.Plugin);
        Assert.Equal("used by 2 records", tree.Summary);
        Assert.Equal(["objects[0].models[0].model", "objects[1].models[0].model"],
            catalog.ReferencesTo(tree.Id).Select(r => r.Path));
        Assert.Equal("used by nothing", catalog.Find(RecordId.Parse("aidyn:scene/0x0B49"))!.Summary);
        Assert.Equal("12×37", catalog.Find(RecordId.Parse("aidyn:image/0x019F"))!.Summary);

        // Records that no plugin defines yet, such as flags, still have their references.
        Assert.Null(catalog.Find(RecordId.Parse("aidyn:flag/5")));
        Assert.Equal("objects[0].visible.flag", Assert.Single(catalog.ReferencesTo(RecordId.Parse("aidyn:flag/5"))).Path);
    }

    /// <summary>
    /// An actor is summed up by the characters whose body it is, and a model by the actors it's the scene or head of, under their
    /// characters' names, and by the items whose model it is, so the editor's search finds them by name.
    /// </summary>
    [Fact]
    public void ActorsAndModelsAreNamedByWhoWearsThem()
    {
        using var temp = new TempPlugins();
        var dir = temp.Add("aidyn", null,
            ("entities.json", """
                [{ "id": "aidyn:entity/0x0243", "name": "Brenna", "body": { "actor": "aidyn:actor/0x29E3" } },
                 { "id": "aidyn:entity/0x0201", "name": "Goblin", "body": { "actor": "aidyn:actor/0x1000" } },
                 { "id": "aidyn:entity/0x0202", "name": "Goblin", "body": { "actor": "aidyn:actor/0x1000" } },
                 { "id": "aidyn:entity/0x0203", "name": "Goblin Chief", "body": { "actor": "aidyn:actor/0x1000" } }]
                """),
            ("dialogentities.json", """[{ "id": "aidyn:dialogentity/0x1400", "name": "Amann", "body": { "actor": "aidyn:actor/0x1000" } }]"""),
            ("actors.json", """
                [{ "id": "aidyn:actor/0x29E3", "scene": "aidyn:scene/0x29C8", "states": [{}, {}] },
                 { "id": "aidyn:actor/0x1000", "scene": "aidyn:scene/0x1001", "states": [{}], "head": { "scene": "aidyn:scene/0x1002", "locator": 3 } },
                 { "id": "aidyn:actor/0x2000", "scene": "aidyn:scene/0x2001", "states": [] }]
                """),
            ("weapons.json", """[{ "id": "aidyn:weapon/0x0700", "name": "Short Sword", "model": "aidyn:scene/0x3000" }]"""));
        // A scene is an asset alone, listed by its file.
        Directory.CreateDirectory(Path.Combine(dir, "assets", "aidyn", "scene"));
        foreach (var scene in new[] { "0x29C8", "0x1001", "0x1002", "0x2001", "0x3000" })
            File.WriteAllBytes(Path.Combine(dir, "assets", "aidyn", "scene", scene + ".glb"), []);
        var catalog = PluginCatalog.Build(PluginSet.Load([dir]));

        string Summary(string id) => catalog.Find(RecordId.Parse(id))?.Summary ?? "";
        Assert.Equal("Brenna, 2 states, scene 0x29C8", Summary("aidyn:actor/0x29E3"));
        // A name once, dialogue entities after the entities.
        Assert.Equal("Goblin, Goblin Chief, Amann, 1 state, scene 0x1001, head 0x1002", Summary("aidyn:actor/0x1000"));
        Assert.Equal("0 states, scene 0x2001", Summary("aidyn:actor/0x2000"));
        Assert.Equal("Brenna, used by 1 record", Summary("aidyn:scene/0x29C8"));
        Assert.Equal("Goblin, Goblin Chief, Amann's head, used by 1 record", Summary("aidyn:scene/0x1002"));
        Assert.Equal("actor 0x2000, used by 1 record", Summary("aidyn:scene/0x2001"));
        Assert.Equal("Short Sword, used by 1 record", Summary("aidyn:scene/0x3000"));
    }

    /// <summary>
    /// A record with no name of its own is summed up by what it holds, naming the records it names by their names: a shop by its
    /// shopkeeper, a recipe by what it makes, a new game by who leads where; a flag with no label by what names it.
    /// </summary>
    [Fact]
    public void NamelessRecordsAreSummedUpByWhatTheyHold()
    {
        using var temp = new TempPlugins();
        var dir = temp.Add("aidyn", null,
            ("entities.json", """[{ "id": "aidyn:entity/0x023F", "name": "Alaron" }, { "id": "aidyn:entity/0x0202", "name": "Female Dryad" }]"""),
            ("items.json", """[{ "id": "aidyn:item/0x0112", "name": "Herb" }, { "id": "aidyn:item/0x1000", "name": "Healing Potion" }]"""),
            ("skills.json", """[{ "id": "aidyn:skill/0", "name": "Alchemist" }]"""),
            ("mapsets.json", """[{ "id": "aidyn:mapset/0", "label": "Overworld", "chunks": [] }]"""),
            ("shops.json", """[{ "id": "aidyn:shop/0", "shopkeeper": "aidyn:entity/0x0202", "stock": [{ "item": "aidyn:item/0x0112" }] }]"""),
            ("recipes.json", """[{ "id": "aidyn:recipe/0", "skill": "aidyn:skill/0", "product": "aidyn:item/0x1000", "level": 1, "ingredients": [{}, {}] }]"""),
            ("triggers.json", """[{ "id": "aidyn:trigger/0", "on": "game.started", "do": [{ "type": "cinematic" }, { "type": "cinematic" }, { "type": "flag" }] }]"""),
            ("newgame.json", """{ "id": "aidyn:newgame/0", "chunk": { "mapSet": "aidyn:mapset/0" }, "leader": "aidyn:entity/0x023F", "gold": 1 }"""),
            ("credits.json", """{ "id": "aidyn:credits/0", "cards": [{}, {}, {}] }"""),
            ("flags.json", """[{ "id": "aidyn:flag/7" }, { "id": "aidyn:flag/8", "label": "Dougal is dead" }]"""));
        var catalog = PluginCatalog.Build(PluginSet.Load([dir]));

        string Summary(string id) => catalog.Find(RecordId.Parse(id))?.Summary ?? "";
        Assert.Equal("Female Dryad, 1 item in stock", Summary("aidyn:shop/0"));
        Assert.Equal("Healing Potion, Alchemist 1, 2 ingredients", Summary("aidyn:recipe/0"));
        Assert.Equal("on game.started: cinematic, flag", Summary("aidyn:trigger/0"));
        Assert.Equal("Alaron in Overworld, 1 gold", Summary("aidyn:newgame/0"));
        Assert.Equal("3 cards", Summary("aidyn:credits/0"));
        Assert.Equal("used by nothing", Summary("aidyn:flag/7"));
        Assert.Equal("Dougal is dead", Summary("aidyn:flag/8"));
    }

    [Fact]
    public void TypedRecordsAreReadOnceAndHoldTheMerge()
    {
        var set = PluginSet.Load([
            Base(),
            Plugin("mymod", ["aidyn"], ("club.json", """{ "patch": "aidyn:weapon/0x0712", "name": "Big club", "price": 9 }""")),
        ]);
        var club = RecordId.Parse("aidyn:weapon/0x0712");

        var read = set.Get<DbRecord>(club);
        Assert.Equal("Big club", read.Name);
        Assert.Same(read, set.Get<DbRecord>(club));
        Assert.Same(read, set.TryGet<DbRecord>(club));
        Assert.Same(read, set.All<DbRecord>(RecordTypes.Weapon)[0]);

        // The same record read as its class is that type's own object; a type the record doesn't read as is refused.
        var weapon = set.Get<WeaponRecord>(club);
        Assert.Equal(9, weapon.Price);
        Assert.Same(weapon, set.Get<WeaponRecord>(club));
        Assert.Null(set.TryGet<DbRecord>(RecordId.Parse("aidyn:weapon/0x0799")));
        Assert.Throws<ArgumentException>(() => set.TryGet<ArmorRecord>(club));
        Assert.Throws<ArgumentException>(() => set.All<WeatherRecord>(RecordTypes.Combat));
        Assert.Throws<ArgumentException>(() => set.All<SkillRecord>());
        Assert.Same(set.All<WeaponRecord>(RecordTypes.Weapon), set.All<WeaponRecord>());
    }

    [Fact]
    public void AllOfATypeIsInIdOrder()
    {
        var set = PluginSet.Load([
            Plugin("aidyn", null,
                ("b.json", """[{ "id": "aidyn:weapon/0x0713", "name": "Mace" }, { "id": "aidyn:flag/1" }]"""),
                ("a.json", """{ "id": "aidyn:weapon/0x0712", "name": "Club" }""")),
            Plugin("mymod", ["aidyn"], ("sword.json", """{ "id": "mymod:weapon/sword", "name": "Sword" }""")),
        ]);

        var weapons = set.All<DbRecord>(RecordTypes.Weapon);
        Assert.Equal(["aidyn:weapon/0x0712", "aidyn:weapon/0x0713", "mymod:weapon/sword"], weapons.Select(w => w.Id.ToString()));
        Assert.Same(weapons, set.All<DbRecord>(RecordTypes.Weapon));
        Assert.Same(weapons[2], set.Get<DbRecord>(RecordId.Parse("mymod:weapon/sword")));
        Assert.Empty(set.All<DbRecord>(RecordTypes.Armor));
    }

    [Fact]
    public void ASingletonIsTheOriginalGamesRecordZero()
    {
        var set = PluginSet.Load([
            Plugin("aidyn", null, ("camping.json", """{ "id": "aidyn:camping/0", "name": "Camping" }""")),
            Plugin("mymod", ["aidyn"],
                ("camping.json", """[{ "patch": "aidyn:camping/0", "name": "Better camping" }, { "id": "mymod:camping/0", "name": "Another" }]""")),
        ]);

        var camping = set.Singleton<CampingRecord>();
        Assert.Equal("aidyn:camping/0", camping.Id.ToString());
        Assert.Equal("Better camping", (string)set.GetJson(camping.Id)!["name"]!);
        Assert.Same(camping, set.TrySingleton<CampingRecord>());
        Assert.Null(set.TrySingleton<SkyRecord>());
        Assert.Throws<KeyNotFoundException>(() => set.Singleton<SkyRecord>());
        // A type with many records, or one that's the class of two types, has no singleton.
        Assert.Throws<ArgumentException>(() => set.Singleton<WeaponRecord>());
        Assert.Throws<ArgumentException>(() => set.Singleton<SkillRecord>());
    }

    [Fact]
    public void TheRegistryTiesEachTypeToItsClassKeyAndKinds()
    {
        Assert.Equal(typeof(CombatRecord), RecordTypes.Info(RecordTypes.Combat).Class);
        Assert.True(RecordTypes.Info(RecordTypes.Combat).Singleton);
        Assert.Equal("aidyn:combat/0", RecordTypes.Info(RecordTypes.Combat).SingletonId.ToString());
        Assert.Equal(KeyKind.Db, RecordTypes.Info(RecordTypes.Entity).Key);
        Assert.True(RecordTypes.Info(RecordTypes.Entity).Reads(typeof(DbRecord)));
        Assert.False(RecordTypes.Info(RecordTypes.Entity).Reads(typeof(ItemRecord)));
        Assert.Null(RecordTypes.Info(RecordTypes.Scene).Class);
        Assert.Equal(KeyKind.Nested, RecordTypes.Info(RecordTypes.Voxel).Key);
        Assert.Equal(RecordTypes.Entity, RecordTypes.Of<EntityRecord>().Name);
        Assert.Throws<ArgumentException>(() => RecordTypes.Info("widget"));
        Assert.Equal(RecordTypes.Registry.Select(t => t.Name), RecordTypes.Names);
        Assert.True(RecordTypes.Registry.All(t => t.Class is null || t.Class.Name.EndsWith("Record", StringComparison.Ordinal)));

        // The map object kinds come from MapObject's attributes, each with a name of its own.
        Assert.Equal(["scene", "container", "light", "sound", "wanderNode", "monsterParty", "refPoint", "teleporter", "camera", "dialogue", "trigger", "savePoint"],
            MapObjectKinds.All.Select(k => k.Discriminator));
        Assert.All(MapObjectKinds.All, k => Assert.NotEqual(k.Type.Name, k.Name));
        Assert.Equal("Monster party", MapObjectKinds.Name("monsterParty"));
    }

    [Fact]
    public void TypedRecordsRoundTrip()
    {
        var original = new SceneObject(3, new(1.5f, 2, -3), 45, PakVariant.With, new FlagGate(RecordId.Parse("aidyn:flag/0"), true),
            [new SceneModel(RecordId.Parse("aidyn:scene/0x0B48"), 15)], 1, new(0, 0.5f, 0), new(1, 1, 1),
            new Rgba(1, 2, 3, 4), SceneObjectFlags.Solid | SceneObjectFlags.Tint);
        MapRecord map = new(RecordId.Parse("aidyn:map/0x0E87"), [original]);
        var json = JsonSerializer.Serialize(map, PluginJson.Options);
        Assert.Contains("\"type\": \"scene\"", json, StringComparison.Ordinal);
        Assert.Contains("[1.5, 2, -3]", json, StringComparison.Ordinal);

        var read = PluginSet.Load([Plugin("aidyn", null, ("maps.json", json))]).Get<MapRecord>(map.Id);
        var scene = Assert.IsType<SceneObject>(Assert.Single(read.Objects));
        Assert.Equal(original.Models, scene.Models);
        Assert.Equal(original with { Models = scene.Models }, scene);
    }

    [Fact]
    public void FilesWithAByteOrderMarkLoad()
    {
        // As Windows' editors and PowerShell 5.1 save them.
        var mod = Plugin("mymod", ["aidyn"], ("club.json", """{ "patch": "aidyn:weapon/0x0712", "damage": 9 }"""));
        foreach (var file in new[] { Path.Combine(mod, "plugin.json"), Path.Combine(mod, "records", "club.json") })
            File.WriteAllText(file, File.ReadAllText(file), new System.Text.UTF8Encoding(encoderShouldEmitUTF8Identifier: true));

        var set = PluginSet.Load([Base(), mod]);

        Assert.Equal(9, (int)set.GetJson(RecordId.Parse("aidyn:weapon/0x0712"))!["damage"]!);
    }

    [Fact]
    public void AFileThatIsntJsonIsNamed()
    {
        var mod = Plugin("mymod", ["aidyn"], ("broken.json", """{ "patch": "aidyn:weapon/0x0712", "damage": """));

        var error = Assert.Throws<InvalidDataException>(() => PluginSet.Load([Base(), mod]));

        Assert.Contains(Path.Combine(mod, "records", "broken.json"), error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void AnEmptyDependencyIsRefused()
    {
        var mod = _temp.AddRaw("mymod", """{ "id": "mymod", "name": "My mod", "version": "1.0.0", "loadAfter": [null] }""");

        var error = Assert.Throws<InvalidDataException>(() => AidynRetold.Plugins.Plugin.Open(mod));

        Assert.Contains("loadAfter", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void AKeyWrittenAsAWholeNumberWithADecimalPointIsTheSameKey()
    {
        var set = PluginSet.Load([
            Plugin("aidyn", null, ("maps.json", MapWithThreeObjects)),
            Plugin("mymod", ["aidyn"], ("map.json", """{ "patch": "aidyn:map/0x0E87", "objects": [{ "index": 0.0, "reach": 5 }] }""")),
        ]);

        var objects = (JsonArray)set.GetJson(RecordId.Parse("aidyn:map/0x0E87"))!["objects"]!;
        Assert.Equal(3, objects.Count);
        Assert.Equal(5, (int)objects[0]!["reach"]!);
    }

    [Fact]
    public void ASaveIsWrittenWholeWithNothingLeftBeside()
    {
        var path = Path.Combine(_temp.Root, "saves", "Save 1.json");
        Saves.SaveFile.Write(path, new Saves.SaveGame { Label = "first" });
        Saves.SaveFile.Write(path, new Saves.SaveGame { Label = "second" });

        Assert.Equal("second", Saves.SaveFile.Read(path).Label);
        Assert.Equal(["Save 1.json"], Directory.GetFiles(Path.GetDirectoryName(path)!).Select(Path.GetFileName));
    }
}
