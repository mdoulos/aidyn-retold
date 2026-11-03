# 0038. Only the unmodified NA 1.0 ROM imports; changes to the game's data are base plugins

Status: Accepted · Nov 3, 2026 · Supersedes [0031](0031-a-data-edit-of-1.0-imports.md)'s import of a data edit (its identification stands)

## Context

[0031](0031-a-data-edit-of-1.0-imports.md) let a ROM hack of NA 1.0 that changed only its data tables import as 1.0, so the owner's Action Edition and Doulos Edit could be played on the port. The records then came from the edited tables, so the ROM plugin differed from player to player by whatever ROM each had.

The owner now makes the remake's own changes as base plugins ([0037](0037-the-rom-plugin-base-plugins-and-mods.md)), and wants them to be the source of truth: the game a player gets shouldn't depend on which ROM they brought. A base plugin can't undo a hack's edits, as it has no copy of 1.0's values to put back, and shipping a copy of every value a hack could change would put 1.0's tables in the release. Taking only 1.0 settles it, with nothing copied: the ROM plugin is then the same for everyone, and the base plugins hold only the owner's changes.

## Decision

- **The importer takes the unmodified NA 1.0 ROM** (its SHA-1, `RomVersions.Na10`), and the CLI's research import of 1.1 as before ([0007](0007-rom-versions.md)). A data edit is refused, by the editor's import page in the player's words (`ImportableRom.Open`) and by the importer itself (`RomImporter.Import`), saying the unmodified 1.0 ROM is needed.
- **A data edit is still told as one** (`RomVersions.FindDataEdit`, 0031's frame), so the refusal can name it and the CLI's dumps (`db dump` and the rest) can read its tables beside 1.0's.
- **The hacks' changes become base plugins.** What the Action Edition or the Doulos Edit changed, if the owner wants it in the remake, is made as patches to the ROM plugin's records, dumped from the hack and compared with 1.0.
- **The importer's name checks always apply.** A weapon at a flying weapon's position, or a spell, that isn't the one the fact names is an error, as 0031 had it for 1.0 alone; the escape for data edits (`checkNames`) is gone.

## Consequences

- `RomPlugin.ImporterVersion` is unchanged: an import of 1.0 is byte for byte what it was. A ROM plugin already imported from a data edit keeps loading until the next import, which needs 1.0.
- Players with only a hacked ROM need the clean one. A hack is usually shared as a patch for the clean ROM, so whoever has one has both.
- [Replacing graphics](../replacing-graphics.md) no longer says a mod survives switching to another ROM: there is no other ROM to switch to.
