# Bug fixes

The original's bugs Retold fixes, each the owner's choice, with no switch to bring them back
([ADR 0018](../adr/0018-the-port-fixes-bugs-and-sets-enhancements.md)). The bugs it keeps on purpose are in
[kept on purpose](kept-on-purpose.md).

## Mechanics

- **Falling out of the world.** The original lifts the player to 50 and sets him down from on high where he was, which can go on for ever.
  Retold sets him down where there's ground above −50, or else on the newest place he stood that it hasn't tried
  ([movement](../systems/movement.md#movement-and-collision)).
- **Wedged in place.** Held falling against a leaning wall, the original's player can be stuck; Retold's steers again after a quarter of a
  second ([movement](../systems/movement.md#wedged-against-a-leaning-plain-wall)).

## Combat

- **Line of sight runs head to head.** The original lifts the attacker's point by both bodies' heads and aims it at the target's middle
  ([combat](../systems/combat.md#where-the-port-differs)).
- **Escape doesn't hang the battle.** In the original, a member taken off the field by Escape leaves the battle waiting for that body for
  ever. Retold goes on, and a party with no one left to fight has fled. Only cheating reaches the spell
  ([combat](../systems/combat.md#where-the-port-differs)).
- **The Goblin Ambush's end puts Alaron's Strength back to its base.** The original adds the base to what he has, doubling it if the battle
  ended before the ambush's second blow ([hardcoded content](../hardcoded-content.md#the-goblin-ambush-encounter-flag-978)).
- **A large caster casts.** The original's two-cell casters, Kitarak's Hobgoblins among them, score a cast from where they stand and then rest every turn, their own footprint blocking the walk the cast is checked by. Retold lets a caster's own footprint be ([combat](../systems/combat.md#where-the-port-differs)).
- **A script's battle gives no bonus loot.** The original's can pay out the last monster party's bonus loot again, as a value is never
  cleared ([hardcoded content](../hardcoded-content.md#findings)).
- **The Shadow's battle says why healing fails.** The original replaces "The Shadow's presence prevents all healing." with "fails to
  perform healing" in the same frame ([combat](../systems/combat.md#where-the-port-differs)).
- **A lowered stat never freezes a combatant.** The original stops a combatant acting while its Intelligence, Willpower, Dexterity or
  Strength is 0, so Stupidity, Clumsiness, Debilitation or Wraith Touch's drain could hold one for the rest of the battle. Those four
  now never read below 1 ([magic](../systems/magic.md#where-the-port-differs)).
- **The dim red tint shows only while a spell or an area is aimed.** The original's shows on everyone a selected spell would reach from the
  turn's first frame ([ui](../systems/ui.md#the-portraits)).
- **Hunters notice for themselves.** In the original, when one wanderer notices the party, every later one in the list chases that frame,
  so every enemy around can chase and ambush at once ([wanderers](../systems/wanderers.md#wandering-noticing-and-chasing)).
- **What can't be reached in play**: a cast past the visuals' pools shows nothing rather than reading past them; a bystander in the first
  four enemy slots is left out rather than leaving an empty slot counted as an enemy; a fleeing enemy that has just left stops; a walk the
  AI gives up is forgotten at the turn's end; the Fireball's aim stops at cell 127 rather than looping for ever
  ([combat](../systems/combat.md#where-the-port-differs), [the AI](../systems/combat.md#the-ai)).

## Magic

- **The Stamina spell raises Stamina,** by 2 a level while it lasts, as its description says. The original's lowers it by 2 a level and
  takes 2 a level more as it ends, so each cast leaves the caster's Stamina 4 a level lower until a load
  ([magic](../systems/magic.md#where-the-port-differs)).
- **Detect Moon Phase and Detect Sun Phase in a battle** do nothing, where the original's write through a null pointer
  ([combat](../systems/combat.md#where-the-port-differs)).

## Items

- **The last flask of a stack flies and bursts.** The original empties the hand as the last one's thrown, so it never flies, never bursts,
  and its target dodges ([combat](../systems/combat.md#where-the-port-differs)).
- **The Archmage Staff can't be swapped away.** In the original its wielder can equip another weapon, and the staff vanishes with its
  bonuses still applied ([items](../systems/items.md#equipment)).

## Characters

- **A member who's in the party already isn't added again.** The original adds a second sheet; only Sholeh's first meeting in Ugarit
  could do it ([characters](../systems/characters.md#joining-and-leaving)).

## Story

- **Script 1040's harbour reunion doesn't softlock.** With Dougal in the party, the original stops on an empty box with no portrait at node
  24, which lost its link to node 25. Retold ends the scene; the reunion and Keelin's joining there don't play until the link is put back
  in the ACRE plugin ([scripts](../systems/scripts.md#faults)).
- **Talkers whose scenes name a point their chunk lacks** (Edur in Overworld I08, Keelin in Terminor, and seven others) skip the move,
  where the original reads the wrong block and crashes: Keelin's scene, talked to before she's recruited
  ([scripts](../systems/scripts.md#the-world-it-plays-in)).
- **A script that would crash ends.** Where 1.0 crashes, on a thirteenth enemy, command 49, a missing random-line script, a menu with no
  text or a walk of 10,000 nodes, Retold ends the script and notes it ([scripts](../systems/scripts.md#faults)).
- **A shopkeeper or trainer with nothing to offer says so.** The original crashes for a shop and opens empty lists for training
  ([ui](../systems/ui.md#the-shop), [ui](../systems/ui.md#training)).

## World

- **The Roog Cave's mushroom.** In D01 the original's mushroom (`aidyn:voxel/0x1205/12`) stands in the neck of a passage, and its
  collision box leaves less room than Alaron needs: walking or sneaking he can't get past, and running he wedges. The ACRE plugin slides it
  0.71 out of the neck, its box with it; the ROM plugin is as the ROM has it, and a mod can put it back
  ([hardcoded content](../hardcoded-content.md#corrections-to-the-roms-maps)).
- **The Jundar gate opens after the riddle.** Overworld O24 holds plain copies of the gate's four barriers, which the original never
  changes from P24; Retold changes them with the gate's ([map objects](../systems/map-objects.md#triggers)).
- **The weather carries on.** The original rebuilds the world for a cut-scene, a door's backdrop, the help menu, a load and a battle's
  arena, and each time its drawn weather starts again: the rain stops, the sky drops back and greys again over ten seconds, and the rain's
  loop starts over. Retold carries what's falling, how hard, the fog, the rain's sky and its loop through them all
  ([weather](../systems/weather.md#trek-built-again)).
- **Wanderers in another chunk are measured right.** The original mixes up chunk coordinates when it measures how far a wanderer in another
  chunk is from the player ([wanderers](../systems/wanderers.md#wandering-noticing-and-chasing)).

## Graphics

- **Rain and snow land on what's drawn.** The original's land on the player's chunk's collision alone, so splashes float on invisible
  walls and over the sea's edge, and none land in the next chunk. Retold's land on what all nine chunks draw: solid things splash, and
  water, lava and see-through things take the drops ([weather](../systems/weather.md#what-the-ports-lands-on)).

## UI

- **The sundial and compass stay hidden in caves after loading.** The original decides only as the player crosses between skies, so after
  loading inside a cave they show until he leaves and comes back ([ui](../systems/ui.md#the-sundial)).
- **The details print each minimum under its label.** The original prints Min. Intelligence's under Min. Strength's and the other way round
  ([ui](../systems/ui.md#the-party-tab)).
- **The Healer's popup rows are 28 apart**, where the original's 25 lays the next name over the health line
  ([ui](../systems/ui.md#the-party-tabs-actions)).
- **Renaming Alaron.** The original leaves the last letter passed over in the slot, which OK keeps; Retold's slot shows a letter only until
  A types it ([ui](../systems/ui.md#renaming-alaron)).

## Audio

- **A body's voice past 15 is silent.** The original's loudness wraps, so a body 15 to 50 away is loud again. Retold silences it outside
  battles, as Aidyn ReChronicled does, and keeps the wrap in a battle ([audio](../systems/audio.md#bodies-sounds)).

## Controls and camera

- **The camera keeps off walls.** The original's outdoor camera can end up behind a wall nearer than about 3. Retold sweeps a sphere from him
  to it and keeps it 0.3 off what it meets, as Aidyn ReChronicled does ([cameras](../systems/cameras.md#the-follow-camera-cameramode0)).
- **The camera's mode follows the chunk however the game starts.** Loaded from the title, the original keeps the outdoor camera inside a
  house until a camera object takes over ([cameras](../systems/cameras.md#which-mode)).
- **A camera object takes over without a jolt.** The original's tracking camera snaps its aim onto the player, 4° to 8° in a tick; Retold's
  snaps only on a cut ([cameras](../systems/cameras.md#camera-objects-processvoxelcamera)).

## Saves

- **A save naming what the plugins don't have still loads**, listing what it couldn't use, where the original checks only its checksum
  ([saves](saves.md#loading)).
