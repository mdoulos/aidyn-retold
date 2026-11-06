# Characters

Stats, skills, levels, training and the party. Their portraits, figures and the Party tab are in [ui](ui.md#the-party-tab), Alaron's
remodel in [graphics](graphics.md#models), his armor in [armor](armor.md), and the fixes in [bug fixes](bug-fixes.md#characters).

## Alaron

- **Alaron can be renamed from the options**, in a game: Options, Rename Character. The name is saved on his sheet, and every script's text
  that names him names the party's first member ([saves](../systems/saves.md#the-format), [scripts](../systems/scripts.md#its-boundary),
  [ui](../systems/ui.md#renaming-alaron)). Typing the name on a keyboard works, and the name entry's cheat codes aren't ported.
- **He starts in Squire Armor** in place of Leather Armor ([armor](armor.md#squire-armor)).
- **He looks and moves as his remodel** ([graphics](graphics.md#models), [mechanics](mechanics.md#moving)).

## The Cook skill

- **A thirteenth skill, Cook**, which cooks food from ingredients as the Alchemist brews potions, ranks 1 to 8 opening better dishes ([items](items.md#food-and-cooking), [characters](../systems/characters.md#the-cook-skill)). Anyone can learn it, every party member starts at 1, and it trains as cheaply as Diplomat.

## Hunger

- **Members tire**: a day and a half without sleep makes a member Tired, three days Overtired, which cost the same four stats as Hungry and Starving do, on top of them. Only camping or a rest brings it back, and entirely ([characters](../systems/characters.md#sleep)).
- **Starving and Overtired each cost a hit point a game hour** in trek, never below a tenth of the maximum: they weaken, and never kill ([characters](../systems/characters.md#hunger)).
- **Each member has a hidden satiety**, 1 to 12, in bands of three: Starving, Hungry, Fed and Well Fed. Food adds one to three points, time takes one off every eight game hours, and Well Fed, which shows, stops them eating until it falls.
- **Members grow hungry.** Two game days without eating anything, a member is Hungry, −2 to Intelligence, Willpower, Dexterity and Strength; three days, Starving, −5. Eating anything feeds them. Hit points aren't touched, and no stat goes below 1. It shows as an icon by the aspect's and school's and a pulse over the portrait ([ui](ui.md#the-party-tab), [characters](../systems/characters.md#hunger)).

## The party

- **The party holds five**, the original's four and one more, since more companions are coming ([characters](../systems/characters.md#the-party)). The joining scripts and the "no room" prompt behave as they did, counting five as full. The trek HUD's frames, the Party tab's small portraits and the save screen's portraits make room for the fifth; a save written back into a Controller Pak can't hold one.
- **A True Name is kept per member**, as a mod's trigger can give anyone one; the original's comes from the record and, for Alaron, flag
  590 ([saves](../systems/saves.md#the-format)).
- **The sheets of members who left** are kept only for those the original keeps, Abrecan, Arturo, Becan, Godric and Keelin, and a kept sheet
  goes when its member rejoins; the original's nine fixed slots are gone. What a player sees is the same
  ([characters](../systems/characters.md#joining-and-leaving)).
- **The party's leader is the player's body**: Retold gives the player the leader's actor, where the original names Alaron's, which only
  cheats swap. For Alaron it's the same ([hardcoded content](../hardcoded-content.md#assets-engine-code-names)).

## The rules

Experience, the level and skill caps, training's prices, the skill checks and who can learn what are the original's. The Party tab now
shows why a member can't use an item, and what's better than what they wear ([ui](ui.md#the-party-tab)).
