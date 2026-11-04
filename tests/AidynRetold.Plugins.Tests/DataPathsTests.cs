namespace AidynRetold.Plugins.Tests;

/// <summary>
/// Where the data folder is (<see cref="DataPaths.Resolve"/>): <c>ACRE_DATA</c>, else a portable install's <c>data</c> beside its
/// executables, else <c>AidynRetold</c> in the local application data folder. The rule's inputs are handed in, so the real environment
/// plays no part.
/// </summary>
public sealed class DataPathsTests : IDisposable
{
    private readonly TempPlugins _temp = new();

    public void Dispose() => _temp.Dispose();

    private string App => Path.Combine(_temp.Root, "app");

    private string Local => Path.Combine(_temp.Root, "local");

    public DataPathsTests() => Directory.CreateDirectory(App);

    [Fact]
    public void TheVariableWinsOverEverything()
    {
        File.WriteAllText(Path.Combine(App, DataPaths.PortableMarker), "");
        Assert.Equal(Path.Combine(_temp.Root, "mine"), DataPaths.Resolve(Path.Combine(_temp.Root, "mine"), App, Local));
    }

    [Fact]
    public void AnInstallKeepsItsDataInTheLocalApplicationDataFolder()
    {
        Assert.Equal(Path.Combine(Local, "AidynRetold"), DataPaths.Resolve(null, App, Local));
        Assert.Equal(Path.Combine(Local, "AidynRetold"), DataPaths.Resolve("", App, Local));
        // The CLI and development hand in no app folder.
        Assert.Equal(Path.Combine(Local, "AidynRetold"), DataPaths.Resolve(null, null, Local));
    }

    [Fact]
    public void APortableInstallKeepsItsDataBesideItsExecutables()
    {
        File.WriteAllText(Path.Combine(App, DataPaths.PortableMarker), "");
        Assert.Equal(Path.Combine(App, "data"), DataPaths.Resolve(null, App, Local));
    }

    [Fact]
    public void AnImportBesideTheExecutablesIsUsedWithoutTheMarker()
    {
        Directory.CreateDirectory(Path.Combine(App, "data", "plugins", RomPlugin.Folder));
        Assert.Equal(Path.Combine(App, "data"), DataPaths.Resolve(null, App, Local));
    }

    [Fact]
    public void AnEmptyDataFolderBesideTheExecutablesIsNotAPortableInstall()
    {
        Directory.CreateDirectory(Path.Combine(App, "data", "plugins"));
        Assert.Equal(Path.Combine(Local, "AidynRetold"), DataPaths.Resolve(null, App, Local));
    }

    /// <summary>The ACRE plugin's folder (ADRs 0039, 0045): the variable, else the exported app's, else none.</summary>
    [Fact]
    public void TheAcrePluginIsTheVariablesElseTheAppsElseNone()
    {
        var app = Path.Combine(App, "acre");
        Assert.Equal(Path.Combine(_temp.Root, "mine"), DataPaths.ResolveAcrePlugin(Path.Combine(_temp.Root, "mine"), app));
        Assert.Equal(app, DataPaths.ResolveAcrePlugin(null, app));
        Assert.Equal(app, DataPaths.ResolveAcrePlugin("", app));
        // The CLI, the tests and the apps in development hand in none.
        Assert.Null(DataPaths.ResolveAcrePlugin(null, null));
    }

    [Fact]
    public void TheLastRomIsKeptBesideThePluginsFolder()
    {
        var plugins = Path.Combine(_temp.Root, "data", "plugins");
        Assert.Equal(Path.Combine(_temp.Root, "data", "rom.txt"), DataPaths.LastRomFor(plugins));
        Assert.Equal(Path.Combine(_temp.Root, "data", "rom.txt"), DataPaths.LastRomFor(plugins + Path.DirectorySeparatorChar));
    }

    /// <summary>Importers 60 and 61 recorded the mushroom choice, which reading leaves out, so their manifests still read and are out of date.</summary>
    [Fact]
    public void AnOldImportsOptionsAreReadPast()
    {
        _temp.AddRaw(RomPlugin.Folder, """
            { "id": "aidyn", "name": "Base", "version": "1.0.0",
              "import": { "importer": 61, "rom": "x", "romVersion": "NA 1.0", "options": { "fixMushroom": false } } }
            """);
        Assert.True(RomPlugin.IsImported(_temp.Root));
        Assert.Equal(new ImportInfo(61, "x", "NA 1.0"), RomPlugin.ImportOf(_temp.Root));
        Assert.StartsWith("The import is out of date (imported by importer 61", RomPlugin.Diagnose(_temp.Root), StringComparison.Ordinal);
    }

    [Fact]
    public void NoImportHasNothingToRead()
    {
        Assert.False(RomPlugin.IsImported(_temp.Root));
        Assert.Null(RomPlugin.ImportOf(_temp.Root));
    }
}
