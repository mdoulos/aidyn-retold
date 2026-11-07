# Story

Scripts, conversations, cut-scenes, cinematics, the journal and the text. The story is the original's so far: the retelling's changes and
additions to it are to come ([ADR 0046](../adr/0046-the-project-is-a-remake-that-retells-the-game.md)). What's changed is how the scripts
run. Their crashes and softlocks that Retold fixes are in [bug fixes](bug-fixes.md#story); the text's look is in [ui](ui.md).

## Conversations

- **The party talks at camp**, in the conversation box over the camp screen ([world](world.md#camping)).

- **Lines that animate the speaker play.** Nine conversation nodes ask the speaker for an animation in place of the talk, which never
  plays in 1.0: the line shows twice and takes A twice, as with the lighthouse keeper's "Here... take the key" (script 890). Retold plays
  the animation and shows the line once, in conversations; cut-scenes are as 1.0 has them
  ([scripts](../systems/scripts.md#ops-that-need-more-than-a-flag)).
- **A line prints as it's spoken**, a few letters a tick, with a voice ([ui](ui.md#the-conversation-box), [audio](audio.md#the-texts-voice)).
- **The text names the party's leader** where it names Alaron, so a renamed Alaron is named ([characters](characters.md#alaron)).
- **The box after a shop is gone.** The original still shows the conversation box's last page under the shop's menu
  ([ui](../systems/ui.md#the-shop)).

## Cut-scenes

- **No marshmallows.** The original's camp falls back, on a night with no story scene, to Alaron alone at the fire ("pass the
  marshmallows"); Retold's doesn't play it ([world](world.md#camping)).

- **The Fire Lord's scene's walkers walk where the script says** (script `0x048F`). In 1.0 a walker stops at another actor's point, as
  bodies push; Retold's cut-scene actors don't push, the user's judgement: "less faithful but better", and Niesen no longer faces the wrong
  way ([scripts](../systems/scripts.md#actors)).
- **A missing point skips the move** rather than crashing: see [bug fixes](bug-fixes.md#story) for Keelin's and Edur's scenes.

## Cinematics

- **The opening plays from the title's New Game**, then script 866 on its stage, then trek, as in the original; Retold builds trek's world
  before the opening rather than after 866, to the same effect, and a save of a new game that stands nowhere plays the opening when loaded
  ([cinematics](../systems/cinematics.md#what-starts-them), [events](../systems/events.md#where-the-port-differs)).
- **The story's endings are data.** Which cinematic, script, credits or game over follows which scene are trigger records
  (`aidyn:trigger/<n>`), not the engine's code, so a mod can change or add to them ([events](../systems/events.md#the-records)).
- **Cinematics are framed at 4:3** with captions and fades inside the frame ([graphics](graphics.md#cinematics)).

## The journal

- **A new entry is told at once**, and entries are listed in the order they were found ([mechanics](mechanics.md#flags-and-the-journal)).
- **The journal is drawn over the whole menu**, centred, the tabs fading under it ([ui](ui.md#the-pause-menu)).

## Text

- **"XP Remaining" reads "XP Available"**, on the Party tab and the Stats page ([ui](ui.md#the-party-tab)).
- **An item's requirement is spelled out**: "Strength" and "Intelligence" where the original says "the minimum PS required" and "IN"
  ([ui](../systems/ui.md#the-party-tabs-actions)).
- **New messages**: "No room in inventory." when there's no room to take something off, "Unsaved progress will be lost." when quitting,
  and "The party is ambushed, but no one comes." for the camp ambush that finds no enemies ([ui](ui.md), [world](world.md#camping)).
