# Changes from the original

What Aidyn Chronicles Retold does differently from Aidyn Chronicles: The First Mage as the North American 1.0 ROM has it, by category. Each
entry says what the original does, what Retold does, and links the system's doc that has the detail and the addresses. Where an entry is
a setting, it gives the setting and its default.

Retold is a remake that retells the game ([ADR 0046](../adr/0046-the-project-is-a-remake-that-retells-the-game.md)). Under it is a port of
the original's engine, running the original's content as the player's ROM has it, never changed (the ROM plugin, `aidyn.rom`); what Retold
changes is laid over that, in the code and in the ACRE plugin, `acre`, the remade game ([ADR 0045](../adr/0045-the-remade-game-is-the-acre-plugin.md)).
The original is where it starts, not a limit:

- **Bugs are fixed outright**, each the owner's choice, with no switch to bring them back. A bug that's part of how the game plays may be
  kept, also one at a time ([ADR 0018](../adr/0018-the-port-fixes-bugs-and-sets-enhancements.md)).
- **Changes are the remake's design, not settings.** Settings are for taste and the machine: the volumes, Graphic Quality and its Advanced
  Graphic Settings, Menu Size, Text Speed and the controls.
- **Graphic Quality's Original is the one switch that shows the original's look**: the original's text, boxes, images, Party tab, tabs and
  System list, and the original's detail. It leaves the screen's settings, the options pages and the layouts of the World tab, the HUD and
  the conversation box as they are.

## The files

| File | What it covers |
| --- | --- |
| [mechanics](mechanics.md) | Time and timing, the random streams, movement, the flags and the journal's timing |
| [combat](combat.md) | Battles: their flow, the AI, the battle screen's behaviour |
| [magic](magic.md) | Spells and how effects land and last |
| [characters](characters.md) | Stats, skills, the party, joining and leaving, Alaron's name |
| [items](items.md) | Potions, flasks, scrolls, key items, equipping and the inventory's rules |
| [weapons](weapons.md) | Weapons |
| [armor](armor.md) | Body armor, Alaron's Squire Armor among it |
| [shields](shields.md) | Shields |
| [story](story.md) | Scripts, conversations, cut-scenes, cinematics, the journal and the text |
| [world](world.md) | The maps, map objects, wanderers, camping and the weather as it plays |
| [graphics](graphics.md) | Rendering, models, textures, the sky, particles and the art |
| [ui](ui.md) | The menus, the HUD, the conversation box, the battle screen and the fonts |
| [audio](audio.md) | Music, sound and the text's voice |
| [controls and camera](controls-and-camera.md) | The controls, the mouse and the cameras |
| [options](options.md) | Every setting, with its default |
| [saves](saves.md) | The save format, what's saved and how loading goes |
| [platform](platform.md) | The PC, the ROM's import, plugins and mods, the editor and the packages |
| [bug fixes](bug-fixes.md) | The original's bugs Retold fixes, by area |
| [kept on purpose](kept-on-purpose.md) | The original's bugs and oddities Retold keeps, so they aren't taken for the port's |

A bug fix is listed in [bug fixes](bug-fixes.md) alone, under its area, and each category's file points there. A change to how something
looks is in [graphics](graphics.md) or [ui](ui.md), whatever it's of: a potion's picture is in graphics, what it does in items.

## What isn't here

- **What the port doesn't do yet.** A few of the original's behaviours aren't ported or don't match yet, such as the lit lighthouse.
  They're gaps rather than changes, and the [roadmap](../roadmap.md#left-over-from-finished-work)
  lists them.
- **Changes to the story's content.** The retelling's changes and additions to the story are to come; [story](story.md) has what's changed
  so far, which is how the scripts run.

Keep these files up to date: a change made to the game goes in its file here as well as in its system's doc.
