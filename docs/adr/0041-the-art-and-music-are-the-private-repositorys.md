# 0041. The art and music are the private repository's: the game's own art is the `acre` base plugin, and the branding is kept beside it

Status: Accepted · Nov 3, 2026 · Adds to [0039](0039-base-plugins-ship-beside-the-apps.md); replaces where [0033](0033-the-project-is-aidyn-chronicles-enhanced.md) kept the logo and the icon

## Context

[0039](0039-base-plugins-ship-beside-the-apps.md) put the owner's base plugins in a private repository of their own. Some of the owner's art was still committed here: the menus' art in `assets/ui`, embedded in the views' assembly; the logo and the icon in `assets/branding`, with copies in the Godot projects, the logo as the title screen's `res://logo.png`; and the remodels' sources in the gitignored `out/blender`. The owner's base plugins themselves sat in the owner's data folder, as mods.

The owner wants only art and music in the private repository. That art and music is a mix of the owner's own work, licensed creative commons work, and work derived from the original game that the owner hasn't the rights to: the logo is drawn from the original's title logo, and much of the art is redrawn from the original's. The code, the docs and everything else can be public; the source isn't secret.

## Decision

- **The two repositories stand side by side** on the owner's machine, in `Aidyn Retold`: this one, `Aidyn Retold Public`, and the private one, `Aidyn Retold Private`. The private one holds `plugins/`, the base plugins, which `ACRE_BASE_PLUGINS` names, with their `NOTICE.md`; `branding/`, the logo and the icon; and `blender/`, the remodels' sources. Only art and music go in it.
- **The game's own art is the `acre` base plugin** ([0040](0040-the-project-is-aidyn-chronicles-retold.md)'s naming). Each piece is the asset of an image in the `acre` namespace, `assets/acre/image/<key>.png` for `acre:image/<key>`, its key the file's old name with underscores: `ui-gold.png` is `acre:image/ui_gold`. The views read it from the plugin set by its key (`UiImages.OwnArtPng`, `Art`, `ArtImage`), the last plugin's file winning, as for any asset, so a mod can put its own in its place. Nothing is embedded in the views' assembly but the typeface.
- **Without the `acre` plugin the game still runs.** Each piece missing is a warning, once. The original's images are drawn where the game's own stood in for them (the aspects' and schools' icons, the potions and the scrolls), and the menus' own art that stands in for nothing, such as the parchment sheets, the silhouette's figures and the hit points bar's frame, isn't drawn.
- **The title's logo is `acre:image/logo`,** the `acre` plugin's, drawn as the menus' art is (`TitleLayer.LogoView`), where it was the game project's `res://logo.png`. Without it, the title has no logo.
- **The branding is the private repository's.** `branding/` holds `acre-icon.png` and `acre-logo.png`, and `make.py`, which makes the icons and the boot splash, puts the logo in the `acre` plugin, and copies each Godot project's `icon.png`, `splash.png`, `icon.ico` and `icon.icns` into this repository, where they're gitignored. `tools/package.ps1 -Branding <folder>` refreshes them for an export, and takes the `branding` folder beside `-BasePlugins` when it isn't given; a project without them stops the build ([releases](../systems/releases.md#branding)).
- **The owner's plugins moved from the data folder into `plugins/`** as base plugins, renamed by 0040's rule, each one's own records' namespace with its ID: `acre_alaron_remodel`, `acre_menu_scroll`, `acre_music` and `acre_rechronicled_art`. `dev/rechronicled-art.cmd`, gitignored here, rebuilds the last there.
- **The typeface stays here.** Marcellus SC is under the SIL Open Font License, which lets it be bundled with software, and it isn't art or music of the owner's; it's carried in the views' assembly as before.

## Consequences

- A clone of this repository alone builds, passes its tests and runs, without the remake's own art and with no icon or splash, Godot's warnings saying so.
- A release needs the private repository: with the `BASE_PLUGINS_REPO` variable unset, packaging stops for want of the icons, where 0039 had a release ship without base plugins.
- The art committed before this record (`assets/ui`, `assets/branding` and the projects' copies of the icon, splash and logo) stays in this repository's history. Taking it out would mean rewriting the published history, which this record doesn't do.
- The README has no logo at its head.
