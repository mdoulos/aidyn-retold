# Replacing the game's graphics

This guide is for anyone who wants to change how Aidyn Chronicles Enhanced looks: repaint the menus, swap a model, or eventually ship a high-resolution texture pack. It explains how graphics mods work, what you can replace today, and what isn't possible yet.

## How it works

Aidyn Chronicles Enhanced never reads your ROM while you play. When you first run it, the importer turns the ROM into a folder of ordinary files called the **ROM plugin**: PNG images, 3D models in glTF format, sounds and data. The game draws everything from those files.

To change a graphic, you don't edit the ROM plugin. You make a **mod**: a second folder, next to the ROM plugin, holding only the files you want to replace. When the game looks for a file, it checks the mods first and falls back to the ROM plugin for everything they don't provide.

This keeps your work safe and separate:

- Importing the ROM again replaces the ROM plugin, but leaves your mods alone.
- Deleting a mod's folder turns it off.

## What you can replace today

| What it is | Where it's drawn | Can you replace it? |
| --- | --- | --- |
| UI images | Menus, the HUD, portraits, the title screen, the sky's gradients | Yes, at the original pixel size |
| Fonts | The text in menus, conversations, books and the battle screen | Yes: a typeface file, or the original's letter sheets at their size |
| Engine sprites | Particles such as fire and smoke, body shadows, the damage numbers in battle | Yes |
| Models | Props, buildings, characters, creatures and the scenery of each area | Yes, with care |
| Textures on models | The walls, ground, faces and clothes painted onto models | Not directly yet |

