# 0040. The project is Aidyn Chronicles Retold, its code `AidynRetold.*`, and ACRE its short name and its base

Status: Accepted · Nov 3, 2026 · Supersedes [0033](0033-the-project-is-aidyn-chronicles-enhanced.md)'s names (its logo and icon are replaced, and where they're kept is [0041](0041-the-art-and-music-are-the-private-repositorys.md)'s) · Amended by [0045](0045-the-remade-game-is-one-base-plugin.md): its base plugins are one, `acre`

## Context

The owner has renamed the project from Aidyn Chronicles Enhanced to Aidyn Chronicles Retold, because it retells the game's story: the original story, with selective changes that keep the main story the same but add or remove here and there, mostly adding, and expand lore the original condensed. The mechanics and the art go on changing too. "Enhanced" said the original made better; "Retold" says the story is told again.

Its short name is ACRE, for Aidyn Chronicles Retold, written `acre-` and `acre_` where an identifier needs it. ACRE is also the base: the ROM plugin is the original game as the player's ROM has it, the foundation that's never changed ([0037](0037-the-rom-plugin-base-plugins-and-mods.md), [0038](0038-only-the-unmodified-1.0-rom-imports.md)), and ACRE's base plugins are what the remake puts over it ([0039](0039-base-plugins-ship-beside-the-apps.md)). Later, players make mods for it with the Aidyn Chronicles Retold Editor, which stays in this repository.

As with [0033](0033-the-project-is-aidyn-chronicles-enhanced.md), this renames the same product, so an installed alpha is upgraded rather than kept beside the new one.

## Decision

- **The project is Aidyn Chronicles Retold.** That's the name a player sees: the Godot projects, "Aidyn Chronicles Retold" and "Aidyn Chronicles Retold Editor", which are the windows' titles and the executables' product names; the installer, its folder `Program Files\Aidyn Chronicles Retold` and its Start menu shortcuts; the macOS bundles `Aidyn Chronicles Retold.app` and `Aidyn Chronicles Retold Editor.app`; the releases, "Aidyn Chronicles Retold alpha N"; and the messages the apps and the importer show.
- **The code is Aidyn Retold's, `AidynRetold.*`.** The solution is `AidynRetold.slnx`, and the projects are `AidynRetold.Rom`, `AidynRetold.Plugins`, `AidynRetold.Core`, `AidynRetold.Cli`, `AidynRetold.Views`, `AidynRetold.Game` and `AidynRetold.Editor`, with tests in `AidynRetold.Core.Tests`, `AidynRetold.Plugins.Tests`, `AidynRetold.Rom.Tests` and `AidynRetold.TestSupport`. Folders, csproj files, root namespaces and assembly names match. So do the names made from them: the data folder `AidynRetold`, the packages `AidynRetold-alpha-N-<platform>…`, the bundle identifiers `com.mdoulos.aidynretold` and `com.mdoulos.aidynretold.editor`, and the shortcuts' registry key, `HKCU\Software\mdoulos\AidynRetold`.
- **ACRE is the short name, and `acre` its identifier.**
  - `acre-` where a name is joined with hyphens: the tool `acre`, the packaged executables `acre` and `acre-editor`, and the branding's files, `acre-icon.png` and `acre-logo.png`.
  - `acre_`, or `ACRE_` in capitals, where it's joined with underscores: `ACRE_DATA` for the data folder, `ACRE_ROMS` for the tests' ROMs, `ACRE_BASE_PLUGINS` for the base plugins, and `ACRE_*` for the other debugging variables; and the base plugins' IDs.
  - ACRE in prose and on the branding: the ACRE icon, `Launch ACRE.cmd`.
- **The base plugins are ACRE's.** A base plugin's ID starts `acre`: `acre` itself is the game's own art ([0041](0041-the-art-and-music-are-the-private-repositorys.md)), and the rest are `acre_<name>`, as `acre_music`. A plugin's own records are in the namespace of its ID ([0004](0004-record-ids.md)), so the game's own content is in `acre:`, where the port's own pictures were in `aidyn_enhanced:` (`MenuImages.OwnNamespace`). A mod takes a name of its own.
- **The installer upgrades Aidyn Chronicles Enhanced.** Its `UpgradeCode` stays the same, so installing Aidyn Chronicles Retold replaces an installed alpha, in the new folder.
- **The data folder isn't moved for the player,** as in 0033. A player who imported under the old name renames `AidynEnhanced` to `AidynRetold`, or points `ACRE_DATA` at the old folder. The owner's was moved on Nov 3, 2026.
- **What keeps "aidyn"** is what 0030 and 0033 kept, for the same reason: the original game's names and its content, as the ROM plugin `aidyn.rom` and the `aidyn:` record IDs; everything written into plugins, saves and settings in those; the Ghidra scripts' project name; and `TempPlugins.Aidyn`. `RomPlugin.ImporterVersion` isn't bumped. The repository keeps its URL, `github.com/mdoulos/aidyn-chronicles-enhanced`, which the installer's About link and the release notes' example name, until it's renamed on GitHub, which redirects the old one.

## Consequences

- Anything that built or ran the old paths (`src/AidynEnhanced.*`, `aidyn-enhanced import`, `AIDYN_ENHANCED_*`) uses the new ones. The Godot projects' gitignored `.godot` folders are made again on the first build.
- The glTFs the importer writes name `acre AidynRetold.Rom` as their generator. Earlier imports keep the old name, which nothing reads.
- The earlier records read as the history they are, with the code's current names: the naming records, [0028](0028-the-project-is-hbht.md), [0030](0030-the-project-is-aidyn-renewed.md) and 0033, keep the names they gave. A reader looking for the current names reads this record.
