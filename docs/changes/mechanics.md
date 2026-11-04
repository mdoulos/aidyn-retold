# Mechanics

The general rules: time, timing, chance, movement and the flags. Combat's, magic's and the characters' are in their own files; the
original's bugs fixed here are in [bug fixes](bug-fixes.md#mechanics).

## Time and timing

- **The rules step every tick, at a steady pace.** The original acts once per drawn frame, at the console's 15 to 25 frames a second, so
  how fast things happen follows the frame rate. Retold steps the rules 60 times a second and matches what the original does per frame to
  an ideal 30 fps frame ([ADR 0017](../adr/0017-the-core-steps-every-tick.md), [runtime](../systems/runtime.md#traps)). A few effects are
  timed to the console's average of 20 fps instead: scrolling textures and rain and snow spawning
  ([rendering](../systems/rendering.md#scrolling-textures), [weather](../systems/weather.md#rain-and-snow)). The camera moves a little
  faster than on the console as a result.
- **The frame rate is the machine's.** The game draws at any frame rate, with no upper limit, interpolating between ticks; it can be
  limited to 30 but not to the console's 15 to 20 ([options](options.md#video)).
- **Effects age every tick of play** rather than every frame, though what they do each round still runs once a 30 fps frame, and not while
  the pause menu is open. The clock itself is the original's: 72 times real time, a day in 20 minutes of play
  ([magic](../systems/magic.md#where-the-port-differs)).

## Chance

- **Separate random streams.** The original draws everything from one random stream, so a cosmetic effect or the frame rate can change
  what a battle rolls next. Retold keeps the original's generator but gives each part its own stream (combat, loot, the world, the
  wanderers, the cosmetic), so a raindrop never moves a battle's outcome. A new game seeds them as the original does
  ([ADR 0006](../adr/0006-tick-model-and-rng.md)). Loot is rolled on the loot stream rather than the battle's
  ([combat](../systems/combat.md#where-the-port-differs)); the weather's rolls on the world's, its wind and lightning on the cosmetic
  ([weather](../systems/weather.md#differences-from-the-original)).

## Moving

- **Jump.** The original has no jump outside its `!flea` cheat. Retold's player jumps (Space, and any controller button bound to it) with the
  cheat's strength, from the ground only, a press up to 6 ticks before landing kept, and climbs a ledge up to about 1.5. It's always on; a mod's map
  can forbid it (`noJump`) ([movement](../systems/movement.md#movement-and-collision)).
- **Full run.** A third gait past the run: Shift, or a click of the left stick, asks the actor for request 26. Alaron's remodel has one, 50%
  faster than the original's run; an actor without one runs as before. A battle doesn't use it
  ([movement](../systems/movement.md#movement-and-collision), [replacing graphics](../replacing-graphics.md#a-skinned-character)).
- **Alaron's gaits are faster.** His remodel walks at 125% of the original's 1.43 a second and jogs at 111% of its run, 4.73
  ([replacing graphics](../replacing-graphics.md#a-skinned-character)).
- **Bodies turn faster.** The original turns a body a fixed step a frame, so at the console's 15 to 20 fps a body turns 300 to 400° a
  second; Retold's turn 600° a second, the 30 fps frame's. The user judged it no worse
  ([movement](../systems/movement.md#movement-and-collision)).
- **Collision across the loaded chunks.** The original collides, sets bodies down and finds trigger objects in the player's own chunk;
  Retold uses every loaded chunk, each triangle its own chunk's ([map objects](../systems/map-objects.md#a-chunks-objects-at-run-time),
  [movement](../systems/movement.md#setting-a-body-down)).

## Flags and the journal

- **A new journal entry is told at once.** In the original the notice of a new entry waits until something reads a derived flag, so some
  wait for a door or a border (Kitarak's death, the letter, Captain Amann's offer). Retold brings the flags up to date first, so every entry
  is told on trek's first free tick, the user's choice ([flags](../systems/flags.md#when-the-network-comes-up-to-date)).
- **The journal lists entries in the order they were found**, not the table's order, and a save keeps that order
  ([saves](../systems/saves.md#the-format)).
- **A mod's flags can't hang the game.** A flag no loaded plugin has reads as clear and takes no writes, and a mod's derived flag that
  loops is cut off after a number of passes ([flags](../systems/flags.md#the-network)).
