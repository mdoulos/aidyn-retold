# UI

The menus, the HUD, the conversation box, the battle screen, the title and the text's look. The settings are in [options](options.md), the
controls in [controls and camera](controls-and-camera.md), and the UI's bugs Retold fixes in [bug fixes](bug-fixes.md#ui).

Retold's menus are redesigned, in its own art and layouts ([ADR 0046](../adr/0046-the-project-is-a-remake-that-retells-the-game.md)).
Graphic Quality's Original brings back the original's text, boxes, images, Party tab, tabs and System list, drawn in the original's
320 × 240 frame; it leaves the screen, the options pages, and the World tab's, the HUD's and the conversation box's layouts as Retold has
them ([ui](../systems/ui.md#options)).

## The screen

- **The UI is as wide as the window**, never narrower than 4:3, and 240 lines tall, or 360 or 480 with Menu Size's Smaller and Smallest.
  The original's is 320 × 240 ([ui](../systems/ui.md#the-screen)).
- **It's always sharp.** The original is seen through the console's soft video output; Retold draws the UI at the window's resolution, and
  the Menu Filter setting is gone ([ui](../systems/ui.md#the-screen)).
- **No overscan bars.** The original draws black bars 16 and 12 pixels in over everything; Retold has none, and feathers the edges of the
  pictures that met them ([ui](../systems/ui.md#books-and-the-help-menu)).

## Text

- **A typeface.** The original draws two sheets of bitmap letters. Retold draws Marcellus SC, small capitals as tall as the sheets'
  capitals, sharp at any size; the sheets' button and icon marks are drawn as the sheets have them. A mod can bring its own `.ttf`.
  Original draws the sheets ([ui](../systems/ui.md#the-texts-typeface)).
- **Smaller text in the menus.** The pause menu's tabs, the barter, training and post-battle windows, the details, the popups, the messages
  and the System pages draw their text at about half the fonts' size, headings larger; the conversation box, the journal, the HUD, the
  battle screen, the captions and the credits keep the full size. Original draws them all at full size
  ([ui](../systems/ui.md#the-texts-typeface)).
- **Long names fit on a line**, squeezed to end at their bounds, as the typeface is wider than the sheets ("Detect Moon Phase", "Control
  Marquis") ([ui](../systems/ui.md#the-ports-layout)).

## Popups and messages

- **Loose sheets of parchment** in place of the original's feathered black boxes: popups, messages, questions, names and details, each with a
  soft shadow, and a wax seal on a question that must be answered. Original draws the boxes ([ui](../systems/ui.md#the-ports-layout)).
- **A message has Close**, for the mouse, and a question's answers take clicks ([ui](../systems/ui.md#the-ports-layout)).
- **Popups stand 24 lower** over the silhouette, so their sheets clear the hit points bar ([ui](../systems/ui.md#the-ports-layout)).

## The pause menu

- **The world goes on being drawn under it.** The original copies the frame into a picture and darkens it; Retold stops trek and keeps
  drawing the world and the HUD under a dark layer, at the same tint, so a resized window draws it again
  ([ui](../systems/ui.md#opening-and-closing)).
- **A blank parchment, the screen's height.** The original's tabs are pictures with their labels, frames, silhouette, map and calendar
  painted in, and the world shows above and below their torn edges. Retold's tabs are the game's own blank parchment, drawn eight times as
  fine, out to near the screen's top and foot, with the menu's pieces drawn over it, and parchment between the rollers as the scroll opens
  and closes. Original draws the tabs' own pictures at their height ([ui](../systems/ui.md#opening-and-closing)).
- **The tabs beside the one showing** are drawn on a wide screen, veiled, as one long scroll ([ui](../systems/ui.md#changing-tab)).
- **The journal is drawn over the whole menu**, centred, the tabs fading under it ([ui](../systems/ui.md#in-the-port)).
- **The World tab rings the party only in the Overworld**, not in every outdoor map set without a minimap ([ui](../systems/ui.md#in-the-port)).

## The Party tab

Redesigned, the user's design. Original brings back the original's layout ([ui](../systems/ui.md#the-ports-layout)).

- **Two sheets and a page.** At the left, a sheet with the portraits, the name, "Level n", the hit points bar with "hp / max" in it, and
  the aspect's and school's icons with their names on hover; under it the silhouette, on a figure for a man, a large man or a woman, with
  the slots. At the right, a page with a tab for each list: Inventory, Stats, Skills and Spells, with their own icons, and the gold in a
  purse. The original's is one painted picture with an eight-row list.
- **The inventory is a grid**, 7 × 8, each item's count on it and the wearer's portrait on what's worn, with Show (All, Weapons, Armor,
  Accessories, Potions, Scrolls, Other, Equipped) and Sort (Type, Name, Value), and the inventory's total. Worn items are under Equipped and
  on the silhouette. The other lists are 12 rows, not 8, with thin scroll bars in place of arrows.
- **Marks on the grid.** What the member can't use (by aspect, Strength, Intelligence, skill, or a scroll they can't learn or know already)
  is faded and crossed out in red; what's better than what they wear has a green arrow. An item's card says why in red ("Needs 18 Strength",
  "Needs the Lunar aspect", "Already known") or what it's better than in green.
- **The stats show what an item would do**: with the cursor on one, each stat shows its total, an arrow and what it would be, green up or
  red down. The six stats are spelled out, raised ones green and lowered ones red, with XP Available, Total XP and Next Level.
- **Equipping from the silhouette.** Picking a slot shows what fits it ("Fits the Helmet", "Nothing to put here"); A chooses and A again
  puts it on.
- **Details stay open.** C-down opens an item's details until it's pressed again, where the original shows them while it's held, with a red
  flourish under the title.
- **Button prompts along the foot** name what each button does (Equip, Unequip, Drink, Read, Use, Details, Back), by the device's first
  binding.
- **An action menu** on a right click, or the controller's Appraise: what A does, and Details.
- **Hunger shows**: an amber or red fork and spoon before the school's icon, a green one for Well Fed, its hover saying what it costs, and a soft pulse over the member's portrait, amber every 3 seconds while Hungry, red every 1.5 while Starving, on the HUD and the battle screen too; the HUD's portraits come up as someone grows hungrier ([ui](../systems/ui.md#the-ports-layout)).
- **Food and cooking**: a Food filter, Eat, and the Cook skill's Cooking popup ([items](items.md#food-and-cooking)).
- **No waiting.** The original ignores the next member or list for about a quarter of a second after a potion or an equip; Retold takes the
  press at once, finishing what moves. The header changes in 10 frames, not 15. Original waits as the original does
  ([ui](../systems/ui.md#changing-member-and-list)).
- **A member change crossfades only the slots one member fills and the other doesn't**, not every slot that differs
  ([ui](../systems/ui.md#changing-member-and-list)).

## The System tab

- **The list is Load Game, Save Game, Options, Extras and Quit Game**, Extras holding the Theater, the Credits and Help; the original's is
  Load, Save, Options, Theater, Credits, Help. Each row is on a parchment slip. Original keeps the original's rows, with Quit Game last
  ([ui](../systems/ui.md#the-system-tab)).
- **Quit Game** asks Quit to Desktop, Return to Title or Cancel, warning "Unsaved progress will be lost." unless the game's saved and
  unchanged; in a battle it always warns. The battle's list is Options, Flee and Quit Game ([ui](../systems/ui.md#the-system-tab)).
- **Pages crossfade**, the options and name entry too ([ui](../systems/ui.md#the-system-tab)).
- **Saving and loading** are files, newest first, with Create New Save at the top, a screenshot of each at 208 × 156, the hours padded
  (18:04:28), no "Do not turn the power OFF" step, and the page sliding in at half the original's time ([saves](saves.md),
  [ui](../systems/ui.md#saving-and-loading)).
- **The options are pages by kind** ([options](options.md)).

## The conversation box

- **The box floats**, from 16 to 304 across and down to 228, feathered on every side, where the original's black band runs right across
  the screen to the overscan bars ([ui](../systems/ui.md#the-conversation-box)).
- **A line prints as it's spoken**, a few letters a tick, with a voice; A shows the rest. Text Speed's Original shows it whole, as the
  original does ([ui](../systems/ui.md#the-conversation-box), [audio](audio.md#the-texts-voice)).
- **The mouse points at a choice and a click is A** ([controls and camera](controls-and-camera.md#the-mouse)).

## Books and help pages

- **They fade in and out, centred over the world**, the game darkened under them as under the pause menu, where the original's appear and
  vanish at once over the whole frame ([ui](../systems/ui.md#books-and-the-help-menu)).

## The HUD

- **The minimap, the sundial, the compass and the portraits keep their places from the screen's edges**, the sundial at the left and the
  compass and minimap at the right, on any shape ([ui](../systems/ui.md#in-the-port-2)).
- **A fade is a black layer over the whole screen**, where the original dims each colour ([ui](../systems/ui.md#in-the-port-2)).

## The battle screen

- **The log box floats** as the conversation box does, 6 lower than the original's ([ui](../systems/ui.md#the-log-box)).
- **The radar is in the screen's bottom right corner**, clear of the log box on a wide screen, where the original's is in the log box
  ([ui](../systems/ui.md#on-other-screens)).
- **The enemies stand from the top left corner, the party from the top right**, and the log box at the foot's middle, on any shape
  ([ui](../systems/ui.md#on-other-screens)).

## The title

- **Retold's logo** in place of the original's ([graphics](graphics.md#the-art)).
- **The intro cards are skipped** by default, the Expansion Pak's, the legal text and the N64's, THQ's and H2O's logos; Intro Logos'
  Original shows them, and A, B or Start skips them once a save exists ([ui](../systems/ui.md#the-intro-cards)).
- **Quit Game** is the menu's last row, quitting at once ([ui](../systems/ui.md#the-menu)).

## The credits

- **The credits fade in every time** trek comes back after them, where the original skips the fade the first time after the console's
  switched on, and stand on a screen of their own, centred ([ui](../systems/ui.md#the-credits)).
