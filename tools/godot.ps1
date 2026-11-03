# Run the pinned Godot editor (.NET build), downloading it on first use.
# Usage: tools/godot.ps1 [<godot arguments>...]
#        tools/godot.ps1 -ExePath    # print the executable's path and exit
#
# Examples:
#   tools/godot.ps1 --path src/AidynRetold.Editor --editor
#   tools/godot.ps1 --path src/AidynRetold.Editor
#
# The version and its hashes are in tools/godot-pin.ps1, which fetches the
# editor from the official GitHub release, checks it against the release's
# SHA-512 and unpacks it into the gitignored .tools/.
#
# There's no param block on purpose: PowerShell would bind Godot's own options
# to it (it read --path as a -Path switch), so every argument goes to Godot as is.
# From PowerShell, put ++ before the project's own options, not --: PowerShell
# drops a bare -- as its end of parameters, and the project then gets no options.
# Godot reads ++ as it reads --. Quote lists too ('40,55'): PowerShell reads
# 40,55 as an array and passes it as two arguments, which the options' pairs
# fall out of step over (--at 40 crashes the start).
$printPath = $args.Count -eq 1 -and $args[0] -eq '-ExePath'
$godotArgs = $args
$ErrorActionPreference = 'Stop'

. (Join-Path $PSScriptRoot 'godot-pin.ps1')
$exe = Install-Godot
if ($printPath) { return $exe }
& $exe @godotArgs
exit $LASTEXITCODE
