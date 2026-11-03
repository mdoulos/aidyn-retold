# Editing the world

This guide is for anyone who wants to change the game's world: move a chest, add a tree, reshape a hillside, repaint the ground, or build new ground in the original's style. It explains the round trip through Blender, what you can change today, and what isn't possible yet. The last section is for whoever changes the code behind it.

## How it works

The world is a set of **chunks**, the squares the game loads around you as you walk. Each chunk is three things in the ROM plugin: its scenery, a 3D model; its collision, the invisible mesh you walk on and bump into, whose faces know what they're made of; and its map objects, the chests, doors, lights, sounds, people and triggers placed in it ([replacing graphics](replacing-graphics.md#how-it-works) says how the ROM plugin and mods fit together).

Two commands carry a chunk out to one glTF file and back:

```powershell
dotnet run --project src\AidynEnhanced.Cli -- chunk export Overworld M07
dotnet run --project src\AidynEnhanced.Cli -- chunk import "out\chunks\Overworld M07.glb" --mod my_village
```

The first writes the chunk as a single `.glb`, which Blender opens. You edit it there. The second reads your file and writes into a mod only what you changed: scenery you reshaped, collision you touched, and the objects you moved, changed, added or removed. The game then plays with your mod over the original game. The ROM plugin is never written, so importing the ROM again or deleting the mod's folder puts everything back.

In a release, the commands are `aidyn-enhanced chunk export` and `aidyn-enhanced chunk import`, with the tool beside the game.

## In the editor

The editor's map view edits the objects on its own, without Blender. Open a map set or a chunk, and click an object's marker, or a placed model such as a tree or a house, to pick it. Then:

- **Drag it** along the ground. It keeps its height over the ground as it goes, so a lantern stays a lantern's height up. Hold Shift and drag up or down to raise or lower it.
- **Set its numbers** in the panel: its position, and for a placed model its rotation in degrees and its scale.
- **Drop to ground** puts it on the collision under it. **Duplicate** (Ctrl+D) makes a copy beside it with the next free number, and **Delete** takes it out.
- **Edit fields…** shows its fields as JSON to change and apply: a chest's loot, a light's colours, a door's destination. A value the game can't read is refused with what was wrong.
- **Save … to mod…** at the top writes everything changed into a mod, asking the mod's ID, and loads the plugins again so the view shows the mod. **Discard edits** throws them away. Edits are kept until one or the other, even through a reload.

The editor writes the same patches the chunk import does, so a chunk edited here and one edited in Blender make the same kind of mod, and both can be used on one mod. The ground and the collision are Blender's: the editor moves what stands on them.

## Finding a chunk

The export names a map set and a chunk. The sets are the seventeen worlds the game loads one at a time: `Overworld`, `GwerniaInteriors`, `GwerniaCastle`, `PortSaiid`, `Terminor`, `Erromon`, `Ugarit`, `Ugarit2`, `JundarInteriors`, `MiscInteriors`, `Lighthouse`, `RoogCave`, `GoblinCamp`, `Barrows`, `EhudUnder`, `ChaosIsle` and `Battlefields`. A chunk is named by row letter and column, so `M07` is row M, column 7; Gwernia is the Overworld's M07.

A map's record can also forbid the player's jump in its chunk, where a jump would get round a puzzle or a ledge: `{"patch": "aidyn:map/0x…", "noJump": true}` ([movement](systems/movement.md)).

To find one, stand there in the game and press F3: the debug overlay names the chunk you're in ([runtime](systems/runtime.md)). The editor's map view draws a set whole with its chunk grid, and `--chunk M07` starts the game in a chunk. A wrong name lists the set's chunks.

## What's in the file

Open the file in Blender with File > Import > glTF 2.0. Blender asks nothing; its defaults are right. You get one tree under a node named for the chunk, `chunk M07`:

