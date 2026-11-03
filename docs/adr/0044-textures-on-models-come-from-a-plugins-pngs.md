# 0044. Textures on models come from a plugin's PNGs, on one model or on every model that uses them

Status: Accepted · Nov 3, 2026 · Adds to [0004](0004-record-ids.md) (a key kind) and [0005](0005-plugin-format.md) (an asset type)

## Context

Each model's glTF carries its own copy of every texture it uses, each material naming its texture's Borg1 (`extras.borg1`) and each image named `borg1 N`. The ROM plugin also has every texture as a PNG of its own (`aidyn:texture/0xNNNN`), which only the game's sprites read. So a mod could retexture a model only by replacing the whole model, and a shared texture only by replacing every model that used it.

A replaced model is a copy of the original's: its geometry and every other texture in it. A base plugin ships in every release ([0039](0039-base-plugins-ship-beside-the-apps.md)), so a base plugin can't retexture a model that way without shipping the original's model, as the mushroom's fix couldn't copy its collision ([0043](0043-the-rom-is-asked-for-once-and-fixes-are-base-plugins.md)). The owner wants the editor to replace a model's textures for that model alone, and a texture pack is the planned way to replace them everywhere.

## Decision

- **A model texture is a PNG for one texture on one model,** `aidyn:modeltexture/<model>/<texture>`, at `assets/aidyn/modeltexture/0x29C8/0x29A9.png` for texture 0x29A9 on Brenna's model, scene 0x29C8. It's an asset alone, with no record (`RecordTypes.ModelTexture`), keyed by a new key kind, the model's key then the texture's Borg as `0xHHHH` in any namespace (`KeyKind.ModelAndTexture`, `RecordId.ModelTexture`); a mod's own model's key is a name.
- **A texture's own PNG from a plugin after the ROM plugin replaces it on every model** that uses it. The ROM plugin's PNG is the original, which the models already carry, so it doesn't count (`PluginSet.FindAsset` against `FindOriginalAsset`).
- **A model texture comes first, then the texture everywhere, then the model's own copy** (`TextureOverrides.Find`). The views load a model through `GlbFile.Load` with the plugins' textures (`TextureReplacements.For`), and `N64Material.Apply` draws an image named for a Borg1 from the replacement where there's one. The game and the editor draw them alike.
- **A PNG can be any size.** Texture coordinates are shares of the image, and the shader takes its texel from the texture it's given, so the filtering, wrapping and scrolling stay the original's.
- **A model replaced whole takes these PNGs only for images that keep the importer's names,** `borg1 N`. A remodel's own textures are its file's.
- **The editor writes them** into the plugin a person names, as it writes map objects ([editor](../systems/editor.md)).

## Consequences

- A texture pack is a folder of PNGs at any resolution, with no models in it, and a base plugin can retexture a model without shipping it.
- The game reads a PNG once until its file changes, and caches each model it loads, so a texture written while it runs shows the next time the plugins load.
- [Replacing graphics](../replacing-graphics.md#textures-on-models) says how to make one by hand.
