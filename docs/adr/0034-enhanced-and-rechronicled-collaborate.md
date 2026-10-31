# 0034. Aidyn Chronicles Enhanced and Aidyn ReChronicled stay two projects, which collaborate and share their assets

Status: Accepted · Oct 29, 2026 · Amends [0032](0032-rechronicled-is-read-for-its-findings.md) (its wait for permission to use ReChronicled's assets is over; the rest of it stands)

## Context

After [0032](0032-rechronicled-is-read-for-its-findings.md) the owner weighed joining [Aidyn ReChronicled](https://github.com/spitzdano/AidynReChronicled) and giving this project up, so that the work went into one project. On Oct 29, 2026 the owner and Spitzdano settled it otherwise: the two stay separate projects, each under its own name, with Spitzdano the primary developer of ReChronicled and the owner of Aidyn Chronicles Enhanced. They help each other as best they can, collaborate, and share assets freely.

The two take different roads to the same game, which is why both have a reason to exist. ReChronicled runs the original's own code, recompiled, under a modern renderer and runtime, so what it plays is the original with enhancements laid over it. Enhanced reimplements the rules in C# over data imported from the ROM, with a plugin system for mods ([0001](0001-importer-owns-the-rom.md), [0003](0003-headless-rules-core.md)). Little of their code is interchangeable, and their licences differ: ReChronicled is GPL-3.0, because it links N64ModernRuntime; this project is MIT. What they can share is assets, findings and designs.

0032 said ReChronicled's art, HD packs and restored samples "wait for a clearer yes", to be recorded there when given. This record is that yes.

## Decision

- **Two projects, two names, two primary developers.** This project keeps its name, its roadmap and its architecture, and ReChronicled keeps its. Neither is folded into the other.
- **Assets are shared freely, both ways.** ReChronicled's painted world textures, portraits, icons, HUD and map art, HD lettering and restored instrument samples are Enhanced's to use, in its mods and its releases. Enhanced's assets, such as Alaron's remodel and the animations made for it ([replacing graphics](../replacing-graphics.md)), and any art or sound made for it later, are ReChronicled's to use. This is the agreement 0032 waited for.
- **Findings and designs are shared as 0032 says.** A fact about the original that either project settles is the other's to take; here it's still confirmed in 1.0's machine code and cited by address before the port relies on it ([0015](0015-the-port-no-longer-reads-the-decomp.md)), and a fix to the original's behaviour is still the user's choice ([0018](0018-the-port-fixes-bugs-and-sets-enhancements.md)).
- **Code follows its licence, not the sharing.** The projects' licences don't change. A finding or design is rebuilt in this project's own code, as before. A piece of ReChronicled's code copied here would need Spitzdano's leave to license that piece under MIT, recorded in the commit that brings it. This project's code is MIT, so ReChronicled may take any of it with the licence's attribution.
- **The decompilation stays unread.** ReChronicled's `decomp/` submodule and the files carrying that decompilation's names are not initialised or read, as 0032 lists them; 0015's reason isn't either project's to waive.
- **Nothing derived from the ROM is committed, whoever made it.** ReChronicled's painted art is made from the game's own pictures and its restored samples from its own samples, so they ship as a mod or in a release, never in this repository, as 0032 and the ground rules already say.
- **Each credits the other.** The README's credits name Aidyn ReChronicled and Spitzdano as a sister project whose assets and findings this project uses. What ReChronicled takes from here is its own to credit.

## Consequences

- The painted-art and restored-sound work 0032 drafted no longer waits for permission. It waits for a copy of the assets, from Spitzdano or a release, and for the mod paths that carry them, which [replacing graphics](../replacing-graphics.md) plans: textures on models from a mod's PNG, higher-resolution UI images, and samples by the Borg11 hash. Those become roadmap tasks as the owner picks them.
- The pipelines already agree on keys (0032): world textures by Borg1 index, portraits and icons by the names the ROM gives its Borg8 images, instrument samples by a hash of the Borg11 sample's data. Shared art needs renaming at most, not rekeying.
- A doc that takes a finding from ReChronicled still says so, naming the commit, and a mod that carries its art still says whose it is.
- 0032 reads as the history it is. Its limits on reading the clone stand; a reader looking for what may be used reads this record.
