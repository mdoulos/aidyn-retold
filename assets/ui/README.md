# UI art

The game's own UI art, drawn by the project's author. None of it comes from the ROM.

- `icon-lunar.png`, `icon-solar.png`, `icon-chaos.png`, `icon-elemental.png`, `icon-naming.png`, `icon-necromancy.png` and `icon-star.png`: the aspects' and the schools' icons, 250 square, the user's (Nov 2, 2026): a crescent with a star, a sun, a flaming eye, a leaf, a temple, a drop of blood with a skull and a four-pointed star. They're drawn wherever the original's `0x01A3` to `0x01A9` are, laid out at the original's 10 square (`UiImages.Get`), but in the original look.
- `parchment.png`: the pause menu's blank parchment, 2560 × 1920 with transparent torn top and bottom edges, the user's third texture (Nov 2, 2026), on the first's outline. The tabs are drawn on it, laid across the screen eight times as fine as the menu (`ParchmentSwap`), and the menu scroll's rolls are painted with it. It's carried in the views' assembly; a plugin's `assets/aidyn/scene/0x0E63.parchment.png` takes its place.
- `ui-inventory.png`, `ui-stats.png`, `ui-skills.png` and `ui-spells.png`: the tabs' icons, a satchel, an arm and brain, a sword with a mortar and flask, and a book with a fireball, 512 square.
- `ui-gold.png`: the purse before the party's gold, 512 square.
- `ui-bar-frame-asymmetrical.png`, `ui-bar-red.png` and `ui-bar-brown.png`: the hit points bar, 850 × 200 each and drawn over one another: the frame, rounded at its left end and with a finial on its right (Nov 2, 2026, where both ends had one), the red fill and the brown track behind it.
- `ui-square-cornered.png`: the knotwork-cornered frame, 512 square, nine-sliced as a frame for any box (`UiPainter.NineSlice`); nothing has it yet.
- `ui-square-solid.png`: a box, 512 square, which the silhouette's slots and the inventory grid's cells sit in.
- `ui-square-select-red.png`: the red square round the inventory grid's cursor's cell, 350 square.
- `parchment-1800x800.png`, `parchment-500x500.png`, `parchment-1800x1800.png`, `parchment-1800x2150.png`, `parchment-320x320.png`, `parchment-512x220.png` and `parchment-600x150.png`: sheets of parchment on a clear background, each drawn for its use, its colour and border matching `parchment.png`, the user's second set, a little warmer than the first on the same outlines (Nov 2, 2026; `UiPainter.Slip`): the wide one for the Party tab's header, the 500 square for the portraits' frames, the large square for the silhouette's, the tall one for the page, the 320 square for the tabs, the 512 × 220 for the gold, and the 600 × 150 for the names on slips and the System list's rows. Other papers, the popups, messages and questions, take whichever of the first four is nearest their shape. The small square is also the outline the portraits are cut to (`UiImages.CutTo`).
- `ui-wax-seal.png`: the seal on a question's slip, 428 square.
- `ui-flourish-red.png`: a heading's red flourish, 655 × 115.
- `silhouette-man.png`, `silhouette-large-man.png` and `silhouette-woman.png`: the figures behind the silhouette's slots, 1024 × 1536, all at one scale, their feet on the same line (`Figures` says whose is whose).

All but the parchment are carried in the views' assembly by their names here (`UiImages.Art`), and drawn far smaller than they're painted, mipmapped. Replacing a file with another of the same name and shape changes the game's. [ui](../../docs/systems/ui.md#the-ports-layout) says where each is drawn.
