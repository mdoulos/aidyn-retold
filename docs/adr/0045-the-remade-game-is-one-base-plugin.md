# 0045. The remade game is one base plugin, `acre`

Status: Accepted · Nov 3, 2026 · Amends [0040](0040-the-project-is-aidyn-chronicles-retold.md)'s base plugin IDs, [0041](0041-the-art-and-music-are-the-private-repositorys.md)'s `acre_*` plugins and [0043](0043-the-rom-is-asked-for-once-and-fixes-are-base-plugins.md)'s `acre_fixes`

## Context

The base plugins were six. `acre` held the game's own art. `acre_alaron_remodel`, `acre_menu_scroll`, `acre_music` and `acre_rechronicled_art` held art and music, one plugin for each source. `acre_fixes` held the content fixes. A release ships them all in one folder ([0039](0039-base-plugins-ship-beside-the-apps.md)), the player can't leave one out, and nothing in the code depends on which one is installed. Graphic Quality's Original draws the original's assets, the first plugin's, whatever is over them.

The owner sees three layers: the original game, the remade game and the modded game. Six plugins split the middle layer for no reason.

## Decision

- **The remade game is one base plugin, `acre`, named Aidyn Chronicles Retold.** It now holds what the other five held: the remodels, ReChronicled's art, the music and the fixes, beside the game's own art.
- **Its own records are in the `acre:` namespace.** The remodel's Squire Armor is `acre:armor/0x0001` and its animations are `acre:animation/0x0001` to `0x000B`. Its patches are in folders by purpose, `records/alaron_remodel/` and `records/fixes/`. Files that replace the original's are at the paths they replace, `assets/aidyn/…`, beside its own `assets/acre/…`.
- **"Base plugin" stays the word** for a plugin that ships with the remake between the ROM plugin and the mods ([0037](0037-the-rom-plugin-base-plugins-and-mods.md)). The loading still allows more than one, and the code doesn't change. The editor's start page calls it the remade game.
- **`NOTICE.md` still lists each part's source and terms**, part by part.

## Consequences

- The plugins load as `aidyn`, then `acre`, then the mods.
- A save names its plugins. A save made before this names the five plugins that are gone, so loading it reports them as not installed, as for any missing plugin. A record such a save names in `acre_alaron_remodel:` (Alaron's armor) no longer exists.
- The build scripts write into `acre`. `Development/rechronicled-art.cmd` replaces only the images it put there before (`rechronicled-sources.json`). The remodel's export writes its records to `records/alaron_remodel/` and its models to `assets/aidyn/scene/`, and leaves `plugin.json` alone.
- The tests that check the owner's fixes load `acre` from `ACRE_BASE_PLUGINS`.
