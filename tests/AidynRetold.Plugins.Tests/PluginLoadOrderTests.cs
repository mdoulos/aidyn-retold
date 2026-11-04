namespace AidynRetold.Plugins.Tests;

/// <summary>The order installed plugins load in, and the check that the ROM plugin was imported by the current importer.</summary>
public sealed class PluginLoadOrderTests : IDisposable
{
    private readonly TempPlugins _temp = new();

    /// <summary>Where the ACRE plugin is, beside the apps rather than in the data folder (ADRs 0039, 0045).</summary>
    private readonly TempPlugins _shipped = new();

    public void Dispose()
    {
        _temp.Dispose();
        _shipped.Dispose();
    }

    private static string Manifest(string id, string[]? dependencies = null, string[]? loadAfter = null, string? import = null)
    {
        static string List(string[] ids) => "[" + string.Join(", ", ids.Select(i => $"\"{i}\"")) + "]";
        var extra = (dependencies is null ? "" : $", \"dependencies\": {List(dependencies)}") +
            (loadAfter is null ? "" : $", \"loadAfter\": {List(loadAfter)}") +
            (import is null ? "" : $", \"import\": {import}");
        return $$"""{ "id": "{{id}}", "name": "{{id}}", "version": "1.0.0"{{extra}} }""";
    }

    private void Rom(int? importer = null) => _temp.AddRaw(RomPlugin.Folder, Manifest("aidyn",
        import: importer is { } n ? $$"""{ "importer": {{n}}, "rom": "abc", "romVersion": "NA 1.0" }""" : null));

    private void Mod(string id, string[]? dependencies = null, string[]? loadAfter = null) =>
        _temp.AddRaw(id, Manifest(id, dependencies, loadAfter));

    /// <summary>The ACRE plugin, in a folder of its own, which <see cref="OrderWithAcre"/> loads.</summary>
    private void Acre(string id = AcrePlugin.Id, string[]? dependencies = null) => _shipped.AddRaw("acre", Manifest(id, dependencies));

    private string[] Order() => [.. PluginSet.LoadInstalled(_temp.Root).Plugins.Select(p => p.Manifest.Id)];

    private string[] OrderWithAcre() => [.. PluginSet.LoadInstalled(_temp.Root, Path.Combine(_shipped.Root, "acre")).Plugins.Select(p => p.Manifest.Id)];

    [Fact]
    public void TheAcrePluginLoadsAfterTheRomPluginAndBeforeTheMods()
    {
        Rom();
        Mod("alpha");
        Acre();

        Assert.Equal(["aidyn", "acre", "alpha"], OrderWithAcre());
    }

    [Fact]
    public void AModCanDependOnTheAcrePlugin()
    {
        Rom();
        Acre(dependencies: ["aidyn"]);
        Mod("alpha", dependencies: ["acre"]);

        Assert.Equal(["aidyn", "acre", "alpha"], OrderWithAcre());
    }

    [Fact]
    public void TheAcrePluginCantDependOnAMod()
    {
        Rom();
        Acre(dependencies: ["alpha"]);
        Mod("alpha");

        Assert.Equal("Plugin acre needs alpha to load before it.", Assert.Throws<InvalidDataException>(OrderWithAcre).Message);
    }

    /// <summary>No mod can have the ACRE plugin's ID, whether or not the ACRE plugin is there.</summary>
    [Fact]
    public void NoModCanHaveTheAcrePluginsId()
    {
        Rom();
        Mod("acre");

        Assert.StartsWith("The mod in ", Assert.Throws<InvalidDataException>(OrderWithAcre).Message, StringComparison.Ordinal);
        Assert.Contains("has the ACRE plugin's ID, acre, which no mod can have", Assert.Throws<InvalidDataException>(Order).Message, StringComparison.Ordinal);
    }

    [Fact]
    public void TheAcrePluginsIdIsAcre()
    {
        Rom();
        Acre(id: "music");

        Assert.EndsWith("says its ID is \"music\"; the ACRE plugin's is \"acre\".", Assert.Throws<InvalidDataException>(OrderWithAcre).Message, StringComparison.Ordinal);
    }

    [Fact]
    public void AnAcrePluginThatIsntThereIsNone()
    {
        Rom();
        Mod("alpha");

        Assert.Equal(["aidyn", "alpha"], OrderWithAcre());
    }

    [Fact]
    public void WithoutConstraintsPluginsLoadByIdAfterTheRomPlugin()
    {
        Rom();
        Mod("zeta");
        Mod("alpha");
        Mod("mid");

        Assert.Equal(["aidyn", "alpha", "mid", "zeta"], Order());
    }

    [Fact]
    public void AnImportKilledPartwayDoesntStopTheLoad()
    {
        // Its staging folder and the old import moved aside both hold the ROM plugin's manifest.
        Rom();
        _temp.AddRaw(RomPlugin.Folder + RomPlugin.StagingSuffix, Manifest("aidyn"));
        _temp.AddRaw(RomPlugin.Folder + RomPlugin.ReplacedSuffix, Manifest("aidyn"));
        Mod("alpha");

        Assert.Equal(["aidyn", "alpha"], Order());
    }

