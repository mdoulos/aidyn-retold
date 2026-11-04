# 0039. Base plugins ship in the release, beside the apps, and load between the ROM plugin and the mods

Status: Accepted · Nov 3, 2026 · Added to by [0041](0041-the-art-and-music-are-the-private-repositorys.md): the private repository holds only art and music, the branding among them · Amended by [0045](0045-the-remade-game-is-the-acre-plugin.md): the base plugins are one, the ACRE plugin, shipped as `acre/` beside the apps

## Context

[0037](0037-the-rom-plugin-base-plugins-and-mods.md) named the owner's own plugins base plugins. Until now every plugin but the ROM plugin lived in the player's data folder, `plugins`, beside the import, and a release shipped none: the owner's remodel, music and art were only on the owner's machine.

The owner keeps the art and music in a private repository of their own and the code in this public one, and builds a release from both. What a base plugin ships has to be the owner's to ship: art and music of their own, licensed work such as CC BY music with its credit, or patches to the ROM plugin's records. Nothing derived from the ROM goes in this repository, as ever ([0001](0001-importer-owns-the-rom.md), [0034](0034-retold-and-rechronicled-collaborate.md)).

## Decision

- **Base plugins are a folder beside the apps.** An exported app reads them from `plugins` beside its executables, or, on macOS, from `Contents/Resources/plugins` in each app's bundle (`AppLauncher.UseAppFolder`, `DataPaths.BasePlugins`). They're part of the install, so an update replaces them, and the player's data folder holds only the ROM plugin, the mods, the saves and the settings.
- **In development,** `ACRE_BASE_PLUGINS` names the folder outright: the owner points it at the base plugins in their private repository, and the game, the editor and the CLI all load them. Without it, and outside an exported app, there are none.
- **They load between the ROM plugin and the mods** (`PluginSet.LoadInstalled`): the ROM plugin, then the base plugins in their own load order, then the mods in theirs. So a mod can patch a base plugin's records, and depend on one. A base plugin can't depend on a mod. A plugin is a base plugin or a mod, not both: the same ID in both places is an error naming the two folders.
- **The editor and the CLI write to a base plugin in its own folder.** Saving map objects or importing a chunk into a plugin whose ID is a base plugin's (`ModFolder.OpenOrCreate`) writes there, not into a new mod of the same ID.
- **Packaging takes the base plugins as a folder** (`tools/package.ps1 -BasePlugins <folder>`), copies each plugin in it into the package, and requires a `NOTICE.md` beside them, which goes beside `LICENSE`: the [MIT licence](../../LICENSE) covers the code and docs, not the art and music, so the release says whose they are and on what terms, with any credit their licences ask for. The release workflow checks the private repository out when the repository variable `BASE_PLUGINS_REPO` names it, with the secret `BASE_PLUGINS_TOKEN` to read it ([releases](../systems/releases.md#the-acre-plugin)). Without the variable, a release ships none, as before.

## Consequences

- A release is the apps, the base plugins and their notice. A player's first run imports the ROM plugin, and the game is the ROM plugin with the base plugins over it.
- Plugins already in a data folder carry on loading, as mods. To make one of the owner's a base plugin, it moves into the private repository's folder.
- A release's files are public downloads, whatever the repository they came from: what's in a base plugin is published by being in one.
- The editor's plugins folder is still the data folder's; the base plugins are loaded under it wherever they are.