The last row is the one most people expect to work, and the [section on it](#textures-on-models) explains why it doesn't yet and how to work around it.

## Making a mod, step by step

### 1. Find the graphic's number

Every graphic has an ID made of a type and a number, such as `aidyn:image/0x0017`. The types you'll use are:

- `image`: UI images.
- `texture`: textures, including the engine's sprites.
- `scene`: models.

To find the one you want, open the editor and browse its list of textures, UI images and scenes. Each entry shows its ID, images open in a gallery, and models open in a viewer that draws them as the game does. The editor is `aidyn-enhanced-editor` beside the game in a release. From the source code, run:

```powershell
tools\godot.ps1 --path src\AidynEnhanced.Editor
```

The original files are also in the ROM plugin's folder, named by number, which is where you'll copy them from:

```
plugins\aidyn.rom\assets\aidyn\image\0x0017.png
plugins\aidyn.rom\assets\aidyn\texture\0x0003.png
plugins\aidyn.rom\assets\aidyn\scene\0x0CF6.glb
```

### 2. Make the mod's folder

The plugins folder is in the game's data folder:

| Install | Plugins folder |
| --- | --- |
| Windows installer, or running from source | `%LOCALAPPDATA%\AidynEnhanced\plugins` |
| Windows portable zip | `data\plugins`, beside the game |
| Linux | `~/.local/share/AidynEnhanced/plugins` |
| macOS | `~/Library/Application Support/AidynEnhanced/plugins` |

If the `AIDYN_ENHANCED_DATA` environment variable is set, the data folder is wherever it points.

Make a folder there for your mod, and put a file named `plugin.json` in it:

```json
{ "id": "hd_ui", "name": "HD UI", "version": "1.0.0" }
```

Two rules catch people out:

- **The ID uses only lowercase letters, digits and underscores.** `hd_ui` works, and `hd-ui` or `HD UI` don't.
- **The folder's name must be exactly the ID.** This mod lives in `plugins\hd_ui`.

The name is what people see, and can be anything.

### 3. Add your files

Copy each original you want to change from the ROM plugin into the same place inside your mod's `assets` folder, then edit your copy. The `aidyn` folder in the path means "the original game's", because these files replace the original game's graphics.

```
plugins\
  aidyn.rom\                         the imported game: don't edit it
  hd_ui\
    plugin.json
    assets\aidyn\image\0x0017.png     replaces UI image 0x0017
    assets\aidyn\texture\0x0003.png   replaces the body shadow
    assets\aidyn\scene\0x0CF6.glb     replaces model 0x0CF6
```

The file name, folder and extension have to match the original exactly, or the game won't find your file and draws the original instead.

### 4. Start the game

Start the game, or restart it if it's running. It loads the ROM plugin first and every mod after it. If two mods replace the same file, the one loaded last wins. Mods load in alphabetical order of their IDs unless one says otherwise: adding `"loadAfter": ["other_mod"]` to a mod's `plugin.json` makes it load after `other_mod`, so its files win.

If a mod is broken, for example its ID has a hyphen or its folder is misnamed, the game shows the problem on screen instead of starting. Fix it or move the folder out, and start again.

## Images: menus, the HUD and sprites

UI images and sprites are plain PNG files, and any PNG an image editor saves will load.

- **Keep the original's shape; any size works.** The game lays out its menus and HUD by the original's size, so a replacement larger than the original, such as a repaint at 8 times, is drawn into the original's place, sharper rather than larger, smoothed and with mipmaps for a smaller screen. A repaint at the same size is drawn pixel for pixel, as the original is. A different shape is squeezed into the original's.
- **The pause menu's parchment is the exception: any size.** See [below](#the-pause-menus-parchment).
- **The sky's gradients are data, not pictures.** Each is 12 columns by 37 rows. A column is one time of day, every two hours from midnight, and the game blends between neighbouring columns. The top 32 rows are the sky from the top of the screen down. The last 5 set the tint on the scenery and the colours of the fog, clouds and sun, so repainting them recolours the whole area, not just the sky. A replacement must be exactly 12 by 37, or the game ignores it.
- **Transparency comes from the alpha channel.** Keep the transparent areas transparent, or a portrait or icon gets a box around it.
- **Colours are drawn as you paint them.** The game shows the image's colours as they are, the way the N64 did, before any lighting or tint the game applies.
- **The title's logo isn't one of them.** The title screen shows Aidyn Chronicles Enhanced's own logo, which is part of the game rather than an image in a plugin, so replacing the original's logo, image `0x0103`, or its shadow, `0x0104`, changes nothing. PRESS START and the title's menu are images as usual.

### The pause menu's parchment

The parchment behind the pause menu's System, Party and World tabs is the game's own blank parchment, drawn sharper than the menu itself. It can be replaced by another picture of blank parchment at any size.

- **Put the PNG beside the scroll's model:** `assets\aidyn\scene\0x0E63.parchment.png` in your mod. The torn top and bottom edges should be transparent. The left and right edges go under the scroll's rolls, so they can be cut straight.
- **It's stretched to fit the screen.** Its width spans the screen. Its torn edges reach almost the screen's top and bottom. Any proportions work, but a 4:3 picture is stretched least.
- **Nothing is painted into it.** The original painted some of the menus into its parchment rather than drawing them: the Party tab's HitPoints, Level and Gold labels, the slots' frames, the silhouette, the knotwork and "Details", and the World tab's map backdrop, compass and the calendar's days and moons. The game's parchment is blank, and so is yours: only what the game draws over the tabs shows on it.
- **The middle tab's copy is mirrored,** so the paper runs on from tab to tab without a seam. A picture whose left and right edges differ still joins up.

## Fonts

The game draws its text in Marcellus SC, a typeface drawn sharp at any screen size, in place of the original's two letter sheets, UI images `0x001C` (the main font) and `0x001D` (the slim one the lists use).

- **To give the text another typeface,** put a `.ttf` file in your mod named for the font: `assets\aidyn\font\0.ttf` for the main font and `assets\aidyn\font\1.ttf` for the slim one. The game sizes its capitals to the sheet's, 8 pixels of the original's screen in the main font and 7 in the slim, and spaces the letters by the typeface's own widths. Your typeface's licence must let it be shipped in a mod; fonts under the SIL Open Font License, as Marcellus SC is, do.
- **The typeface draws letters, digits and the common punctuation marks.** The sheets also hold the controller buttons and a few icons (the skull, the heart, infinity), which some characters draw in place of themselves. Those always come from the sheet, so a repainted sheet changes them.
- **A repainted sheet** works as any UI image does, at its original size, for the buttons and icons it draws.

## Models

Models are glTF binary files (`.glb`), which Blender and most 3D tools open. You can change a model's shape, its UV mapping and the textures inside it. The textures can be any size: texture coordinates are stored as fractions of the image, so a texture twice the size lines up exactly where the original did.

The importer also stores things in each model that the game depends on, and a replacement has to keep them:

- **The N64 settings on each material and on the model,** such as how a texture wraps, which textures scroll like water, and where particles come from. glTF calls these *extras*. Blender imports them as custom properties, and keeps them only if you tick **Custom Properties** under **Include** when you export.
- **The names of the model's parts.** The game finds each part by its node's name: `transform 0`, `transform 1` and so on for the parts themselves, `locator 0` and up for the points where heads, held weapons and particle emitters go, and `camera` and `light 0` and up for the model's camera and lights. Don't rename or delete them.
- **For characters and creatures, the animations and the parts they move.** The animations are inside the model file, named `borg6` and a number, and each moves the parts by their names. Keep the animations and the parts' names, and reshape the mesh around them. Props and scenery without animations are much more forgiving.

Collision is stored separately from the visible model. Reshaping a piece of scenery changes how it looks, but you still walk on the original's ground and bump into the original's walls. Keep the shape close where the player can reach it.

A round trip through Blender hasn't been tested yet. After replacing a model, open it in the editor's model viewer, which draws with the game's own renderer, and check it in the game.

### Swapping a part's mesh and keeping its animations

A part can draw a new mesh without touching anything else in the file. Hang the new mesh from the part's node by a child node of its own, placed where the old mesh lay, and take the `mesh` off the part's node. The child moves with its part through every animation, and the names, extras, lights and animations all stay as the importer wrote them. A child's name mustn't start with `transform`, `locator`, `light` or `emitter`, which the game reads as parts, points, lights and particle emitters (`SceneNames`). The material needs no N64 extras: one without them is lit as the game's are, and its normal map, with `TANGENT` in the mesh, is used under **Surface Detail**.

The pause menu's scroll was remodelled this way (Oct 31, 2026), as the base plugin `menu_scroll`. The original's two rolls, `transform 4` and `transform 5` in scene `0x0E63`, became a modelled parchment roll, reduced in Blender from 182,000 triangles to 27,000 with no difference at full-screen size, the normal map carrying the fine detail. One mesh, hung from both rolls, is scaled evenly to the original's length of 17.6, and then 1.25 times along it alone, so the rolls run past the screen's top and bottom, a little beyond the parchment's torn edges. It's centred on each roll's pivot (the node's `pivot` extra) with its length along the node's Z. Its front faces the camera, and its top up the screen, in the menu's resting pose (the idle animation, `borg6 3684`, at its start), so it turns as the old rolls did through every opening and closing. The shadow strip under each roll, `transform 6`, is kept. The rolls are painted with the menu's parchment (the game's, [above](#the-pause-menus-parchment)), so the paper on the rolls matches the paper between them. The model's texture is an atlas of scattered islands, so the picture can't simply be put in its place. Instead, each texel is found on the mesh and coloured from the picture by where it lies round the roll. The picture's height, torn edge to torn edge, runs the roll's length. Its width runs round the roll at the same pixels per unit, the roll's 1.25 stretch included, so the grain is as fine both ways. The strip starts and ends at the back, fading into itself over its last tenth. The normal map is kept. The scripts that made it are kept outside the repository, as the remodels' are: `out/blender/scripts/decimate_scroll.py` reduces the model and adds its tangents, `bake_roll_parchment.py` paints the parchment onto the reduced roll, and `merge_scroll.py` builds the scene from the original.

### A skinned character

A character can also be replaced by a modern one: a single mesh that bends around a skeleton, in place of the original's rigid parts. The game needs no change for this. It loads the file through Godot's glTF importer, which skins the mesh, and it plays the animations by their names as it does any model's. A remodelled Alaron of 1,000 triangles and 23 bones has been tested this way: he walks and runs in trek and holds his sword and shield in battle.

The file must keep these from the original:

- **The root part, `transform 2`, at the top, with the original's channels in every animation.** The game moves the character by this part's animation and draws it with its position held, so copy its translation, rotation and scale channels unchanged and put the skeleton under it.
- **The animations, named `borg6` and a number, with the original's extras** (`borg6` and `length`), sampled 60 times a second as the original's are. Each must move the skeleton's bones as well as the root.
- **The locators** `locator 0` and up, each with its `locator N attachment` child, hung from the bones that stand for the original's parts: a held weapon, a shield, the head and spell effects are placed on them. Turn each attachment so it ends up facing as the original's does, or the weapon is held at the wrong angle.
- **The model's extras and its light**, copied from the original.

A character whose head is part of the new mesh still has its original head hung from locator 3, because the head is a separate model. Replace the head's model too, with one that keeps only its root node and has no geometry.

Only Alaron has a separate head. Every other character's head is a rigid part of the body's own model, moved by the animations like any other part, so a remodel of Brenna or another companion needs no head file. Nothing turns those heads: in the original only Alaron's turns toward what he can use, and the game doesn't turn his yet. Brenna's hood is such a part too. Her head is two parts side by side under the torso: the face and hair (`transform 19`, under the neck, `transform 11`) and the hood (`transform 12`), with the hood's point (`transform 20`) hung from the top of the hood. The animations move the face and the hood on separate tracks, nearly together as she walks (within about 7°), while the point swings about 10° in her idle and 30° as she walks. A skinned remodel can weight the hood to the head bone with the hair, which the walk nearly does, or give it a bone of its own keyed from the original's `transform 12` and `transform 20` tracks to keep the point swinging.

The animations can be new ones rather than the original's, as long as each keeps the original's length in ticks, since the game times its states by them. The game moves the character by the root's translation alone, so new animations can hold the root's rotation and scale at rest and carry the body's motion in the bones. A walk's or run's two steps are each half a loop, whose stride should carry the planted foot back as fast as the game moves the character (Alaron walks at 1.43 units a second, runs at 4.26 and sneaks at 1.24), or the feet slide; and an attack should reach its swing's fastest moment at the share of its length the game strikes at, 0.42 for Alaron and 0.4 for a bow shot or a throw ([combat](systems/combat.md)). A death holds its last frame. A mod can give its character the port's full run (request 26, on Shift), or a jump (request 27, played while he's in the air, whose root motion is ignored as he carries his speed; without one his pose holds), by patching the actor with states for it: a patch is `{"patch": "aidyn:actor/0x25F9", "states": [...]}`, and an actor's `states` are replaced whole, so the patch lists all of them, the original's 45 with their branches to the new states and the new ones after. Their animations are new ones in the model file, with IDs in the mod's own namespace (`alaron_remodel:animation/0x0001` is played as `borg6 1`), and their root's translation sets the run's speed. Alaron's remodel runs in full 50% further than the original's run goes, 6.39 units a second, in the same steps, so it outpaces the original game's run; plain movement is his lighter jog. A skinned character blends from one animation into the next under the Enhancements page's **Animation Blending**, so the joins between states needn't match exactly. The root's translation is also how fast the game moves the character, so a mod can make a gait slower or faster by scaling it: Alaron's remodel jogs at 111% of the original's run speed, 4.73 units a second, faster than its lighter stride covers, the user's choice: both gaits were first fitted at 89% and 120%, and then lengthened by a quarter (Oct 30, 2026) by scaling the root's forward translation in the jog's seven clips (the original's `0x25D1`–`0x25D3`, `0x25DB`, `0x25DC`, `0x25E5` and `0x25E6`) and the sprint's start and two steps (`alaron_remodel:animation/0x0001`–`0x0003`), and the walk's seven the same day (`0x25CC`–`0x25CE`, `0x25DD`, `0x25DE`, `0x25E1` and `0x25E2`), so he walks at 125% of the original's 1.43 units a second, with the animations, their lengths and their footsteps as they were; the stops weren't touched, since a stop moves nothing. The scaling was done on the plugin's `.glb`, so a fresh export from the Blender file would need it again. Alaron's remodel plays clips from Quaternius's CC0 Universal Animation Libraries fitted this way.

Alaron's remodel jumps (Oct 31, 2026) in one of two ways, chosen at random each jump. Each is three states after its 50, its rise reached from every state by request 27 and its air left by what he's asked for as he lands. Every state's branch on 27 is split between the two rises, a chance of 49 and then 51: the state machine rolls 0 to 99 and takes the first branch whose chance brings the roll under 1 (`ActorStateMachine.Roll`), so 50 and 50 would be 51 to 49, and 49 and 51 is even. The roll is the game's animation generator's, which every body draws from, so the choice varies through play. A mod can split any request the same way, between two branches of the same request in order. The first jump's rise (`alaron_remodel:animation/0x0006`, 24 ticks) is the library's `Jump_Start` from just after its spring, as he's already off the ground, easing into the air's pose; the air (`0x0007`, 150 ticks) is `Jump_Loop`, looping until he's down; and the landing (`0x0008`, 30 ticks) is `Jump_Land`'s impact, crouch and rise played faster and two thirds as deep, a hop's and not a fall's, as he comes down standing, which moving or jumping again cuts short: coming down moving goes straight into the gait's start. The second jump (states 53 to 55, `0x0009` to `0x000B`) is made the same way from the second library's `NinjaJump_Start`, `NinjaJump_Idle_Loop` (120 ticks) and `NinjaJump_Land`: knees tucked and arms held out. Their hips keep the clips' crouch but not their rise, as the game lifts him, and their roots hold still, as it carries his speed. They were baked onto the rig with the other clips' retargeting (`out/blender/scripts/anim_jump.py`, in Blender) and added to the mod's `.glb` and its actor patch without exporting it again (`merge_jump.py`), so the gaits' scaling stands; the files before the merge are kept in `out/blender/remodel/before-jump`, and a merge starts from them, so it can run again.

The skeleton can have bones the original has no part for. Alaron's remodel has three in each hand: two for the fingers, at the knuckles and at their middle, and one for the thumb, from its root by the wrist, with both outfits weighted to them, so that an animation can close his hands. The thumb's bone carries whatever stands forward of the palm between the wrist and the fingertips, which on the armour's gloves includes the point by the wrist. His jog closes his hands, as a casual jog's, the user's choice: his elbows bend about 90° and his upper arms swing through about 30°, where the clips bent them 50 to 65° and swung them through 60°, his fingers fold into fists, and his thumbs lie in across them rather than standing up. At about 1,000 triangles a thumb turned down toward the fingers crumples, so it turns in toward the palm. His full run closes his hands the same way and keeps the clips' arms; his walk keeps the clips' arms and open hands. The hands' bones are keyed in every animation, at rest wherever the hands are open, and they blend between animations as the other bones do, so his hands close as he starts to jog or run and open as he stops.

A character can have several **outfits**, each worn by what the character has on, rather than a model for every item. Put each outfit's mesh in the same file, all skinned to the one skeleton in the same rest pose so they share the animations, with a node name of its own, and list them in the actor's patch, the first whose conditions are met being worn:

```json
{"patch": "aidyn:actor/0x25F9", "outfits": [
  {"meshes": ["outfit armour", "outfit armour head"], "armored": true},
  {"meshes": ["outfit plain"]}]}
```

`"armored": true` is met while the character wears body armour that protects, which Regular Clothing doesn't (`false` is met while it doesn't), and `"items": ["aidyn:armor/0x0503"]` while one of the items is equipped. An outfit with neither is worn by anyone, and by a body that's no character's. The meshes the other outfits name are hidden. Alaron's remodel has two: plain clothes, and the armour, which is worn over any armour that protects. Both wear the same head: the armoured outfit's head is a copy of the plain one's faces above the collar, with the same weights and texture, so the face and hair never change between them. Its body was built from its own image-to-3D model, posed to the plain one's A-pose before skinning, its limbs turned to point as the plain one's do and its hands twisted so their palms face the same way, or the outfits would hold their hands differently. At about 1,000 triangles a pauldron or a flared sleeve standing off the body breaks up as the arm turns, the arm under it showing through, so the remodel fuses each to the arm under it and lets its texture carry the shape.

