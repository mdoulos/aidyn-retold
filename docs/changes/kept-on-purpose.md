# Kept on purpose

The original's bugs and oddities Retold keeps, the owner's choice, one at a time: they're part of how the game plays, or fixing them
waits for its content to change. They're listed so they aren't taken for the port's own. Most of the original's rules are kept as they
are, and aren't listed: only what looks like a mistake is.

## Combat

- **A flask's blast distance is kept in a byte**, so it wraps at 256 and a combatant far off can be caught, and friendly fire happens. The
  target takes its share twice, the blast's and the blow's; Retold shows both ([combat](../systems/combat.md#where-the-port-differs)).
- **Lowering an enemy's stats can leave it unable to move** (the repository's [README](../../README.md#accuracy)).
- **In the Shadow's battle**, Heal With Herbs loses its herb before it's refused, and a potion drunk there is used up for nothing
  ([combat](../systems/combat.md#the-special-battles)).
- **A Fireball that would catch more of its own side scores above everything** for the AI, as a negative count wraps
  ([the AI](../systems/combat.md#the-ai)).
- **Enemies buff ahead**, and **get stuck or rest for no reason you can see**, as their planner sees the grid alone
  ([the AI](../systems/combat.md#the-ai)).

## Magic

- **Spirit and Starlight Shield leave protection behind**: 2 a level on, 1 a level off as they end, so each cast leaves a level until a load
  rebuilds the sheet ([magic](../systems/magic.md#the-originals-quirks-the-port-keeps)).
- **Wind on an ally raises Stamina and its maximum for good**, until a load ([magic](../systems/magic.md#the-originals-quirks-the-port-keeps)).
- **A charm or a hold that the limit stops still hurts**, (margin) d6 ([magic](../systems/magic.md#the-originals-quirks-the-port-keeps)).
- **The dead keep their buffs**, as lowering a stat does nothing to the dead
  ([magic](../systems/magic.md#the-originals-quirks-the-port-keeps)).

## Story and quests

- **Rewards that never come.** The flags for the party's best stats are never written, so the secret teleporter in EhudUnder M05 (Mechanic
  above 5) and three dialogues in EhudUnder G03 (Loremaster above 4 or 5) never work ([flags](../systems/flags.md#what-the-engine-writes)).
- **Trahern's conversation (script 936) loops** once flags 67 and 84 are set; nothing sets 84 ([scripts](../systems/scripts.md#faults)).
- **Twelve rumour scripts give their first rumour twice as often** as each other, as they end on a blank node that shows the first's text
  ([scripts](../systems/scripts.md#ops-that-need-more-than-a-flag)).
- **A beaten monster party with no encounter flag comes back** after three chunk loads or teleports: 249 of the 1,079
  ([saves](../systems/saves.md#map-objects-memory)).
- **A script's battle doesn't set its encounter flag when won** ([hardcoded content](../hardcoded-content.md#findings)).
- **Most of the map items are never given**: only Map 2 and Map 7 have a source, and the Skull Key and the Map to the Goblin Lair have none
  ([hardcoded content](../hardcoded-content.md#findings)).
- **A script asking for an item misses gear worn past a gap** in the slots ([items](../systems/items.md#the-inventory)).

## Cut-scenes

- **Pochanargat, with no walk, jumps to her point** five seconds on; **Shamsuk stands, then jumps onto his point** if his tower scene is
  moved on within about a second and a half; **Phelan has only a one-legged model** ([scripts](../systems/scripts.md#actors)).
- **Back at Port Saiid from the Namer's Isle, Alaron arrives about 10 up and falls**, out of the console's 4:3 view; a wide window shows
  it ([scripts](../systems/scripts.md#actors)).
- **A cut-scene's point-of-view cut hides the actor but not its shadow** ([cameras](../systems/cameras.md#cut-scenes-cameras)).
- **The ending's last two captions never show** ([cinematics](../systems/cinematics.md#captions)).

## World

- **The player can still wedge in 38 of the original's 41 places** that hold him, crevices mostly ([movement](../systems/movement.md#wedged-against-a-leaning-plain-wall)).
- **The four terrain flags wrap** for the 28 terrains ([flags](../systems/flags.md#what-the-engine-writes)).
- **Rain rolled elsewhere can fall in the mountains** ([weather](../systems/weather.md#the-roll)).
- **Dropped items are lost**, and one worth a gold or less can't be dropped ([ui](../systems/ui.md#the-loot-window)).

## Graphics

- **A mirrored model is drawn inside out**, as the Shadow is in its merge ([rendering](../systems/rendering.md#mirrored-models)).
- **Only the party's bodies take the map's lights**, so wanderers don't, and the sun, the moon and the point lights light every other
  vertex of what they light ([rendering](../systems/rendering.md#bodies-light), [rendering](../systems/rendering.md#point-lights)).
- **The cracks between meshes stay**, as faint lines; whether to weld them is the owner's to choose later
  ([rendering](../systems/rendering.md#the-n64-material)).
- **The moon is flat white discs**, with no surface texture ([playtests](../playtests.md)).

## Audio

- **No reverb, and no voice to spare**: past 32 sequences, or with no free voice, a note is dropped
  ([audio](../systems/audio.md#sound-effects)).
- **Notes above 65,535 Hz wrap and play too low** in two pieces of music and some sounds ([user checks](../user-checks.md#sound)).
- **Silent things stay silent**: conversation choices, signs, the loot window's questions, spell visuals, arrows, flasks, a level up and a
  victory. A weapon sounds on a hit or a miss alike, a shield's block is a dodge, and the Party tab's list plays its sound twice
  ([audio](../systems/audio.md#sound-effects), [audio](../systems/audio.md#bodies-sounds)).

## Cameras

- **The conversation camera always frames from one side**, the user's choice to keep for now ([cameras](../systems/cameras.md#the-conversation-camera)).
- **An interior's camera arrives in front of the player, looking back** ([cameras](../systems/cameras.md#which-mode)).
