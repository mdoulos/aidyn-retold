# Releases and packages

Aidyn Chronicles Retold ships as two programs, the game (`acre`, the Godot project `src/AidynRetold.Game`) and the editor (`acre-editor`, `src/AidynRetold.Editor`), with the owner's base plugins when the build is given them ([below](#base-plugins)) and no content of the original's: players import their own ROM on first run. `tools/package.ps1` builds the packages for the machine it runs on, and the release workflow (`.github/workflows/release.yml`) runs it on Windows, Linux and macOS and publishes the result as a numbered prerelease.

## The packages

| Package | File | Data folder |
| --- | --- | --- |
| Windows installer | `AidynRetold-alpha-N-windows.msi` | `%LOCALAPPDATA%\AidynRetold` |
| Windows portable | `AidynRetold-alpha-N-windows-portable.zip` | beside the executables (`portable.txt`) |
| Linux | `AidynRetold-alpha-N-linux-x86_64.tar.gz` | `~/.local/share/AidynRetold` |
| macOS | `AidynRetold-alpha-N-macos.dmg` | `~/Library/Application Support/AidynRetold` |

On Windows and Linux both apps are in one folder, side by side, as Godot's .NET export lays each out:

```
acre(.exe)            acre-editor(.exe)
acre.pck              acre-editor.pck
data_AidynRetold.Game_<platform>_x86_64/     data_AidynRetold.Editor_<platform>_x86_64/
plugins/   NOTICE.md    (with base plugins only)
LICENSE   README.md   portable.txt (the portable zip only)
```

`<platform>` is `windows` or `linuxbsd`. The two exports don't collide: each executable finds the PCK with its own name, and its .NET assemblies and runtime in the data folder named after its assembly. The Windows zip has the files at its root (Explorer's Extract All makes the folder); the Linux tarball has them in a folder `AidynRetold-alpha-N/`. The first-run code finds the other app, and `portable.txt`, by these names, so they don't change.

On macOS the disk image holds `Aidyn Chronicles Retold.app` (executable `Contents/MacOS/acre`) and `Aidyn Chronicles Retold Editor.app` (`Contents/MacOS/acre-editor`), `LICENSE`, `README.md` and a link to Applications. Each bundle has its PCK and two data folders, `data_<assembly>_macos_arm64` and `_x86_64`, in `Contents/Resources`: the export is universal, and the .NET export publishes each architecture's runtime. With base plugins, each bundle has its own copy in `Contents/Resources/plugins`, as a player may move the two apps apart, and `NOTICE.md` is beside `LICENSE`.

## Base plugins

The owner's base plugins ([ADR 0039](../adr/0039-base-plugins-ship-beside-the-apps.md)) live in the owner's private repository, `Aidyn Retold Private`, not this one: only art and music go there, with the patches that fix the original's content beside them in the one base plugin, `acre`, the remade game ([ADR 0045](../adr/0045-the-remade-game-is-one-base-plugin.md)), and none of it is committed here ([ADR 0041](../adr/0041-the-art-and-music-are-the-private-repositorys.md), [ADR 0043](../adr/0043-the-rom-is-asked-for-once-and-fixes-are-base-plugins.md)). A package carries them when the build is given their folder, the private repository's `plugins`:

```
Aidyn Retold Private/
  plugins/                  -BasePlugins, and ACRE_BASE_PLUGINS in development
    NOTICE.md               whose the art and music are, on what terms, and any credit their licences ask for
    acre/                   the remade game, the one base plugin, with its plugin.json (ADR 0045)
      assets/acre/          the game's own art, acre:image/<key>
      assets/aidyn/         what it puts in the original's place: the remodels, ReChronicled's art, the music
      records/              its patches: Alaron's remodel, and fixes/, which fix the original's content (ADR 0043)
  branding/                 -Branding, found beside -BasePlugins (below)
  blender/                  the remodels' sources, which no package carries
```