Animate the bones by rotation and, for the hips and the root, translation. A bone animated by scale hasn't been tried, and the game handles a part that's both turned and scaled specially.

The game draws the material's base colour texture with its N64 lighting. A normal map carries the detail the triangles leave out: with the Advanced Graphic Settings page's **Surface Detail** on Full, as Medium and High Graphic Quality set it, the model is lit at each pixel through it, and on Original at each vertex as the game's own models are. Bake the normal map in the mesh's tangent space with +Y up the image, as Blender does, and include the mesh's tangents in the file. A metal-roughness map is ignored.

## Textures on models

The textures in the `texture` folder are only used for sprites the game draws itself: particles, shadows and the damage numbers. The textures painted on models aren't read from there. Each model file carries its own copy of every texture it uses, so putting a new `texture\0x0EAB.png` in a mod changes nothing on the models that use that texture.

Many textures belong to a single model, and those you can retexture today by replacing that one model, with the new texture inside it. Others are shared by many models: the most common is used by 85. Retexturing a shared texture today means replacing every model that uses it.

To find the models that use a texture, run this Python script with the texture's number. Each material in a model records the number of its texture, which is what it looks for:

```python
# Lists the models that use a texture, by its number: python models_using_texture.py 0x0EAB
import glob, json, os, struct, sys

texture = int(sys.argv[1], 16)
folder = os.path.expandvars(r"%LOCALAPPDATA%\AidynEnhanced\plugins\aidyn.rom\assets\aidyn\scene")
for path in sorted(glob.glob(os.path.join(folder, "*.glb"))):
    with open(path, "rb") as f:
        data = f.read()
    length = struct.unpack("<I", data[12:16])[0]
    gltf = json.loads(data[20:20 + length])
    if any((m.get("extras") or {}).get("borg1") == texture for m in gltf.get("materials", [])):
        print(os.path.basename(path))
```

On Linux or macOS, change `folder` to your plugins folder.

## What's planned

Replacing assets is part of the mod tools in the [roadmap](roadmap.md)'s Phase 5, and one change to the game would finish it for graphics:

1. **Textures on models from a mod's PNG.** When the game builds a model's materials, it would look for a mod's `texture` file with that texture's number and use it instead of the copy inside the model. Every material already records its texture's number, so this is a small change. A texture pack would then be a folder of PNGs at any resolution, with no models to touch.

The editor will eventually make and preview mods for you. For now, mods are made by hand as this guide describes, except the world: a chunk's scenery, collision and objects go out to Blender and back in with two commands ([editing the world](editing-the-world.md)).
