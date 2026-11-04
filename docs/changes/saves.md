# Saves

The save format, what a save keeps and how a game loads. The save and load screens are in [ui](ui.md#the-system-tab), and the bugs fixed
in [bug fixes](bug-fixes.md#saves).

## The format

- **Files, not a Controller Pak.** The original saves up to 16 notes of 7 KB on the Controller Pak. Retold saves JSON files in the data
  folder's `saves`, as many as the player likes, newest first, which can be read and edited by hand. Each names its plugins and the
  version it was made by ([saves](../systems/saves.md#the-format)).
- **Quick save**, F8, to `quick.json` ([runtime](../systems/runtime.md#what-it-draws)).
- **A screenshot beside each save**, 208 × 156, in place of the original's 64 × 32 picture ([saves](../systems/saves.md#the-format)).
- **The original's saves can be read and written** with the `acre save pak`, `from-pak` and `to-pak` commands, for the original's
  Controller Pak in an emulator, a mod's items and flags left out ([saves](../systems/saves.md#making-and-checking-saves)).

## What's kept

As the original's, and also ([saves](../systems/saves.md#the-format)):

- **potions' effects**, with their power and the time left ([items](items.md#potions));
- **the weather**: its class, the wind and what's drawn, where the original's keeps what falls and how hard, and its class is stale after a
  load ([weather](../systems/weather.md#differences-from-the-original));
- **the journal's order** ([mechanics](mechanics.md#flags-and-the-journal));
- **Alaron's name**, and each member's True Name ([characters](characters.md));
- **every random stream** and the minimaps' pieces seen.

The volumes and the resolution aren't saved: they're the player's settings ([options](options.md)).

## Loading

- **A record a save names that the plugins don't have is listed and passed over**, and gear the rules refuse is left off, where the
  original checks only the save's checksum, so a bad one breaks it ([saves](../systems/saves.md#loading-as-the-original-does)).
- **A stray spell is kept**, last in the spellbook, where the original drops a spell of another school
  ([saves](../systems/saves.md#loading-as-the-original-does)).
- **Gear fills the first free slots**, so gaps close ([saves](../systems/saves.md#loading-as-the-original-does)).
- **A save with no place starts as a new game**, with the opening ([saves](../systems/saves.md#the-format)).
- **The pause menu starts on Alaron** after a load; the original keeps the member it was last on ([ui](../systems/ui.md#opening-and-closing)).
