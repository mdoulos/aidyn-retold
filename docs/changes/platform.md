# Platform

The PC, the ROM, plugins and mods, the editor and the packages. None of this is in the original, a cartridge for the Nintendo 64.

## The game and the ROM

- **A PC game** for Windows (an installer and a portable zip), Linux and macOS ([runtime](../systems/runtime.md#the-data-folder)).
- **The player brings the ROM.** No game content is included. On its first run the game asks for the North American 1.0 ROM, unmodified,
  in any byte order, and imports it into the ROM plugin, `aidyn.rom`, once; it never reads the ROM again. A ROM hack, the debug ROM, 1.1 and
  PAL versions aren't imported for play, so the game is the same for everyone. The import makes no choices and corrects nothing; the
  remake's fixes are the ACRE plugin's ([ADR 0038](../adr/0038-only-the-unmodified-1.0-rom-imports.md),
  [ADR 0043](../adr/0043-the-rom-is-asked-for-once-and-fixes-are-base-plugins.md), [importer](../systems/importer.md)).
- **The remade game is a plugin**, the ACRE plugin, `acre`, which ships beside the apps: Retold's art, Alaron's remodel, the music, the
  Squire Armor and the content fixes, over the ROM plugin, which stays as the ROM has it
  ([ADR 0045](../adr/0045-the-remade-game-is-the-acre-plugin.md)). Without it the game runs as the original looks, with no logo.
- **Chunks load in the background**, where the original stalls as a border's crossed ([ADR 0025](../adr/0025-chunks-load-off-the-main-thread.md)).

## Mods

- **Mods are plugins**, folders in the data folder's `plugins`, loaded after the ACRE plugin: each can add or patch records field by field,
  and replace any asset, a model, a texture, a picture, a font or a piece of music, at any size. A mod can add items, flags, party members
  and triggers; the original's limits on how many records of a kind there are don't apply. Mods can't run code; sandboxed Lua is planned
  ([plugins](../systems/plugins.md), [replacing graphics](../replacing-graphics.md), [ADR 0037](../adr/0037-the-rom-plugin-base-plugins-and-mods.md)).
- **A broken mod is reported**, and the plugins' conflicts are listed ([plugins](../systems/plugins.md)).

## The editor

- **An editor comes with the game**, the Aidyn Chronicles Retold Editor: every record and asset of the plugins to browse, with its
  preview, its fields and what uses it; a quest atlas of the flags and scripts; a map view that moves, adds and removes map objects; pages
  that edit armor, weapons and items; and models' textures and models exported and replaced. It saves into a mod, each save undoable
  ([editor](../systems/editor.md)).
- **The world edits in Blender**, a chunk exported as a glTF and imported back into a mod ([editing the world](../editing-the-world.md),
  [ADR 0035](../adr/0035-the-world-is-edited-through-gltf.md)).
- **A command-line tool**, `acre`, imports the ROM and dumps, checks and converts its parts, saves and chunks (the repository's
  [CLAUDE.md](../../CLAUDE.md#code)).

## Aidyn ReChronicled

Retold and Aidyn ReChronicled, a native recompilation of the original, are two projects that share their art freely, both ways: Retold
draws ReChronicled's redrawn portraits, icons and HUD pieces, and takes a few of its fixes, each confirmed in the original's code
([ADR 0034](../adr/0034-retold-and-rechronicled-collaborate.md)).