- **`NOTICE.md` is required.** The MIT [LICENSE](../../LICENSE) covers the code and docs, not the art and music, so a package with base plugins says what does. It goes beside `LICENSE`, and the MSI's license page shows it under the MIT text. Licensed work names its licence and credit there: a CC BY track, such as Kevin MacLeod's on incompetech.com, gives its title, its author and the licence, as the author's site words it.
- **Each folder holding a `plugin.json` is a plugin**, copied whole into the package's `plugins`, beside the executables or in each macOS bundle's `Contents/Resources` (`Copy-BasePlugins`). Other folders, such as `.git`, are left out. The script doesn't check the plugins' records: load them once in the game with `ACRE_BASE_PLUGINS` pointing at the folder first ([runtime](runtime.md#the-base-plugins)).
- **A local build** takes the folder: `tools/package.ps1 -Version 7 -BasePlugins "..\Aidyn Retold Private\plugins"`.
- **The release workflow** checks the private repository out before packaging when the repository's Actions variable `BASE_PLUGINS_REPO` names it (`owner/name`). `BASE_PLUGINS_PATH` is the folder in it that holds the plugins and `NOTICE.md`, `plugins` if unset, and the secret `BASE_PLUGINS_TOKEN` reads it: a fine-grained personal access token with read-only Contents access to that one repository. The checkout fetches Git LFS files too. Without the variable, the packages carry no base plugins and have no branding, so packaging stops ([below](#branding)).
- **A release is public,** wherever its files came from: anyone can download a package and take its plugins out. What goes in a base plugin is published by being in one.

## Branding

The apps' icon and boot splash are art, so they're the private repository's too ([ADR 0041](../adr/0041-the-art-and-music-are-the-private-repositorys.md)): its `branding/` holds `acre-icon.png` and `acre-logo.png`, and its `make.py` makes `acre-icon-256.png`, `acre-splash.png`, `acre-icon.ico` and `acre-icon.icns` from them. Each Godot project here has gitignored copies, `icon.png` (its `config/icon`), `splash.png` (its boot splash), and `icon.ico` and `icon.icns`, which its export presets name; `make.py` writes them. The title screen's logo isn't one of them: it's the `acre` base plugin's `acre:image/logo`.

`tools/package.ps1 -Branding <folder>` refreshes the projects' copies from the folder before each export and takes away any it made after; without it, the script takes the `branding` folder beside `-BasePlugins`, which is the private repository's layout, so the release workflow needs nothing more. With neither, the projects' copies are used as they are, and a project without them stops the build.

Every package is built with Godot's **release** templates, so `OS.IsDebugBuild()` is false in it, which is what keeps the debug keys out of a player's hands. The exports don't embed the PCK and have no console wrapper.

### Windows

The **MSI** (`packaging/windows/AidynRetold.wxs`, WiX 6.0.2 from the repository's tool manifest, `dotnet-tools.json`) installs per machine into `Program Files\Aidyn Chronicles Retold`, with Start menu shortcuts "Aidyn Chronicles Retold" and "Aidyn Chronicles Retold Editor" and the icon in Add/Remove Programs. Its one page shows the license (WixUI_Minimal). The `UpgradeCode` is fixed and the package has a `MajorUpgrade`, so installing a newer release replaces the old one, and an older one refuses. The installer never writes the player's data folder, so uninstalling leaves the imported game, saves and settings where they are. The shortcuts' component is kept by a per-user registry value (`HKCU\Software\mdoulos\AidynRetold`), which Windows Installer's checks ICE43 and ICE57 require of a shortcut; the package passes `wix msi validate`.

The **portable zip** is the same folder with `portable.txt` beside the executables, which tells Aidyn Chronicles Retold to keep its data in that folder.

The executables carry the icon, ACRE's (`acre-icon.ico`, [branding](#branding)), and version resources: product name "Aidyn Chronicles Retold" or "Aidyn Chronicles Retold Editor", company and copyright mdoulos, file and product version `N.0.0.0`. Neither package is signed, so SmartScreen may warn on the first run ("More info", then "Run anyway").

### Linux

A **tar.gz** of the folder. It's what Godot exports, it keeps the executable bits, and it needs nothing to build or maintain; players unpack it anywhere and run `acre`. The executables need the libraries any Godot 4 game does (Vulkan or OpenGL drivers, X11 or Wayland, ALSA or PulseAudio). AppImage and Flatpak were passed over for now: two programs in one AppImage need a launcher to choose between them, and Flatpak needs a manifest, a runtime and a repository to publish to, and sandboxes the paths the importer reads ROMs from.

### macOS

A **disk image** (`hdiutil`, on the macOS runner) with both apps, because a player drags apps out of a disk image into Applications as a matter of course, and the two have to stay side by side.

Godot names a bundle's executable and PCK after the project (`Aidyn Chronicles Retold Editor`), so the script renames them to `acre` and `acre-editor`, with `CFBundleExecutable`, and signs each bundle again ad hoc (`codesign --sign -`), since the rename breaks Godot's signature and Apple silicon runs nothing unsigned. Godot looks for the PCK in `Contents/Resources` by the executable's name, so the two renames go together. Bundle identifiers are `com.mdoulos.aidynretold` and `com.mdoulos.aidynretold.editor`.

There's no Developer ID, so the apps aren't notarized and Gatekeeper stops them the first time. A player:

1. Drags both apps from the disk image to Applications (or any one folder, together).
2. Opens Aidyn Chronicles Retold. macOS says it can't check it for malicious software; choose Done.
3. In System Settings, Privacy & Security, chooses **Open Anyway** beside the message about Aidyn Chronicles Retold, and confirms with their password. Then the same for Aidyn Chronicles Retold Editor.

Or, in Terminal, clears the quarantine once: `xattr -dr com.apple.quarantine /Applications/Aidyn Chronicles Retold.app "/Applications/Aidyn Chronicles Retold Editor.app"`.

Running the apps straight from the disk image or the Downloads folder works, but while they are quarantined macOS runs each from a random read-only copy (App Translocation), where the game can't find the editor beside it. Moving them clears that.

## Building locally

```powershell
tools/package.ps1 -Version 7     # the packages for this machine, as release 7
tools/package.ps1                # as release 0
```

The script:

1. Fetches the pinned Godot editor and the .NET export templates into `.tools/godot/` (`tools/godot-pin.ps1`, which `tools/godot.ps1` shares), each checked against the SHA-512 in the release's `SHA512-SUMS.txt`. The templates come as one 1.2 GB `.tpz` of every platform; only the four desktop release files are kept, in the self-contained editor's `editor_data/export_templates/4.7.2.stable.mono/`.
2. For each project, puts in place what its export needs and takes it away after: `config/version="N"` in `project.godot` (committed as `"0"`), which Godot gives to the Windows version resources and the macOS bundle versions; the branding's `icon.png`, `splash.png`, `icon.ico` and `icon.icns` ([branding](#branding); the last two excluded from the PCK); and `<assembly>.sln`, which Godot's .NET export refuses to run without, though it publishes the `.csproj` (the repository's solution is `AidynRetold.slnx`).
3. Imports and exports each project headless with its preset (`--export-release`). Godot exits 0 from a failed .NET export, so the script fails on any `ERROR:` line, and keeps Godot's output in `out/packages/work-<platform>/logs/`.
4. Lays out the folder in `out/packages/work-<platform>/` and writes the packages to `out/packages/`.

`-Version` is at most 255, an MSI's highest major version. The MSI's `ProductVersion` is `N.0.0` (Windows Installer compares only the first three fields), the executables' `N.0.0.0`.

The export presets (`export_presets.cfg` in each project, committed) hold nothing machine-specific: no export path, no signing identity. If the Godot editor's Export dialog is opened it writes them back with every option spelled out, which is harmless. A universal or arm64 macOS export requires ETC2/ASTC texture import, so both projects turn it on; neither imports any VRAM-compressed texture, so it changes nothing else.

## The release workflow

Run **Release** by hand from the repository's Actions tab (`workflow_dispatch`). It:

1. Picks the number: one more than the highest tag `v<number>`, or 1 with none. Two runs never overlap (`concurrency: release`).
2. Runs `dotnet test` on Windows. The real-data tests skip, as CI has no ROMs.
3. Runs `tools/package.ps1 -Version N` on `windows-latest`, `ubuntu-latest` and `macos-latest`, with .NET from `global.json` and `.tools/godot` cached by the hash of `tools/godot-pin.ps1`, and uploads the packages (and Godot's logs, if it fails).
4. Writes the description with `tools/release-notes.ps1`: what to download and how to open it, then one bullet per commit since the previous `v*` tag, newest first, each the first sentence of its message (cut at 280 characters) with a link to the commit, at most 100, and a link to the full comparison. The first release, with no tag before it, says it's the first alpha and links the history instead.
5. Creates the release `vN`, "Aidyn Chronicles Retold alpha N", as a prerelease on the commit the workflow ran on, with the four packages attached (`gh release create`, which makes the tag).

Only the last job can write to the repository (`contents: write`); the rest read. The actions are GitHub's own, pinned to major versions.

To try the notes without a release: `tools/release-notes.ps1 -Version 2 -Previous HEAD~5 -RepositoryUrl https://github.com/mdoulos/aidyn-chronicles-enhanced`.

## Not done yet

- Nothing is signed with a certificate: no Authenticode on Windows, no Developer ID or notarization on macOS. Both need paid certificates and secrets in the workflow.
- Linux is x86_64 only, and Windows x86_64 only; Godot has arm64 templates for both.
- The game doesn't show its version anywhere yet; `ProjectSettings` holds it as `application/config/version`.
