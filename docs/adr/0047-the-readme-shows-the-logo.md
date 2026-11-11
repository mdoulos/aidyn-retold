# 0047. The README shows the logo: the one image in the public repository, which is published as github.com/mdoulos/aidyn-retold

Status: Accepted · Nov 11, 2026 · Amends [0041](0041-the-art-and-music-are-the-private-repositorys.md)

## Context

[0041](0041-the-art-and-music-are-the-private-repositorys.md) keeps every piece of art in the private repository, the branding among
it, because the art is a mix of the owner's own, licensed and derived from the original's; the logo is the original's title logo with
"Retold" under it. The owner moved the public repository to a new home, `github.com/mdoulos/aidyn-retold`, replacing
`aidyn-chronicles-enhanced`, and wants its README to open with the logo, which the releases already show on the boot splash and the title
screen.

## Decision

- **One image is committed here: `docs/images/acre-logo.png`,** the private repository's `branding/acre-logo.png` scaled to 640 px wide,
  which the README shows by its address on GitHub (`raw.githubusercontent.com/mdoulos/aidyn-retold/main/…`), so the README a release
  carries shows it too. Made again from that file when the logo changes; it's for the README only, and nothing in the build reads it.
- **It isn't under the MIT license,** as the README's License section says, like the art and music a release carries.
- **Everything else in 0041 stands:** new art the code draws is an image in the ACRE plugin, and the branding the builds use stays in the
  private repository.
- **The public history holds no other art.** When the repository moved, its history was rebuilt for the new home: the art committed
  before 0041 was left out of every commit, and the code it was forked from, HBHT, became one starting commit, the owner's import of
  HBHT's code at its commit 6ec85b3 under the MIT license, whose notice `LICENSE` keeps. HBHT's own history is in its repository.

## Consequences

- The README's logo is a second copy of the branding's, to keep in step by hand.
- Commit hashes cited before Nov 11, 2026 (in old notes or the deleted repository's releases) don't match the new history's; the commits
  keep their messages, dates and order.
