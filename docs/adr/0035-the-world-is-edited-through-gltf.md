# 0035. The world is edited through a glTF round trip, one chunk at a time

Status: Accepted · Oct 30, 2026

## Context

Phase 5's Construction Set ([roadmap](../roadmap.md#phase-5-mod-system-and-tools)) is the largest open task: record editing, dialogue graphs, map object placement and an atlas over mods. The owner asked for the existing world to be easy to edit now, before the rest of that work: moving and adding map objects, reshaping a chunk's terrain and collision, and building new ground in the original's style of low-polygon textured faces.

Everything the runtime draws is already ordinary files in the base plugin ([0001](0001-importer-owns-the-rom.md), [0005](0005-plugin-format.md)): a chunk is a scene asset in glTF, a collision asset in glTF and a map record of objects, with the chunk grid on the map set record. A mod's asset of the same ID replaces the base's. So the world can be edited by any tool that reads and writes glTF, if the parts come out as one file a modeller can work on and go back in as the plugin's own layout.

Two routes were weighed. Gizmos in the editor's map view would be easiest to pick up, but reach only objects: terrain and collision would wait for a mesh tool, which is a much larger job, and the editor draws with the runtime's code, which isn't an editing kit. Godot's own scene format isn't an option either: the runtime loads glTF through Godot at run time, never `.tscn` ([0010](0010-editor-reads-plugins.md), [0022](0022-views-are-shared.md)). A round trip through a modeller, Blender first, covers terrain, collision, textures and objects at once, with tools the owner already uses for Alaron's remodel ([replacing graphics](../replacing-graphics.md)), and what it needs to write a mod is what in-editor placement needs later.

## Decision

- **One chunk is one glTF.** `aidyn-enhanced chunk export` writes a chunk of a map set as a single file: a root node named for the chunk, carrying which set and chunk it is; the chunk's scenery and see-through scenery as they are in their assets; the collision as one mesh; and a node per map object at its place, with the object's fields in the node's extras and the model a scene object places under it to look at. `chunk import` reads such a file back into a mod ([editing the world](../editing-the-world.md)).
- **Collision surfaces are materials.** The collision mesh's materials are named for the surface and role of the faces they're on, `collision Grass`, `collision Stone trigger 12`, `collision Grass pak`, so painting the ground as grass or marking a trigger is assigning a material. The import reads the names back. It's the one encoding a modeller can edit without a plugin of ours.
- **What the ROM stores and an edit invalidates is worked out again.** The collision's edge and corner flags follow the triangles' order, and the grid of objects each cell lists follows the collision's extent and the objects' places; the import recomputes both by rules settled against every base map, and a test keeps them so. Nothing is asked of the modeller for them.
- **The import writes only what changed.** Scenery whose triangles and images are as the base's isn't written, nor collision as the base's, and the map's patch holds the fields that differ, object by object, with removals and additions, so a mod that moves one chest is one small patch. `--force` writes the scenery and collision regardless.
- **Mods are written by a shared writer.** `ModFolder` makes a mod's folder and manifest, puts a record or patch in place of the one for the same ID, and writes assets where the loader looks. Whatever edits the world later, the editor's own tools among them, writes through it.

## Consequences

- Editing the existing world works today for anyone with Blender, and the first new ground can be built the same way, on an existing chunk. Adding chunks to a set, and new sets, come next on the same path: the file already carries the chunk's entry, and the import refuses a chunk its set lacks until the map set patch and the new records are written.
- A scenery pass through Blender re-encodes the scene: meshes, images and materials come back as Blender writes them, not as the importer did, and the N64 settings survive only as custom properties. A chunk whose scenery was only looked at isn't rewritten, so that cost falls on chunks whose terrain was changed. How well Blender keeps the rest is for a round trip to show; the test covers the project's own reader and writer.
- The flags a modeller's file gets differ from the ROM's where a shared edge is claimed by a different triangle, which is as valid: exactly one triangle tests each shared edge either way.
- The rules the import uses are the port's, settled from the data rather than the machine code: the grid's listing rule lists what the original's cells list and a few more, which is harmless to pressing A. If a system is found that reads the grid more narrowly, the rule is tightened then.
- The `GltfBuilder` and the surface names move to `AidynEnhanced.Plugins.Assets`, since the import writes glTF and names surfaces without the ROM or the core.
