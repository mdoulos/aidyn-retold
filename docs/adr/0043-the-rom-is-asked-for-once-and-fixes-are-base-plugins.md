# 0043. The ROM is asked for only when there's no import, in a dialog that quits on Cancel; the import corrects nothing, and the mushroom's fix is the `acre_fixes` base plugin's

Status: Accepted · Nov 3, 2026 · Supersedes [0029](0029-content-fixes-wait-for-a-bugfix-mod.md)'s import options (its bugfix base plugin stands) · Amends [0012](0012-editor-holds-the-importer.md)'s first run and [0041](0041-the-art-and-music-are-the-private-repositorys.md)'s "only art and music" · Amended by [0045](0045-the-remade-game-is-one-base-plugin.md): `acre_fixes` is the `acre` base plugin's `records/fixes/`

## Context

The editor asked for the ROM in three places: the import page that filled its window when there was no import, the same page in a window from the record browser's **Import ROM…**, and, on both, the player's choice about the Roog Cave's mushroom, **Fix the mushroom (recommended)** or **Don't fix the mushroom**. That choice was [0029](0029-content-fixes-wait-for-a-bugfix-mod.md)'s rule for a content fix the game needs: an import option, on by default, which the importer made on the parsed Borg9 (`MapCorrections`) and the manifest recorded.

The owner wants the ROM asked for once, only when there's no import, and nothing to be done without it. The ROM plugin is the original game imported from the player's ROM and never changed ([0037](0037-the-rom-plugin-base-plugins-and-mods.md), [0038](0038-only-the-unmodified-1.0-rom-imports.md)), so the mushroom's fix belongs over it, in a base plugin, with no choice to make.

A base plugin ships in every release, and what a release ships is published ([0039](0039-base-plugins-ship-beside-the-apps.md)). The mushroom's fix moves its collision box with it, 8 triangles of the chunk's collision; a base plugin with its own copy of the chunk's collision asset would ship the original's collision. It has to be a patch.

## Decision

### The ROM is asked for once

- **Only at startup, and only when there's no current import** (`RomPlugin.Diagnose`): none, or one an older importer wrote, which has to be done again. The editor shows the import dialog (`ImportDialog`) over its empty window before anything loads: "The Aidyn Chronicles Retold Editor requires a North American 1.0 ROM.", the ROM with **Browse...**, **Import** and **Cancel**. Started by the game, as [0012](0012-editor-holds-the-importer.md) has it, it says "Aidyn Chronicles Retold requires a North American 1.0 ROM.", its window has the game's title, and it ends with **Play**.
- **Cancel, or closing the dialog, quits.** Neither the editor nor the game goes on without an import: the game has already handed off and closed, and the editor closes too.
- **The record browser's Import ROM… is gone,** with its window. A plugins folder picked there that has no current import says so and isn't loaded.
- **The import takes no choices.** `ImportOptions`, the mushroom's two buttons, `acre import --keep-mushroom` and the editor's `--keep-mushroom` are gone. The manifests importers 60 and 61 wrote, with `import.options`, still read; the field is passed over.

### The import corrects nothing; content fixes are the `acre_fixes` base plugin's

- **The importer writes the ROM's content as it is.** `MapCorrections` is gone, and the mushroom stands where the ROM has it in the ROM plugin. `RomPlugin.ImporterVersion` is 62, so an import that moved it is done again.
- **0029's bugfix base plugin is `acre_fixes`,** and the content fixes the game needs, those that 0029 made import options, are its patches. Its first is the mushroom: object 12 of the Roog Cave's D01 (`aidyn:voxel/0x1205/12`) moved by (−0.124, 0, 0.696), and its box, triangles 926 to 933 of the chunk's collision, by the same. 0029's rule for which content to fix stands: only what stops the game, each the owner's choice; the other fixes it waits for go in `acre_fixes` too.
- **A map record can move a run of its collision's triangles,** `collisionMoves` (`MapRecord.CollisionMoves`, `CollisionMove`): `{"first": 926, "count": 8, "by": [-0.124, 0, 0.696]}`, the triangles numbered as the ROM plugin's asset has them, the Borg9's order, their surfaces, roles, flags and targets kept. `CollisionMesh.For`, which everything that reads a map's collision goes through, makes the moves on the ROM plugin's asset alone: a later plugin that gives the map a collision asset of its own, as a chunk import does from an export the moves were already made in, gives it as it's to be, and the moves are left out.
- **`acre_fixes` is in the private repository's `plugins`, with the other base plugins,** though it holds no art or music: the release and `ACRE_BASE_PLUGINS` take the base plugins as one folder. It holds only patches, nothing of the ROM's but the numbers that name what they fix and where it goes, as 0039 allows.

## Consequences

- Every import before this is out of date, and the player imports once more, in the dialog, which says the import is from an older version.
- Without `acre_fixes`, as in a clone of this repository alone, the mushroom stands where the ROM has it. The tests check the fix's move without it, and the plugin itself when `ACRE_BASE_PLUGINS` has it.
- A mod can take the fix back by patching the object's position and `collisionMoves: null` over `acre_fixes`.
- The editor's map tools write a mod's map patches to `records/maps/<map set>.json`, replacing the one there for a map; `acre_fixes` keeps its patch in a file of its own, `records/mushroom.json`, so they never replace it.
