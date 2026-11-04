# 0045. The remade game is one plugin, the ACRE plugin

Status: Accepted · Nov 3, 2026 · Amends [0029](0029-content-fixes-wait-for-a-bugfix-mod.md)'s bugfix plugin, [0037](0037-the-rom-plugin-base-plugins-and-mods.md)'s and [0039](0039-base-plugins-ship-beside-the-apps.md)'s base plugins, [0040](0040-the-project-is-aidyn-chronicles-retold.md)'s base plugin IDs, [0041](0041-the-art-and-music-are-the-private-repositorys.md)'s `acre_*` plugins and [0043](0043-the-rom-is-asked-for-once-and-fixes-are-base-plugins.md)'s `acre_fixes`

## Context

The base plugins were six. `acre` held the game's own art. `acre_alaron_remodel`, `acre_menu_scroll`, `acre_music` and `acre_rechronicled_art` held art and music, one plugin for each source. `acre_fixes` held the content fixes. A release ships them all in one folder ([0039](0039-base-plugins-ship-beside-the-apps.md)), the player can't leave one out, and nothing in the code depends on which one is installed. Graphic Quality's Original draws the original's assets, the first plugin's, whatever is over them.

The owner sees three layers: the original game, the remade game and the modded game. Six plugins split the middle layer for no reason. With one plugin left, "base plugin", a name chosen when there were to be several, says less than its own name.

## Decision

- **The remade game is one plugin, the ACRE plugin, `acre`, named Aidyn Chronicles Retold.** It holds what the six held: the game's own art, the remodels, ReChronicled's art, the music and the fixes to the original's content. Every later change of ACRE's goes into it.
- **"Base plugin" is retired, everywhere.** The docs say the ACRE plugin. The code has `AcrePlugin.Id` and `DataPaths.AcrePlugin`. The environment variable `ACRE_PLUGIN` names the plugin's own folder: in development, the private repository's `plugins/acre`. `tools/package.ps1 -AcrePlugin` takes that folder, and the release workflow fetches it by `ACRE_PLUGIN_REPO`, `ACRE_PLUGIN_PATH` and `ACRE_PLUGIN_TOKEN`.
- **The plugins load in three layers:** the ROM plugin, then the ACRE plugin, then the mods ([0037](0037-the-rom-plugin-base-plugins-and-mods.md)). The ACRE plugin's ID must be `acre`, and no mod can have it. A mod may depend on the ACRE plugin; the ACRE plugin depends on nothing loaded after it.
- **A package carries it as `acre/` beside the apps,** in a macOS bundle's `Contents/Resources`. Its `NOTICE.md` is inside it, and a copy goes beside `LICENSE`.
- **Its own records are in the `acre:` namespace.** The remodel's Squire Armor is `acre:armor/0x0001`, and its animations are `acre:animation/0x0001` to `0x000B`. Its patches are in folders by purpose, `records/alaron_remodel/` and `records/fixes/`. Files that replace the original's are at the paths they replace, `assets/aidyn/…`, beside its own `assets/acre/…`.
- **The editor names the three layers.** Its chips say AC ROM for the ROM plugin and AC Retold for the ACRE plugin, and a mod's ID in capitalized words. Its start page calls them the original game, the remade game and a user-generated mod.
- **`NOTICE.md` lists each part's source and terms,** part by part.

## Consequences

- A save names its plugins. A save made before this names the five plugins that are gone, so loading it reports them as not installed, as for any missing plugin. A record such a save names in `acre_alaron_remodel:` (Alaron's armor) no longer exists.
- `ACRE_BASE_PLUGINS` is gone. A shell or launcher that set it now sets `ACRE_PLUGIN` to the plugin's own folder. The GitHub repository's variables and secret are renamed to match, or a release ships without the ACRE plugin.
- The build scripts write into the ACRE plugin. `Development/rechronicled-art.cmd` replaces only the images it put there before (`rechronicled-sources.json`). The remodel's export writes its records to `records/alaron_remodel/` and its models to `assets/aidyn/scene/`, and leaves `plugin.json` alone.
- The tests that check the owner's fixes load the ACRE plugin from `ACRE_PLUGIN`.
