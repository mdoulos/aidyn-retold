# Graphics

How the world, its models and the game's pictures are drawn. The menus' and the HUD's look is in [ui](ui.md), the settings in
[options](options.md#video), and the graphics bugs Retold fixes in [bug fixes](bug-fixes.md#graphics).

Graphic Quality's Original brings the original's detail back for every setting below that has an Original; Medium, the default, and High
are the remake's look ([options](options.md#graphic-quality)).

## The screen

- **Any resolution and shape.** The original draws 320 × 240 (or 512 wide with the Expansion Pak) at 4:3. Retold draws at the window's
  resolution, scaled from 240p up to supersampled at 200%, in any shape, and the view widens with the window
  ([ui](../systems/ui.md#options)). Anti-aliasing is an option.
- **Any frame rate**, with the core's 60 ticks a second interpolated between, and particles glided between their 30 fps steps
  ([rendering](../systems/rendering.md#particles)).

## Draw distance

- **Draw Distance: Extended** (Medium's and High's). The original draws the 3 × 3 chunks round the player. Extended draws the Overworld's
  5 × 5, the outer 16 as scenery alone, with the fog pushed out and the far plane cut round instead of flat; the weather's fog draws it back
  in toward the original's. Elsewhere it keeps the 3 × 3, cut by a sphere at 180, and cut-scenes, arenas and cinematics keep the original's
  planes ([rendering](../systems/rendering.md#draw-distance)).
- **Object Distance: 2x or 3x** (Medium's 2x, High's 3x) draws scene objects, and their fades, two or three times as far. Bodies are drawn
  out to 30 at most, past which they don't move ([rendering](../systems/rendering.md#draw-distance)).
- **Model Detail: Highest** (High's) draws every object's nearest, most detailed level at any distance
  ([rendering](../systems/rendering.md#draw-distance)).
- **An actor's particles fade with it.** The original draws an actor's emitters at any distance, whether the actor is drawn or not, so a
  Fire Elemental's flames show far beyond its body. Retold shows them while the actor is drawn, at its fade
  ([rendering](../systems/rendering.md#draw-distance)).
- **The view's fade widens with the window**, so an object fading out at the original's 4:3 edge fades outside a wide screen's edge
  instead ([rendering](../systems/rendering.md#draw-distance)).

## Models

- **Alaron is remodelled.** About 1,000 triangles and 23 bones, skinned, with a normal map, fingers and a thumb that close as he runs, and
  two outfits, plain and armoured, the armoured one while he wears armor that protects. He walks, jogs and runs on the remodel's own
  animations, and jumps two ways at random, each with its rise, its time in the air and its landing. His original head is emptied, as the
  remodel's body has its own ([replacing graphics](../replacing-graphics.md#a-skinned-character), [rendering](../systems/rendering.md#outfits)).
- **Surface Detail: Full** (the default) lights a model that brings a normal map per pixel, through the original's own light model. None of
  the original's models has one, so only a remodel looks different ([rendering](../systems/rendering.md#normal-maps)).
- **Animation Blending: Smooth** (the default) blends a model with a skeleton into its next animation over 0.18 seconds, where the
  original cuts. None of the original's models has a skeleton, so only a remodel blends
  ([rendering](../systems/rendering.md#blending-between-animations)).
- **The pause menu's scroll is remodelled**: its rolls are a modelled parchment roll with a normal map, lengthened to run past the screen's
  edges ([replacing graphics](../replacing-graphics.md#swapping-a-parts-mesh-and-keeping-its-animations)).
- **Any model, texture or picture can be replaced by a mod**, a texture for one model or for every model that uses it, at any size
  ([ADR 0044](../adr/0044-textures-on-models-come-from-a-plugins-pngs.md), [replacing graphics](../replacing-graphics.md)).

## The art

- **The camp is a picture in pieces**: the user's blank forest, with the user's campfire laid on it at the fire's point, its pot and
  front logs laid again over the flames that burn behind them and its front stones over all the flames, animated flames, smoke and
  embers, and firelight added over it, so another biome or another fire changes a piece, not the painting
  ([camping](../systems/camping.md#the-camp-screen)).
- **The party sits or stands round the camp's fire**, each figure the user's art, Alaron's painted both ways and Becan's so far facing right, with most of the light it was
  painted under taken out, and lit as the forest is through the user's normal maps: a low sun behind the trees at the upper right rims
  their edges, the side the camera sees in the shade, a little brighter from above and greener from below, the fire a warm touch on the
  side that faces it, flickering with it, and a soft shadow under each where it meets the grass. Each casts a long soft shadow from the
  lights strong enough to cast one, as dark as its share of the light: in the day the sun's, away from it, lighter where it crosses
  the firelit ground round the fire; at night it would be the fire's, out from it. Whatever touches the ground has a shadow of its own
  too, each foot and a sitting figure's seat, darkest at the spot it touches and blending into the rest. They idle, each leaning, breathing and half turning a little every few seconds, its feet
  planted. Each stands as tall as the game's own height for them, which sets how long their shadows run, and each party member has a body size, normal, large or giant, which sets how wide: a large man's is about a quarter wider and a giant's about half. Alaron's and Becan's are the first
  ([camping](../systems/camping.md#the-camp-screen)).

- **Redrawn pictures.** Aidyn ReChronicled's redrawn portraits, item icons, empty slots, titles and HUD pieces stand in the original's, drawn
  at the original's size and sharper ([ADR 0034](../adr/0034-retold-and-rechronicled-collaborate.md), [ui](../systems/ui.md#the-ports-layout)).
  Alaron's portrait is the owner's own instead (Nov 7, 2026), kept out of ReChronicled's set so rebuilding that leaves it be.
- **A picture for each potion and each scroll.** The original draws every potion as one flask and every scroll as one scroll. Retold has
  each potion's own bottle (conical flasks with a ribbon for the four flasks, a slanted bottle for Healing, a round one for Curing, square
  ones for Stamina, Antidote and Restore, tubes for the rest, the new Intellect, Determination and Ingenuity among them), and each scroll rolled with a ribbon of its spell's school's colour: green
  Elemental, purple Naming, red Necromancy, gold Star, white for the unschooled. Graphic Quality's Original draws the shared ones
  ([ui](../systems/ui.md#the-ports-layout)).
- **The aspects' and schools' icons are redrawn**: a crescent with a star for Lunar, a sun for Solar, a flaming eye for Chaos, a leaf for
  Elemental, a temple for Naming, a drop of blood with a skull for Necromancy and a four-pointed star for Star. Original draws the
  original's ([ui](../systems/ui.md#the-ports-layout)).
- **Retold's own logo** on the title, in place of the original's logo and its shadow, and as both apps' boot splash
  ([ui](../systems/ui.md#in-the-port-1), [ui](../systems/ui.md#power-on)).
- **The menus' own art**: parchment sheets, the Party tab's tab icons, the purse, the hit points bar, the silhouette's figures and a wax seal
  ([ui](ui.md#the-pause-menu)).

## How it's drawn

- **The sky is placed as the scenery is projected.** The original places the sun, moon, stars and clouds, and slides its gradient, by a
  formula that isn't a projection, so the sky drifts against the scenery as the camera turns and pitches. Retold projects them as it does
  the scenery, so the sky stays put. It isn't a setting; F6 and `--sky original` show the original's placement, for comparing
  ([rendering](../systems/rendering.md#the-skys-colours), [rendering](../systems/rendering.md#the-skys-objects)).
- **Every texture is mipmapped, and blended between levels.** About a fifth of the original's textures have no smaller levels, so tree
  walls shimmer in the distance, and those that have them aren't blended. Retold smooths them all; whether to keep this is still the
  user's to choose ([rendering](../systems/rendering.md#the-n64-material)).
- **No dither.** The console's colour dither makes see-through things grainy; Retold draws at full precision
  ([rendering](../systems/rendering.md#the-n64-material)).
- **Shadows lie on the ground.** The original's shadow is a flat square drawn without a depth test, on the first triangle found, so it can
  stand up on a wall. Retold's is laid on the nearest ground no steeper than 60° and ignores walls ([movement](../systems/movement.md#shadows)).
- **The lens flare looks for the sun along a ray through the collision**, so leaves don't hide it as the original's depth test lets them
  ([rendering](../systems/rendering.md#the-skys-objects)).

## Weather

- **Rain and Snow: 2x, 4x or 8x** (Medium's 4x, High's 8x) makes rain and snow two to eight times as thick as the console's thin fall,
  their splashes up to half again as many ([weather](../systems/weather.md#more-rain-than-the-original)).
- **The rain is already falling past a door.** The original's starts empty in each new place, so drops take two seconds to fall into view;
  Retold's fills the air at once when it's already raining ([weather](../systems/weather.md#differences-from-the-original)).
- **Rain lands on what's drawn**, not on invisible walls ([bug fixes](bug-fixes.md#graphics)).

## Combat

- **Spell auras** can show over every head at once ([combat](combat.md#battles)).

## Cinematics

- **Cinematics are framed at their own 4:3** with black beside them on a wide window, which would otherwise show past the sets' edges,
  their captions and fades inside the frame ([cinematics](../systems/cinematics.md#drawing)).