- **`scenery`** is the chunk's model, every piece of ground, wall and roof as the game draws it. Its materials carry the game's textures. **`scenery alt`** is the see-through part, water and glass, which the game draws last.
- **`collision`** is the mesh you walk on. Its faces have one material each, named for what the face is: `collision Grass`, `collision Stone`, `collision Water`, `collision Player and camera` for an ordinary wall, `collision Player` for an invisible wall the camera sees through. A face that runs something is named for it: `collision Stone trigger 12` runs map object 12 (a camera or a conversation) when you step on it, `collision Default toggle 3` is a way that a trigger opens, and `collision Grass pak` is ground that's only there with the Expansion Pak (the game always plays as if one is in). The materials are coloured as the editor colours surfaces, so grass is green and water blue. Hide the collision while you work on the scenery, and the scenery while you work on the collision.
- **`objects`** holds a node per map object, named for what it is and its number: `container 3`, `scene 12`, `teleporter 5`, `light 7`, `monsterParty 2`, `dialogue 0`, `refPoint 40`, `camera 1`, `trigger 9`, `wanderNode 6`, `sound 4`, `savePoint 0`. Each sits where the object is. A `scene` object, a placed model such as a tree or a house, has the model under it to look at. The object's other fields are the node's custom properties (the Object Properties tab, under Custom Properties): a chest's loot and lock, a light's colours, a door's destination, a party's enemies.

## What you can do

- **Move, turn and scale things.** Grab an object node and move it. A `scene` object can be rotated and scaled too; the others only move. The game reads the node's place, not the model under it, so moving the model alone does nothing.
- **Change what an object is.** Edit its custom properties: a chest's `lockLevel`, a light's `from` and `to` colours, a sound's `volume`. The fields are those of the map records, described in `aidyn-enhanced schema`'s `map.schema.json`, and a value the game can't read stops the import with the node's name and what was wrong.
- **Add an object.** Duplicate a node of the kind you want (Shift+D). Blender names the copy `scene 12.001`; the import sees the copy and gives it the next free number. Change its properties as you like. A new `scene` object shows whatever `models` names.
- **Remove an object.** Delete its node. The import writes the removal.
- **Reshape the ground.** Edit the `scenery` mesh as any mesh: pull vertices, extrude, cut. Keep the materials on the faces, since they carry the textures. New faces take the material you give them, and the game draws the texture in the material's image with its N64 look. The original's ground is big flat faces, each with a texture and vertex colours; building more of it the same way looks as the original does.
- **Make the collision match.** The game walks on the collision, not the scenery, so a hill you raised needs the collision raised under it. Edit the `collision` mesh the same way, and give each face the material for what it is. A face whose material isn't a collision material is read as Default, and the import says so.
- **Paint the ground.** Assign `collision Grass`, `collision Sand` and so on to faces to change the footsteps and what camping finds there. Make a new material named the same way for a surface the chunk didn't have.

## Exporting from Blender

File > Export > glTF 2.0, as **glTF Binary (.glb)**. Under **Include**, tick **Custom Properties**: without it, every object's fields and the game's material settings are lost, and the import refuses the objects. Keep **+Y Up**. Under **Data > Mesh**, keep UVs, Normals and Vertex Colors on, and leave **Apply Modifiers** on. Don't rename the `chunk`, `scenery`, `collision` and `objects` nodes, or the `transform …` nodes inside the scenery.

Then import the file into your mod:

```powershell
dotnet run --project src\AidynEnhanced.Cli -- chunk import "my-chunk.glb" --mod my_village
```

The mod's folder is made in the plugins folder if it isn't there, with a manifest named for it; a mod's ID is lower-case letters, digits and underscores. The command says what it wrote: the scenery or collision as the mod's own copy of the asset, and a patch to the map record listing the objects that moved, changed, came or went. Scenery and collision whose faces didn't move aren't written, so a mod that moves one chest is one small patch; `--force` writes them regardless. Importing again replaces what the earlier import wrote for that chunk.

Check it in the game. `tools\godot.ps1 --path src\AidynEnhanced.Game ++ --chunk M07` starts you there, and the editor's map view draws the mod's chunk as the game does.

## What isn't possible yet

