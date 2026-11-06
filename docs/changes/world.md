# World

The maps, the map objects, the wanderers, camping and the weather as it plays. What the weather looks like is in
[graphics](graphics.md#weather), and the world's bugs Retold fixes in [bug fixes](bug-fixes.md#world).

## The maps

- **The Roog Cave's mushroom no longer blocks its passage**, moved by the ACRE plugin ([bug fixes](bug-fixes.md#world)).
- **The world can be drawn farther**, the Overworld out to 5 × 5 chunks; the outer ones are scenery alone, with nothing in them rolled,
  written or aged ([graphics](graphics.md#draw-distance)).
- **A teleporter with no arrival point sets the player down at its own height**, where the original sweeps from near his height before
  ([map objects](../systems/map-objects.md#teleporters)).
- **The new game's spawn.** A rock stands in a hole in the ground in front of the spawn; the original drops the player from 10 up, and
  Retold sets him on the highest ground under it ([runtime](../systems/runtime.md#stand-ins)).

## Wanderers

- **There's no cap on wanderers.** The original has room for 39 and a new game's 3 × 3 around Overworld H13 holds 44; Retold gives every
  party a body, as mods add their own ([wanderers](../systems/wanderers.md#bodies-as-their-chunk-loads)).
- **An ambush's line of sight is checked across the loaded world**, not the wanderer's own chunk alone
  ([wanderers](../systems/wanderers.md#wandering-noticing-and-chasing)).

## Camping

- **A camp ambush with no enemies to find**, on terrain 27 (Overworld N25, O24 to O26, P24 and P25), where the original reads another
  table's data as enemies, says "The party is ambushed, but no one comes." and goes back to trek, with no battle, no heal and no time
  passing. It stands in until what the original does is settled ([camping](../systems/camping.md#the-ambushs-enemies)).
- **Camping's rules are data** (`aidyn:camping/0`), for mods ([camping](../systems/camping.md#in-the-port)).
- **The party sleeps through a camp**, which ends Tired and Overtired ([camping](../systems/camping.md#in-the-port), [characters](characters.md)).
- **Food spoils through the camp's hours**, and when trek shows again a toast says what was tossed ([camping](../systems/camping.md#in-the-port), [items](items.md)).

## Weather

- **The weather carries on through cut-scenes, doors, the help menu, loads and battles**, rather than starting over each time
  ([bug fixes](bug-fixes.md#world)).
- **A save keeps all of the weather**, so a loaded game looks as it did ([saves](saves.md)).
- **Rain lands on what's drawn** ([bug fixes](bug-fixes.md#graphics)).
