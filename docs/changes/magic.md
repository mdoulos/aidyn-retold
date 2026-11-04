# Magic

Spells, and how every effect lands, lasts and ends. Potions and flasks are in [items](items.md), what the spells look like in
[graphics](graphics.md#combat), and the magic bugs Retold fixes in [bug fixes](bug-fixes.md#magic).

## Effects

- **One record can hold several effects.** The original's spells, potions and enchantments each do one kind of thing (Web of Starlight and
  Tap Stamina aside). Retold's records list effects from one vocabulary, so any spell, potion, flask or enchantment can carry any of them,
  for mods; the original game's records hold what they always did
  ([magic](../systems/magic.md#how-effects-land), [ADR 0020](../adr/0020-one-effect-vocabulary.md)).
- **Spells are found by their record, not their kind,** so a mod's new spell needs no kind of the original's, and the AI judges it as it
  does the original's ([magic](../systems/magic.md#a-spells-kind)).
- **The original's oddities are parameters.** Spirit and Starlight Shield leaving protection behind, and Wind on an ally raising Stamina
  for good, are kept as the original has them ([kept on purpose](kept-on-purpose.md#magic)), but a mod can change either
  (`ProtectionEffect.Leaves`, `StaminaEffect.RaisesMaximum`) ([magic](../systems/magic.md#the-originals-quirks-the-port-keeps)).

## Day and night in a battle

- **Darkness and Light hold the part of the day in the battle alone.** The original locks the world's part of the day, which also stops the
  weather rolling. Retold holds it in the battle only: the flags follow the clock, the clock steps each turn, and the weather keeps rolling
  ([magic](../systems/magic.md#where-the-port-differs), [flags](../systems/flags.md#what-the-engine-writes)).
- **Detect Moon Phase and Detect Sun Phase do nothing in a battle**, where the original's write through a null pointer. No one learns
  either in play ([combat](../systems/combat.md#where-the-port-differs)).

## Saved effects

- **A spell's effects still end with the battle** and aren't saved, as the original's; a potion's are saved now ([items](items.md#potions)).
