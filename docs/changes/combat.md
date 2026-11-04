# Combat

How a battle goes, and how the battle screen behaves. Spells are in [magic](magic.md), flasks and potions in [items](items.md), the battle
screen's look in [ui](ui.md#the-battle-screen), and the original's combat bugs Retold fixes in [bug fixes](bug-fixes.md#combat).

## Battles

- **Spell auras can show at once** (option). The original's lasting spell shells and rings over the combatants' heads take turns, one at a
  time across the whole battle. Retold can show each character's at once, each over its own head with its own pulse; the five seconds after
  a cast stay. Setting: Enhancements, Status Auras: Original (the default) or More Often
  ([combat](../systems/combat.md#where-the-port-differs), [what spells look like](../systems/combat.md#what-spells-look-like)).
- **Aiming moves at the console's pace.** The flask's circle and an area spell's cursor move a step every 4 ticks, as the console's slower
  frames move them, rather than at the port's usual 30 fps, at which the user found them twice as fast as the console's and hard to steer
  ([combat](../systems/combat.md#where-the-port-differs)).
- **Holding the stick or the D-pad repeats** in an aim and the battle's menus, as the runtime's other menus do; the original's aim steps
  once a press ([combat](../systems/combat.md#where-the-port-differs)).
- **Previous and Next Target steer an attack's or a spell's target**, not only a bow's or a thrown weapon's ([ui](../systems/ui.md#controls)).
- **Confirming a placed area with no one in it goes back to the turn**, as confirming a spell with no one selected does
  ([combat](../systems/combat.md#where-the-port-differs)).
- **A win opens the post-battle windows on the member last shown**, or on the member whose turn the last pause was on
  ([combat](../systems/combat.md#where-the-port-differs)).
- **A conversation with no one in the party to talk starts the script's battle**; the original's does nothing. Only a mod reaches it
  ([combat](../systems/combat.md#in-and-out)).
- **Timing is counted per tick.** The camera and the action timers act every tick and the spell visuals step every tick, so a blow that
  lands on an odd frame lands a tick sooner (a goblin's Sabre at 81 rather than 82); the same at 30 fps
  ([combat](../systems/combat.md#where-the-port-differs)).
- **The ring starts at the same angle each time.** The original's starts a hair further round with each layout, by float error
  ([combat](../systems/combat.md#where-the-port-differs)).

## Not ported

Small things the original does that Retold leaves out on purpose ([combat](../systems/combat.md#where-the-port-differs),
[what the bodies hold](../systems/combat.md#what-the-bodies-hold)):

- the half-transparent shadows of a few entities (every shadow is drawn whole);
- not animating idle bodies far from the camera (every body but the held and the fallen is stepped);
- the `!bigw` and `!balloon` cheats, which double what bodies hold and their heads;
- an arrow, a flask or a thrown weapon leaves from the attacker's middle when its actor has no hand locator.

## Development aids

Debug builds only, not in a release: `--battle <monster party>[,<arena>][,ambush]` starts a battle, F3 shows the battle's state, F10 and
F11 win or lose at the next turn's change, and B held two seconds on a party member's turn wins, but not against the Shadow
([combat](../systems/combat.md#the-port), [runtime](../systems/runtime.md#controls)).
