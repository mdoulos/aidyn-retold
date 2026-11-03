# 0042. What's only for the owner's development lives in `Development`, beside the two repositories, and is never public

Status: Accepted · Nov 3, 2026 · Adds to [0041](0041-the-art-and-music-are-the-private-repositorys.md)

## Context

The owner's one-off launchers and their scratch, such as the full party's save for working on the party screens, were in this repository's gitignored `dev/`: kept out of the commits, but inside the public repository's folder. [0041](0041-the-art-and-music-are-the-private-repositorys.md) set the private repository beside this one for art and music. The owner wants what's only for development in a third folder of its own, beside both, and never public.

## Decision

- **`Development`, beside `Aidyn Retold Public` and `Aidyn Retold Private`,** holds what only the owner's development needs: the launchers for one way of running something (`full-party.cmd`, `party-animation-ring.cmd`, `voice-ring.cmd`, `text-voice.cmd`, `world-editor.cmd`), the scripts and data behind them (`full-party.cs` and its save, `rechronicled-art.py` and its map), and the owner's notes (`todo.md`). It's never published, in either repository or a release.
- **A launcher there runs this repository's code from beside it:** it changes to `..\Aidyn Retold Public` before building or running, and a file-based app there names this repository's projects by that path (`#:project ../Aidyn Retold Public/src/…`).
- **What's committed here stays here,** for anyone who clones it: the tools the build, the release and the docs use (`tools/`), and `Launch ACRE.cmd`, which builds and starts the game.
- **`dev/` stays gitignored,** so a clone's own scratch there is never committed.

## Consequences

- The docs here that name one of the owner's launchers say it's in `Development`, outside the repository.
- Moving `Development`, or renaming the public repository's folder, means changing the launchers' path to it.
