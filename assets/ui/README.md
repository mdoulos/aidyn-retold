# UI art

The game's own UI art, drawn by the project's author. None of it comes from the ROM.

- `parchment.png`: the pause menu's blank parchment, 2560 × 1920 with transparent torn top and bottom edges. The tabs are drawn on it, laid across the screen eight times as fine as the menu (`ParchmentSwap`), and the menu scroll's rolls are painted with it. It's carried in the views' assembly; a plugin's `assets/aidyn/scene/0x0E63.parchment.png` takes its place.
- `ui-inventory.png`, `ui-stats.png`, `ui-skills.png` and `ui-spells.png`: the tabs' icons, a satchel, an arm and brain, a sword with a mortar and flask, and a book with a fireball, 512 square.
- `ui-gold.png`: the purse before the party's gold, 512 square.
- `ui-bar-frame.png`, `ui-bar-red.png` and `ui-bar-brown.png`: the hit points bar, 850 × 200 each and drawn over one another: the cartouche, the red fill and the brown track behind it.
- `ui-square-cornered.png`: the knotwork-cornered frame, 512 square, nine-sliced as a frame for any box (`UiPainter.NineSlice`); nothing has it yet.
- `ui-square-solid.png`: a box, 512 square, which the silhouette's slots and the inventory grid's cells sit in.
- `ui-square-select-red.png`: the red square round the inventory grid's cursor's cell, 350 square.
- `parchment-bg-400x180.png`, `parchment-bg-500x500.png`, `parchment-bg-1400x1800.png` and `parchment-bg-1500x1800.png`: sheets of parchment with torn edges on a clear background, a wide one, a square and two tall ones. Every paper in the menus is the one nearest its shape (`UiPainter.Slip`): the Party tab's page, its left column's sheets and the gold's, the tabs, the portraits' frames, and the popups, messages, questions and names. The square one is also the outline the portraits are cut to (`UiImages.CutTo`).
- `ui-wax-seal.png`: the seal on a question's slip, 428 square.
- `ui-flourish-red.png`: a heading's red flourish, 655 × 115.
- `silhouette-man.png`, `silhouette-large-man.png` and `silhouette-woman.png`: the figures behind the silhouette's slots, 1024 × 1536, all at one scale, their feet on the same line (`Figures` says whose is whose).

All but the parchment are carried in the views' assembly by their names here (`UiImages.Art`), and drawn far smaller than they're painted, mipmapped. Replacing a file with another of the same name and shape changes the game's. [ui](../../docs/systems/ui.md#the-ports-layout) says where each is drawn.
