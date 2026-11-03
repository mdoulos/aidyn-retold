# 0037. The import is the ROM plugin; base plugins are the owner's, and mods are other people's

Status: Accepted · Nov 3, 2026 · Amends the names in [0001](0001-importer-owns-the-rom.md), [0029](0029-content-fixes-wait-for-a-bugfix-mod.md) and [0033](0033-the-project-is-aidyn-chronicles-enhanced.md)

## Context

[0001](0001-importer-owns-the-rom.md) made the import a plugin like any other, `aidyn.base`, and called it the base plugin. The docs called every other plugin a mod, including the ones the owner makes for the remake: Alaron's remodel, the menu's scroll, the music, ReChronicled's art, and the bugfix plugin [0029](0029-content-fixes-wait-for-a-bugfix-mod.md) waits for. The owner found that confusing. To them a mod is someone else's change to their game, and what comes with the remake is its base.

## Decision

- **Plugin** stays the word for the format ([0005](0005-plugin-format.md)): a folder of records and assets that the runtime and the editor load. It says nothing of who made it.
- **The ROM plugin** is what the importer makes from the player's ROM: the original game's content, as the ROM has it.
  - Its folder is `aidyn.rom`, where it was `aidyn.base`. Its namespace stays `aidyn`.
  - In code it's `RomPlugin` (was `BasePlugin`), its importer `RomImporter` (was `BaseImporter`), its namespace `RecordId.RomNamespace` and its folder in the data folder `DataPaths.RomPluginDirectory`.
- **A base plugin** is one the owner makes and ships with the remake: content of its own, or changes to the original's. 0029's bugfix mod is the bugfix base plugin.
- **A mod** is a plugin someone else makes. The docs written for people making one ([replacing graphics](../replacing-graphics.md), [editing the world](../editing-the-world.md)) keep the word, though everything they describe works the same for a base plugin.
- **"The original game"** is what the docs and code called the base game: the content the ROM plugin holds. "Base" no longer names anything of the ROM plugin's. The base values of stats and skills (`magicMod`) are another thing and keep their name.

## Consequences

- An install from before this record carries on without importing again. The first thing that reads the ROM plugin's folder moves `aidyn.base` to `aidyn.rom` (`RomPlugin.MoveFromOldFolder`): the runtime's, the CLI's and the editor's checks (`RomPlugin.Diagnose`), and loading (`PluginSet.LoadInstalled`). A move that fails leaves the old folder where it is. Loading skips it, and the next import deletes it.
- `RomPlugin.ImporterVersion` is unchanged: an import writes the same bytes, only to another folder.
- The glossary in `CLAUDE.md` holds the words. The older records keep theirs as the history they are, and this record is how to read them.
