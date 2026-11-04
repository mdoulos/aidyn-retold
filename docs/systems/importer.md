# The importer and the ROM plugin (M1)

`acre import <rom>` turns a ROM into `aidyn.rom`, the plugin the runtime loads (ADR 0001). It goes to `plugins/aidyn.rom` in the data folder ([runtime](runtime.md#the-data-folder)): from the CLI, `ACRE_DATA` if set, else `%LOCALAPPDATA%\AidynRetold`. `--out` puts it elsewhere. It refuses the debug ROM (ADR 0007). It refuses a data edit of NA 1.0, a ROM hack that changed only the tables the layout places, saying the unmodified 1.0 ROM is needed, so the ROM plugin is the same for every player ([ADR 0038](../adr/0038-only-the-unmodified-1.0-rom-imports.md), [rom](../formats/rom.md#data-edits-of-10)); its changes are made as base plugins. The checks that the importer's facts name what 1.0's data names, a flying weapon's position and a spell's ID, always apply. The editor's import dialog runs the same importer, asking for the unmodified NA 1.0 ROM ([editor](editor.md#the-import-dialog)).

## No choices, no corrections

The import takes no choices and corrects nothing ([ADR 0043](../adr/0043-the-rom-is-asked-for-once-and-fixes-are-base-plugins.md)): the ROM plugin is the ROM's content as it is, and what the game needs fixed is the `acre` base plugin's, over it ([hardcoded content](../hardcoded-content.md#corrections-to-the-roms-maps)). Importers 60 and 61 moved the Roog Cave's mushroom by default (`MapCorrections`, `ImportOptions.FixMushroom`) and recorded the choice in the manifest's `import.options`, which reading now passes over; 62 leaves it where the ROM has it, so their imports are out of date.

## The flow

`RomImporter.Import` stages the plugin in a sibling folder (`<dir>.importing`) and moves it into place when it's done, moving the old import aside to `<dir>.old` first and deleting it after, so a file held open can't leave it half deleted. A failed import deletes its staging folder and leaves the old one in place. One killed partway leaves its folders for the next import to clear, and loading skips them (`RomPlugin.IsImportWork`), as they hold the ROM plugin's manifest and would otherwise stop every load as a plugin in a folder not named for its ID. An import from before [ADR 0037](../adr/0037-the-rom-plugin-base-plugins-and-mods.md) is in `aidyn.base`: the first check or load moves it to `aidyn.rom` (`RomPlugin.MoveFromOldFolder`), and one the move couldn't take is skipped by loading and deleted by the next import into `aidyn.rom`. An `ImportContext` holds what the families share: the ROM, its Borg archive and DB, each code-segment table read once on first use (`EngineTables`, `BodySoundTables`, `AudioTables` and the rest), and the writers for records and assets. An importer per family then builds its records, in this order:

- `WorldImporter`: map sets, terrains, weather, the World tab's map, camping, the new game, the title, the sky.
- `CombatImporter`: the combat record (`aidyn:combat/0`) and its `text` ([combat](combat.md), [hardcoded content](../hardcoded-content.md#bespoke-behaviours)).
- `MagicImporter`: spells' and potions' effects and visuals, and the magic record (`aidyn:magic/0`) ([magic](magic.md)). The ROM keeps no effects as data, since the original's code switches on a spell's kind, so each is a fact checked against 1.0's machine code, and the importer fails on a spell without one, a fact without a spell, or a name or entity place that differs.
- `FlagImporter`: flags, the journal, the state flags.
- `CharacterImporter`: dialogue entities' names, entities, spells, stats, skills, progression.
- `ItemImporter`: weapons, armor, items, loot, shops, recipes, inventory.
- `TextImporter`: strings, fonts ([text](../formats/text.md)).
- `DialogueImporter`: Borg13 scripts.
- `CinematicImporter`: the four cinematics, the credits.
- `TriggerImporter`: the original game's triggers, `aidyn:trigger/<n>` in `triggers.json` ([events](events.md)).
- `MapImporter`: minimaps, maps and their collision.
- `GraphicsImporter`: actors, scenes and their emitters, textures, UI images.
- `AudioImporter`: the sounds record, sequences, samples.

`BodySoundImporter` and `MinimapImporter` build the sounds and minimap records the others write.

## The ROM plugin

- **`plugin.json`** records the importer version (`RomPlugin.ImporterVersion`), and the ROM's SHA-1 and version. `RomPlugin.Diagnose`, which the runtime, the CLI and the editor all go by, refuses a base another importer version wrote, so the player imports again.
- **`records/`** holds a JSON file per family (`entities.json`, `combat.json`, `triggers.json` and so on), each record's system doc describing it. The loader reads every JSON file under `records/`, whatever the layout (ADR 0005), so the split is only for reading and diffing. Maps are a file per map set (`maps/Overworld.json` and so on).
- **`assets/aidyn/<type>/<key>`** holds every scene as a glb (`aidyn:scene/0xHHHH`, the same glTF as `acre scenes`), with locator nodes (`locator <i>`) for what the game attaches at run time; every chunk's collision (`aidyn:map/0xHHHH`, in the chunk's own coordinates); every texture and UI image as a PNG (`aidyn:texture/0xHHHH`, full size only, and `aidyn:image/0xHHHH`, which includes the sky gradients); every Borg12's events (`sequence/0xHHHH.dcm`) and every Borg11 (`sample/0xHHHH.wav`, [audio](../formats/audio.md#in-the-base-plugin)). Scenes carry their own textures; the texture records are for what the engine draws with a texture alone, such as bodies' shadows and particles. A mod replaces an asset by putting a file at the same path in its own `assets/`. The core reads some itself, headlessly, through `AidynRetold.Plugins.Assets`: collision (`CollisionMesh`), scenes' triangles (`SceneMesh`), actors' timing and root motion (`ActorClips`), and images' sizes (`PluginSet.ImageSize`).
- **Record IDs** follow ADR 0004: `aidyn:<type>/<key>`, the key a DB `ItemID` or Borg index as `0xHHHH`, a decimal index, or for a map object the map's `0xHHHH` then its index. Map sets are numbered as the original's `EnumMapDatA`; a map (`aidyn:map/0xHHHH`) holds its map objects of every type but the code objects ([maps](../formats/maps.md#map-objects-in-the-base-plugin-06)).

## Conventions

- **Reading the ROM.** A reader of the ROM's bytes uses `Be` (big-endian fields, tables as arrays, lists up to a terminator, a Borg's bounds-checked slice) and `RomImage.Table` for a code-segment or DB table's range in the version's layout ([rom](../formats/rom.md#where-each-version-has-its-tables)).
- **`Rom` types and records.** A type in `AidynRetold.Rom` that is the ROM's form of something a plugin record also models takes a `Rom` prefix (`RomEntityRecord`, `RomCinematic` and so on), so no name in the two assemblies means two types and the importers need no `using` aliases. An enum both sides need is defined once, in `AidynRetold.Plugins` (the Borg9 reader parses straight into `CollisionFlags` and `CollisionRole`). Where the ROM's enum has members the record's leaves out, the importer casts one into the other, and `ParallelEnumTests` checks that the names and values still match.
- **Facts** are what the original's code holds as a switch or literal rather than a table. The importer carries them, checked against 1.0's machine code, and refuses a ROM where one doesn't fit (a sky image that isn't a UI image, a position list that doesn't match its DB). [hardcoded content](../hardcoded-content.md) maps each.
- **Corrections** to the ROM's data are the user's choice, one at a time, and keyed on the exact record, which the importer checks first: `MapCorrections` for the maps, listed in [hardcoded content](../hardcoded-content.md#corrections-to-the-roms-maps).
- **Bump `RomPlugin.ImporterVersion`** whenever what the importer writes changes, with a line in its comment saying what changed; a stale base is then refused rather than read wrongly. Branches made in parallel each bump it, so renumber on merge.
- **Deterministic.** Two imports are byte-identical, which a test checks. JSON is indented, with vectors and colours on one line, so two imports diff cleanly.

The format the importer writes in, and how plugins load and merge, is in [plugins](plugins.md).
