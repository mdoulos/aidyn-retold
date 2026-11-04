# Options

Every setting, and what it starts at. The original's options are a short list in the System tab, its volumes and resolution kept in each
save, so loading one puts them back. Retold's are the player's own, kept in `settings.json` in the data folder, not in saves, and in pages
by kind: Audio, Video, Enhancements and Controls, with Rename Character in a game
([ADR 0013](../adr/0013-settings-in-the-options-menu.md), [ADR 0024](../adr/0024-the-options-are-pages-by-kind.md),
[ui](../systems/ui.md#options)). The original's resolution row (the Expansion Pak's 512 wide) isn't there.

The settings are for taste and the machine; what the remake changes isn't a setting
([ADR 0046](../adr/0046-the-project-is-a-remake-that-retells-the-game.md)).

## Audio

| Setting | Choices | Starts at |
| --- | --- | --- |
| Master, Ambient, Effects, Interface, Dialogue | 0 to 100 | 100 |
| Music | 0 to 100 | 65, the original's |

## Video

| Setting | Choices | Starts at |
| --- | --- | --- |
| Display | Windowed, Fullscreen (borderless) | Windowed |
| Window Size | 4:3 and 16:9 sizes from 640 × 480 to 3840 × 2160 that fit the screen, Maximized | as it opens |
| Render Resolution | 240p, 480p, 50%, 75%, 100%, 150%, 200% (over 100% supersampled) | 100% |
| Frame Rate Limit | Display, 30, 60, 90, 120, 144, Unlimited | Display |
| V-Sync | On, Off | On |
| Anti-Aliasing | Off, 2x, 4x, 8x | Off |
| Graphic Quality | Original, Medium Quality, High Quality (Custom when set by hand) | Medium |

### Graphic Quality

Graphic Quality sets the Advanced Graphic Settings together. Original is also the original's look in the menus and text
([ui](ui.md)); Medium and High are the remake's.

| Advanced Graphic Setting | Original | Medium | High |
| --- | --- | --- | --- |
| Draw Distance | Original (3 × 3) | Extended (5 × 5) | Extended |
| Object Distance | Original | 2x | 3x |
| Model Detail | Original | Original | Highest |
| Surface Detail | Original | Full | Full |
| Animation Blending | Original | Smooth | Smooth |
| Rain and Snow | Original | 4x | 8x |

What each does is in [graphics](graphics.md).

## Enhancements

| Setting | Choices | Starts at |
| --- | --- | --- |
| Menu Size | Original (240 lines), Smaller (360), Smallest (480) | Original |
| Text Speed | Original (whole lines), Slow, Normal, Fast | Normal |
| Status Auras | Original, More Often | Original |
| Intro Logos | Original, Skip | Skip |

## Controls

Every control's binding, for the keyboard and mouse and for the controller, Mouse Sensitivity (0.25x to 4x, 1x to start) and Reset to
Defaults ([controls and camera](controls-and-camera.md)).

## Gone

Menu Filter (the UI is always sharp), Pace and Jump (the jump is always on) were settings and are no longer, nor the menu's height, the
Party tab's layout or the text's font, which Graphic Quality's Original covers. The sky's placement is off the menu: F6 and
`--sky original` show the original's for comparing ([ui](../systems/ui.md#options)).