- **New chunks and new map sets.** The import takes a chunk the set already has. A new area is built on an existing chunk for now; adding chunks to a set is the next step on this path.
- **Textures on the scenery from a PNG.** The scenery's textures live inside the model, so a repaint goes in with the scenery. A texture pack as a folder of PNGs is planned ([replacing graphics](replacing-graphics.md#whats-planned)).
- **A placed model's own shape.** The model under a `scene` object is there to look at. To reshape a tree, replace the tree's model as [replacing graphics](replacing-graphics.md#models) says, and every tree changes.
- **The round trip through Blender hasn't been tested on scenery.** The project's own reader and writer round-trip every chunk unchanged, and moving a chest comes back as the one patch it should be. Blender re-encodes a scenery mesh it exports, and how well it keeps the vertex colours, the texture wrap settings and the two sets of texture coordinates the game's lighting uses is for a first edit to show. Look at the result in the editor's model viewer and in the game, and report what's off.

## For whoever changes the code

The export and import are in `AidynEnhanced.Plugins.World` (ADR [0035](adr/0035-the-world-is-edited-through-gltf.md)), on the plugins alone: no ROM and no core, so the CLI runs them over the installed plugins, and the editor can later.

- **`ChunkExport.Export`** builds the file with `GltfBuilder` (now in `AidynEnhanced.Plugins.Assets`, since the importer and this both write glTF). The chunk's scenery and each placed model are carried in whole with **`GltfCopy`**, which copies the nodes under some roots and everything they use, renumbering every index, with one copier per source file so a model placed fifty times has its meshes once. The root's extras are the `ChunkEntry`'s fields, the set's ID and `aidyn: chunk`, so the import knows what it has. A scenery node keeps its scene's own extras as JSON text in `sceneExtras`, which Blender carries as a string. The collision is welded by exact position into one mesh with a primitive per material (**`CollisionMaterials`** names them and reads them back). An object node is its record's JSON in its extras less the index, position, rotation and scale, which the name and the transform carry; the rotation is `SceneObject.Orientation()` and comes back through `SceneObject.RotationOf`.
- **`ChunkImport.Import`** reads the tree by node names, with copy suffixes (`.001`) dropped, and world matrices from the root down, so a moved parent still reads right. The scenery subtree goes out through `GltfCopy` into an asset of its own, with the scene extras put back, and is written only when its triangles (`SceneMesh.Read`) or its images' sizes differ from the base's. The collision's faces take their surface, role and target from their material names, and **`CollisionRules.WithFlags`** gives them their flags; it's written when anything but the flags differs. The objects are read back through **`JsonShape.Normalise`**, which puts whole floats and 0/1 back into the integer and boolean fields a tool may have loosened, then strictly deserialised, so a bad field names its node. A position, rotation or scale within a hair of the base's is kept as the base's, and the patch is a field-level diff per object (RFC 7386 inside each keyed element), `$remove` for a node that's gone, and a whole object for one that's new. **`CollisionRules.Grid`** recomputes the grid when anything it lists changed, or when the collision in effect, the mod's own whether written now or before, differs from what's below the mod, and the patch carries it only when it differs; so importing a mod's collision again unchanged, or the editor saving objects put back as the base has them, keeps the grid that collision needs. **`ModFolder`** writes the mod: its manifest, the patch into `records/maps/<set>.json` in place of the earlier one for that map, and the assets where `PluginSet.FindAsset` looks.
- **The rules were settled against every base map** ([maps](formats/maps.md#collision-the-grid-and-placement-1c)), and `ChunkRoundTripTests` holds them: the flags recomputed from the ROM's triangles match 99.5% or better (they follow the vertex-list indices, and a few maps have two vertices at one position), the grid's shape is exact on every map and its lists never drop an object the original's list, and chunks exported and imported untouched write nothing. Blender isn't on the project's machine, so a Blender pass is checked by hand.
- **What's left:** adding chunks (a map set patch and a new map record and assets in the mod's namespace; the root's extras already carry the entry), textures as PNG overrides, and in-editor placement over the same `ModFolder`.
