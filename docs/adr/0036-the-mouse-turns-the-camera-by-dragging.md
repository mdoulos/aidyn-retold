# 0036. The mouse turns the camera as World of Warcraft's does, by dragging, and is free to click otherwise

Status: Accepted · Oct 31, 2026 · Supersedes [0026](0026-controls-are-bound-by-situation.md)'s mouse look: the mouse on the Camera row always steering, captured in trek and a battle

## Context

[0026](0026-controls-are-bound-by-situation.md) put the mouse on the Camera row by default as mouse look: captured in trek and a battle, it always turned the camera, as a shooter's does. That leaves the cursor nowhere to point. The owner is making the game a PC game first, where the mouse is the main way to act: clicking on what's in the world, and in the menus clicking, right-clicking and dragging items ([the UI's direction](../systems/ui.md#the-ports-layout)). They asked for the camera to work as World of Warcraft's does, so the mouse is free to click on things.

## Decision

- **The cursor is free in trek and a battle.** With the mouse on the Camera row, the camera keeps the angle it was last turned to, as mouse look's did, and the cursor shows.
- **Dragging with the left or right button held turns the camera.** A press is held back until it's let go. If the mouse moved less than 4 pixels meanwhile it was a click, and goes on to what reads presses as it would have. So a battle's left click still attacks and its right click still cancels, a moment later, on letting go. Past 4 pixels it's a drag: the cursor is hidden and captured while the mouse turns the camera at mouse look's rate, and comes back where the press was once both buttons are up.
- **Both buttons held run him the way the camera looks**, as in World of Warcraft, and are never a click.
- **The two buttons turn the camera alike.** World of Warcraft's right button also turns the character to face the camera and makes the turning keys strafe. Here the controls already move him relative to the camera and turn him the way he goes, and the original's bodies have no sidestep, so there's nothing for it to change.
- **Off the Camera row the mouse steers while Camera Hold is held**, as before, and the rest of 0026 stands: the bindings, the situations and the controller's camera.

## Consequences

- A mouse button bound to a control in trek or a battle acts on letting go, not on pressing. In the menus, a conversation, a cut-scene and the free camera the mouse is as it was.
- `Game.Mouse.cs` holds the click and the drag, `CameraRig.Dragging` says when the mouse steers, and `Game.CaptureMouse` captures it only while it's dragged.
- Clicking on things in the world (a body, a container, the ground) is now possible, but nothing yet answers a click there.