    /// <summary>An import from before the ROM plugin had its name is moved to its folder, rather than imported again (ADR 0037).</summary>
    [Fact]
    public void AnImportInTheOldFolderIsMovedToTheRomPluginsFolder()
    {
        _temp.AddRaw(RomPlugin.OldFolder, Manifest("aidyn", import: $$"""{ "importer": {{RomPlugin.ImporterVersion}}, "rom": "abc", "romVersion": "NA 1.0" }"""));
        Mod("alpha");

        Assert.Null(RomPlugin.Diagnose(_temp.Root));
        Assert.True(Directory.Exists(Path.Combine(_temp.Root, RomPlugin.Folder)));
        Assert.False(Directory.Exists(Path.Combine(_temp.Root, RomPlugin.OldFolder)));
        Assert.Equal(["aidyn", "alpha"], Order());
    }

    [Fact]
    public void AnOldFolderBesideTheRomPluginsIsLeftOutOfTheLoad()
    {
        Rom();
        _temp.AddRaw(RomPlugin.OldFolder, Manifest("aidyn"));
        Mod("alpha");

        Assert.Equal(["aidyn", "alpha"], Order());
        Assert.True(Directory.Exists(Path.Combine(_temp.Root, RomPlugin.OldFolder)));
    }

    [Fact]
    public void ADependencyThatSortsLaterStillLoadsFirst()
    {
        Rom();
        Mod("alpha", dependencies: ["zeta"]);
        Mod("zeta", dependencies: ["aidyn"]);

        Assert.Equal(["aidyn", "zeta", "alpha"], Order());
    }

    [Fact]
    public void LoadAfterOrdersWhenThePluginIsThere()
    {
        Rom();
        Mod("alpha", loadAfter: ["zeta"]);
        Mod("zeta");
        Mod("beta");

        Assert.Equal(["aidyn", "beta", "zeta", "alpha"], Order());
    }

    [Fact]
    public void ALoadAfterThatIsntInstalledIsNotAnError()
    {
        Rom();
        Mod("alpha", loadAfter: ["missing", "aidyn"]);

        Assert.Equal(["aidyn", "alpha"], Order());
    }

    [Fact]
    public void AMissingDependencyIsStillAnError()
    {
        Rom();
        Mod("alpha", dependencies: ["missing"]);

        var error = Assert.Throws<InvalidDataException>(Order);
        Assert.Contains("alpha needs missing", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ACycleIsAnErrorNamingThePluginsInIt()
    {
        Rom();
        Mod("one", dependencies: ["two"]);
        Mod("two", loadAfter: ["three"]);
        Mod("three", dependencies: ["one"]);
        Mod("after_the_cycle", dependencies: ["one"]);
        Mod("fine");

        var error = Assert.Throws<InvalidDataException>(Order);
        Assert.Contains("one, three, two", error.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("after_the_cycle", error.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("fine", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void AFolderMustBeNamedForItsPluginsId()
    {
        Rom();
        _temp.AddRaw("some_folder", Manifest("other_id"));

        var error = Assert.Throws<InvalidDataException>(Order);
        Assert.Contains("some_folder", error.Message, StringComparison.Ordinal);
        Assert.Contains("other_id", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ANotImportedGameIsDiagnosedWithTheFolderItLookedIn()
    {
        var diagnosis = RomPlugin.Diagnose(_temp.Root);

        Assert.NotNull(diagnosis);
        Assert.Contains("hasn't been imported", diagnosis, StringComparison.Ordinal);
        Assert.Contains(Path.Combine(_temp.Root, RomPlugin.Folder), diagnosis, StringComparison.Ordinal);
        Assert.Throws<IOException>(() => PluginSet.LoadInstalledChecked(_temp.Root));
    }

    [Fact]
    public void AnOldImportIsDiagnosedWithBothVersions()
    {
        Rom(importer: RomPlugin.ImporterVersion - 1);

        var diagnosis = RomPlugin.Diagnose(_temp.Root);

        Assert.Equal(
            $"The import is out of date (imported by importer {RomPlugin.ImporterVersion - 1}, now {RomPlugin.ImporterVersion}). Run: acre import <rom>",
            diagnosis);
        var error = Assert.Throws<IOException>(() => PluginSet.LoadInstalledChecked(_temp.Root));
        Assert.Equal(diagnosis, error.Message);
    }

    [Fact]
    public void ARomPluginWithNoImportInfoIsOutOfDate()
    {
        Rom();

        Assert.Contains("imported by importer unknown", RomPlugin.Diagnose(_temp.Root), StringComparison.Ordinal);
    }

    [Fact]
    public void AManifestThatCantBeReadIsDiagnosedRatherThanThrown()
    {
        _temp.AddRaw(RomPlugin.Folder, "{ not json");

        Assert.StartsWith("The import can't be read (", RomPlugin.Diagnose(_temp.Root), StringComparison.Ordinal);
    }

    [Fact]
    public void ACurrentImportPassesAndLoads()
    {
        Rom(importer: RomPlugin.ImporterVersion);
        Mod("alpha", dependencies: ["aidyn"]);

        Assert.Null(RomPlugin.Diagnose(_temp.Root));
        Assert.Equal(["aidyn", "alpha"], PluginSet.LoadInstalledChecked(_temp.Root).Plugins.Select(p => p.Manifest.Id));
    }
}
