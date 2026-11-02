# UI art

The game's own UI art, drawn by the project's author. None of it comes from the ROM.

- `parchment.png`: the pause menu's blank parchment, 2560 × 1920 with transparent torn top and bottom edges. The tabs are drawn on it, laid across the screen four times as fine as the menu (`ParchmentSwap`), and the menu scroll's rolls are painted with it. It's carried in the views' assembly; a plugin's `assets/aidyn/scene/0x0E63.parchment.png` takes its place.
- `ui-tab-closed.png` and `ui-tab-open.png`: the Party tab's tabs, banners on rods, 512 square, closed and open. The open one is lighter and hangs lower.
- `ui-inventory.png`, `ui-skills.png` and `ui-spells.png`: the tabs' icons, a satchel, a sword and quill, and a book, 512 square.
- `ui-gold.png`: the purse before the party's gold, 512 square.
- `silhouette-man.png`, `silhouette-large-man.png` and `silhouette-woman.png`: the figures behind the silhouette's slots, 1024 × 1536, all at one scale, their feet on the same line (`Figures` says whose is whose).

All but the parchment are carried in the views' assembly by their names here (`UiImages.Art`), and drawn far smaller than they're painted, mipmapped. Replacing a file with another of the same name and shape changes the game's. [ui](../../docs/systems/ui.md#the-ports-layout) says where each is drawn.
