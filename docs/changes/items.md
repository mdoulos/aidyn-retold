# Items

Potions, flasks, scrolls, key items, equipping and the inventory's rules. What items look like (each potion's and scroll's own picture) is
in [graphics](graphics.md#the-art), the inventory's grid and cards in [ui](ui.md#the-party-tab), weapons, armor and shields in their own
files, and the item bugs Retold fixes in [bug fixes](bug-fixes.md#items).

## Potions

The ACRE plugin's potions, the user's (Nov 4, 2026; [items](../systems/items.md#potions), [magic](../systems/magic.md#potions-and-flasks)):

| Potion | The original | Retold |
| --- | --- | --- |
| Literacy Potion | Clarity Potion: Loremaster +3, in or out of a battle | Renamed; Loremaster +3, outside a battle only |
| Charisma Potion | Diplomat +3, in or out of a battle | Diplomat +2 and Merchant +1, outside a battle only |
| Intellect Potion | none | New: Intelligence +10, in or out of a battle, 500 gold |
| Determination Potion | none | New: Willpower +10, in or out of a battle, 500 gold |
| Ingenuity Potion | none | New: Thief +1 and Mechanic +1, outside a battle only, 300 gold |

- **Each lasts the original's three hours**, the stat potions raising the stat's base as Strength's and Dexterity's do.
- **The new ones are brewed and sold.** An Alchemist brews Intellect from 1 Herb, 2 Spices and 1 Gemstone at rank 2, Determination from
  1 Herb and 3 Gemstones at rank 3, and Ingenuity from 2 Herbs and 1 Spice at rank 2. Intellect and Determination are sold where the
  Dexterity Potion is, and Ingenuity where the Charisma Potion is.
- **The potions drunk only outside a battle outlast one**, as the original's Stealth Potion does: Literacy, Charisma and Ingenuity with it.
  The original's Clarity and Charisma come off as a battle ends.
- **Nine potions can work at once**, one of each that lasts; the original has room for seven, which held its six.
- **The Charisma Potion's description names its recipe right**: 2 Gemstones, as its recipe takes, where the original's says 1.
- **A potion's effect lasts through a save and a load.** The original's save keeps the party's sheets without their effects, so a
  Strength or Clarity potion drunk before saving is gone after loading. Retold saves each working potion with its power and the time it has
  left, and puts it back on as the game loads, the user's choice ([saves](../systems/saves.md#the-format)).
- **The Defence potion and the Acid flask's corrosion change the sheet's protection,** as a shield does, where the original adds them as the
  total is worked out. A corroded target's total stops at 0, so it isn't hit for 1 every blow; the totals are the original's but past 255
  ([magic](../systems/magic.md#where-the-port-differs)).
- **A potion is data.** Each potion's effects and how it's used, drunk in trek, in a battle or thrown, are its record's, so a mod's potion
  works as the original's do ([ADR 0021](../adr/0021-instant-effects-land-in-one-place.md), [magic](../systems/magic.md#potions-and-flasks)).

## Food and cooking

New, the user's design (Nov 4, 2026; [items](../systems/items.md#cooking-and-food)): the original has no food.

- **Food heals and restores Stamina**, eaten from the pause menu, or from the battle's C-up menu, which takes the turn, as a potion is drunk. The best dishes stand in for the Curing Potion in a battle.
- **Ingredients:** Meat, Fish, Potato, Carrot, Apple, Egg, Cured Bacon and the Bag of Oats, sold in a general store in each town; beasts drop Meat. Carrot and Apple can be eaten raw, for a little Stamina. Bread is sold there too, a finished dish no one cooks.
- **Sixteen dishes**, from Cooked Apple to the pies, cooked by the new Cook skill ([characters](characters.md#the-cook-skill)), several to an effect from different ingredients, up to Cook 6, a rank above only failing less. A failed cook makes the recipe's own burnt dish, which still feeds.
- **Herby, Spicy and Hearty versions** of the eight meals of two and three ingredients, which heal or restore Stamina more; no recipe makes them yet.
- **In a battle** only the snacks, Bread, and the lighter dishes (the cooked or burnt apple, carrot, meat and fish, the fish cakes and the bacon) can be eaten; the rest are too filling.
- **Eating anything feeds the eater**, against hunger ([characters](characters.md#hunger)): a snack, bread or a burnt dish a point, a dish two, a dish of several ingredients three. Well Fed, nothing more goes down until time brings it down.

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

- **The Intellect, Determination and Ingenuity Potions** ([above](#potions)).
- **Five ingredients, fourteen dishes and the Burnt Meal** ([above](#food-and-cooking)).
- **Squire Armor**, Alaron's starting armor ([armor](armor.md#squire-armor)).
