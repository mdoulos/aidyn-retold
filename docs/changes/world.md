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
- **Camping is a screen**, the party round the fire in a painted campsite with what it can do before it sleeps, cooking, brewing, making armor, and resting, where the original cuts straight to its camp scene. The terrain check, the ambush and the story's eight camp scenes stay as they were ([camping](../systems/camping.md#the-camp-screen)).
- **Camping doesn't heal.** The original's camp gives back half of each member's hit points; Retold's party sleeps, and its effects and food age through the night, but its hit points stay as they were. Wands and the like still refill by half, as the original's do. An inn's rest, a script's, still heals fully ([camping](../systems/camping.md#in-the-port)).
- **A night with no story scene plays nothing.** The original falls back to Alaron alone at the fire, "pass the marshmallows" (the Campsite map's scene); Retold's camp has its talks and its screen instead ([camping](../systems/camping.md#in-the-port), [story](story.md#cut-scenes)).
- **The party chooses how long it rests**: Quick Rest, an hour, or Long Rest, until morning, noon, evening or midnight, waking as it comes, where the original always sleeps eight hours. What's made at camp doesn't change it. A story camp scene still passes its own eight hours ([camping](../systems/camping.md#the-camp-screen)).
- **The party talks at camp**: a conversation chosen for the night from those whose members are there and whose story flags hold, over the camp screen, on the nights the original's story scenes leave empty ([camping](../systems/camping.md#the-camp-screen)).
- **Breaking camp is allowed**: B on the camp screen goes back to trek with nothing done, where the original always camps once it's chosen ([camping](../systems/camping.md#the-camp-screen)).
- **The game knows where the party is**: places name the chunks, the world map's regions and the map sets, "Gwernia" the moment the party steps into its streets and "Oisin Forest" on the road to Erromon, where the original only rings the world map ([places](../systems/places.md)).
- **The camp has a living fire**: the user's campfire, its stones, logs, tripod and pot, laid on a blank forest at the fire's point the user marked, flames that flicker and sway on its glowing logs, rising from behind its logs and front stones, some rising behind the pot and some in front of it, faint smoke from the pot, small embers rising now and then, some higher than others, a gentle wind that turns every 10 to 20 seconds to the left, to still or to the right and leans the flames, the smoke and the embers its way, firelight that ebbs on the ground and the party, and the fire crackling while the camp shows ([camping](../systems/camping.md#the-camp-screen)).
- **The camp's menu is a band of the pause menu's scroll**: after the talk the picture pans up and a strip of parchment unrolls between two rollers, with five tabs on slips down its left, Camp, Food, Potions, Weapons and Armor; the Camp tab shows each member's name, level, portrait, the Party tab's hit points bar and their statuses stacked in badges, with the location, Quick Rest and Long Rest along the foot; the band rolls up before a rest, Break camp or a talk ([camping](../systems/camping.md#the-camp-screen)).

## Weather

- **The weather carries on through cut-scenes, doors, the help menu, loads and battles**, rather than starting over each time
  ([bug fixes](bug-fixes.md#world)).
- **A save keeps all of the weather**, so a loaded game looks as it did ([saves](saves.md)).
- **Rain lands on what's drawn** ([bug fixes](bug-fixes.md#graphics)).
