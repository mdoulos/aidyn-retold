# Controls and camera

How the game is played with a keyboard, a mouse and a controller, and how the cameras follow. The camera bugs Retold fixes are in
[bug fixes](bug-fixes.md#controls-and-camera).

## Controls

- **Every control can be rebound**, for the keyboard and mouse and for the controller apart, on the options' Controls page, listed by
  what the player does in each situation: trek, the menus, a battle and its aiming. A binding taken by another control in the same
  situation moves to the newest; the essential controls always keep one; Reset to Defaults puts them back
  ([ADR 0026](../adr/0026-controls-are-bound-by-situation.md), [ui](../systems/ui.md#controls)).
- **The keyboard's defaults are a PC game's**: WASD or the arrows move, the mouse turns the camera, Shift runs, C walks, Ctrl sneaks, Space
  jumps, E uses, Tab or I, M and J open the pause menu's tabs, Enter or Escape opens the menu, and the wheel zooms. The controller's keep the
  N64's layout ([ui](../systems/ui.md#controls), [runtime](../systems/runtime.md#controls)).
- **Conversations and cut-scenes take the menus' controls**: Accept goes on, Back closes or skips, and Close Menu skips a cut-scene, a
  cinematic or the credits ([ADR 0027](../adr/0027-conversations-take-the-menus-controls.md)).
- **New moves**: a jump and a full run ([mechanics](mechanics.md#moving)).

## The mouse

The original has no mouse.

- **Dragging turns the camera**, as in World of Warcraft: either button held and moved more than 4 pixels turns it, a press without a drag
  is a click, and both buttons held run him the way the camera looks. The wheel zooms from 0.6 to twice the distance. The camera stays
  where the mouse leaves it, carried with him, rather than swinging back behind him
  ([ADR 0036](../adr/0036-the-mouse-turns-the-camera-by-dragging.md), [cameras](../systems/cameras.md#the-follow-camera-cameramode0)).
- **Mouse Sensitivity**, from 0.25x to 4x ([ui](../systems/ui.md#controls)).
- **The menus take the mouse**: hovering moves the cursor in the pause menu's System and Party tabs, the title's menu and the quit box,
  the wheel moves it, and a click chooses. On the Party tab a click picks and a double click uses, items drag to a slot to put them on, to a
  member's portrait to give them, and back to the grid to take them off, and a click on a small portrait shows that member
  ([ui](../systems/ui.md#controls), [ui](../systems/ui.md#the-ports-layout)).
- **In a conversation** the cursor points at a choice and a click anywhere moves on ([ui](../systems/ui.md#controls)).

## The camera

- **He walks while the camera turns.** The original turns its camera only with R held, and Alaron stands still while it does. Retold's
  right stick, Q and E, and the mouse turn it while the left stick or WASD walks him; only Camera Hold, the original's R, holds him still
  ([cameras](../systems/cameras.md#the-follow-camera-cameramode0)).
- **The camera keeps off walls** ([bug fixes](bug-fixes.md#controls-and-camera)).
- **It swings at a steady pace**, the 30 fps frame's, where the original's turn rate follows its frame rate
  ([cameras](../systems/cameras.md#why-the-original-behaves-as-it-does)).
