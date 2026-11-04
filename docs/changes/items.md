# Items

Potions, flasks, scrolls, key items, equipping and the inventory's rules. What items look like (each potion's and scroll's own picture) is
in [graphics](graphics.md#the-art), the inventory's grid and cards in [ui](ui.md#the-party-tab), weapons, armor and shields in their own
files, and the item bugs Retold fixes in [bug fixes](bug-fixes.md#items).

## Potions

- **A potion's effect lasts through a save and a load.** The original's save keeps the party's sheets without their effects, so a
  Strength or Clarity potion drunk before saving is gone after loading. Retold saves each working potion with its power and the time it has
  left, and puts it back on as the game loads, the user's choice ([saves](../systems/saves.md#the-format)).
- **The Defence potion and the Acid flask's corrosion change the sheet's protection,** as a shield does, where the original adds them as the
  total is worked out. A corroded target's total stops at 0, so it isn't hit for 1 every blow; the totals are the original's but past 255
  ([magic](../systems/magic.md#where-the-port-differs)).
- **A potion is data.** Each potion's effects and how it's used, drunk in trek, in a battle or thrown, are its record's, so a mod's potion
  works as the original's do ([ADR 0021](../adr/0021-instant-effects-land-in-one-place.md), [magic](../systems/magic.md#potions-and-flasks)).

## Flasks

- **A flask's damage shows in full.** The original's target takes the blast's share and then the blow's again, but its number and line show
  one share: a Fire flask of 10 takes a rat from 31 to 11 and says 10. Retold keeps the damage and shows both shares, Sleep Gas's Stamina
  too ([combat](../systems/combat.md#where-the-port-differs)). The last flask of a stack now flies ([bug fixes](bug-fixes.md#items)).

## Equipping

- **A ring or a scroll picked for a slot replaces the slot's own**, from the Party tab's silhouette; the original only ever adds them
  ([ui](../systems/ui.md#the-ports-layout)).
- **Taking something off with no room in the inventory says so** ("No room in inventory.") and leaves it on, where the original says
  nothing ([ui](../systems/ui.md#the-party-tabs-actions)).

## New items

- **Squire Armor**, Alaron's starting armor ([armor](armor.md#squire-armor)). No other new item yet.
