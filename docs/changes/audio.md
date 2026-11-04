# Audio

Music, sound and the text's voice. The original's sequencer and mixer are ported, so the original's music plays as it did; the volumes'
settings are in [options](options.md#audio), and the audio bugs Retold fixes in [bug fixes](bug-fixes.md#audio).

## Volumes

- **Six volumes**: Master, Music (65 to start, the original's), Ambient, Effects, Interface and Dialogue (100 to start). The original has a
  sound volume and a music volume, and takes the sound volume two or three times over for some sounds, so at half volume menu sounds play at
  a quarter and containers at an eighth; Retold takes each group's volume once ([audio](../systems/audio.md#volumes)).
- **A cinematic's score plays at the Music volume**, where the original takes the louder of its two ([audio](../systems/audio.md#volumes)).

## Music

- **Gwernia has new music**: the user's "Gwernia Town Peace", a recording in the ACRE plugin, plays in place of the original's sequence
  `0x05CE`, levelled to match it. The user's War and Finale versions aren't played yet
  ([audio](../systems/audio.md#recordings-in-the-sequences-place)).
- **Any sequence can be a recording.** A plugin's WAV named for a sequence plays wherever that sequence would
  ([audio](../systems/audio.md#recordings-in-the-sequences-place)).

## The text's voice

- **A conversation's line has a voice.** The original shows a line whole and silent. Retold prints it a few letters a tick with a soft blip
  for each, pitched by the speaker: a man's lower, a woman's higher, by height, deeper for big men, guards, brutes, ogres, the Cyclops, the
  Troll and minotaurs, with tones (soldier, soft, sly, grand, old, hollow and others) and moods (stern, pleading, sad, glad) from the line.
  Talkative speakers, as Godric, blip more often, and reserved ones, as Becan, Brenna and Farris, less. Creatures and signs have none. It
  plays at the Dialogue volume, silent at 0, and Text Speed's Original shows lines whole with no voice
  ([ui](../systems/ui.md#the-conversation-box), [hardcoded content](../hardcoded-content.md#assets-engine-code-names)).

## Sound

- **A rain's loop goes on, hushed, under the post-battle windows**, where the original's is silent; whether to hush or stop it is still
  open ([weather](../systems/weather.md#differences-from-the-original)).
- **The weather's loop and the sky carry on through cut-scenes and battles** ([bug fixes](bug-fixes.md#world)).
