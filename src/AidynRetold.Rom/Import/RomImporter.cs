using AidynRetold.Plugins;
using AidynRetold.Plugins.Records;

namespace AidynRetold.Rom.Import;

/// <summary>
/// Turns a ROM into <c>aidyn.rom</c> (ADR 0001): records under <c>records/</c> and converted assets
/// under <c>assets/aidyn/</c>, keyed by record ID. The output is deterministic: the same ROM and
/// <see cref="RomPlugin.ImporterVersion"/> always give the same bytes.
/// </summary>
/// <remarks>
/// The families of records each have an importer, which builds them from what an <see cref="ImportContext"/>
/// reads of the ROM and writes them through it: <see cref="WorldImporter"/>, <see cref="CombatImporter"/>,
/// <see cref="MagicImporter"/>, <see cref="FlagImporter"/>, <see cref="CharacterImporter"/>, <see cref="ItemImporter"/>,
/// <see cref="TextImporter"/>, <see cref="DialogueImporter"/>, <see cref="CinematicImporter"/>,
/// <see cref="TriggerImporter"/>, <see cref="MapImporter"/>, <see cref="GraphicsImporter"/> and <see cref="AudioImporter"/>.
/// </remarks>
public static class RomImporter
{
    /// <summary>
    /// Imports <paramref name="rom"/> into <paramref name="directory"/>, replacing what's there, with the player's choices
    /// (<see cref="ImportOptions.Default"/> if none), which the manifest records. It writes to a sibling folder first, so a failed
    /// import leaves the old one in place, and deletes it on a failure; one killed partway is left for the next import to
    /// clear, and loading skips it (<see cref="RomPlugin.IsImportWork"/>). An import into <see cref="RomPlugin.Folder"/> replaces one
    /// left in <see cref="RomPlugin.OldFolder"/> beside it too. A data edit of 1.0 is refused (ADR 0038): the ROM plugin is the original's
    /// content as 1.0 has it, and changes to it are base plugins.
    /// </summary>
    public static void Import(RomImage rom, string directory, IProgress<string>? progress = null, ImportOptions? options = null)
    {
        if (rom.Version.ResearchOnly)
            throw new UnsupportedRomException(rom.Sha1, rom.Header);
        if (rom.Version.DataEdit)
            throw new InvalidDataException($"{ImportableRom.EditedRom} (Its SHA-1 is {rom.Sha1}; NA 1.0's is {RomVersions.Na10.Sha1}.)");
        options ??= ImportOptions.Default;

        directory = Path.GetFullPath(directory).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var staging = directory + RomPlugin.StagingSuffix;
        var replaced = directory + RomPlugin.ReplacedSuffix;
        if (Directory.Exists(staging))
            Directory.Delete(staging, recursive: true);
        Directory.CreateDirectory(staging);

        try
        {
            var context = new ImportContext(rom, staging, progress, options);
            context.WriteJson(PluginManifest.FileName, new PluginManifest(
                RecordId.RomNamespace, "Aidyn Chronicles: The First Mage", "1.0.0",
                Import: new ImportInfo(RomPlugin.ImporterVersion, rom.Sha1, rom.Version.Name, options)));

            WorldImporter.Import(context);
            CombatImporter.Import(context);
            MagicImporter.Import(context);
            FlagImporter.Import(context);
            CharacterImporter.Import(context);
            ItemImporter.Import(context);
            TextImporter.Import(context);
            DialogueImporter.Import(context);
            CinematicImporter.Import(context);
            TriggerImporter.Import(context);
            MapImporter.Import(context);
            GraphicsImporter.Import(context);
            AudioImporter.Import(context);
        }
        catch
        {
            TryDelete(staging);
            throw;
        }

        // Moved aside rather than deleted in place, so a file held open can't leave the old import half deleted.
        if (Directory.Exists(replaced))
            Directory.Delete(replaced, recursive: true);
        if (Directory.Exists(directory))
            Directory.Move(directory, replaced);
        Directory.Move(staging, directory);
        TryDelete(replaced);
        if (string.Equals(Path.GetFileName(directory), RomPlugin.Folder, StringComparison.OrdinalIgnoreCase) &&
            Path.GetDirectoryName(directory) is { } parent)
            TryDelete(Path.Combine(parent, RomPlugin.OldFolder));
    }

    private static void TryDelete(string directory)
    {
        try
        {
            if (Directory.Exists(directory))
                Directory.Delete(directory, recursive: true);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            // Left for the next import to clear; loading skips it.
        }
    }
}
