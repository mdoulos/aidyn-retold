using AidynRetold.Plugins;
using AidynRetold.Rom.Import;
using Godot;

namespace AidynRetold.Editor;

/// <summary>
/// The import dialog (docs/systems/editor.md#the-import-dialog, ADR 0043), the one place the ROM is asked for: the editor shows it
/// at startup when there's no current import (<see cref="RomPlugin.Diagnose"/>), over an empty window, before anything loads. The
/// ROM, Browse..., and Import, which runs the importer off the main thread with its steps shown. Cancel, or closing it, quits, so
/// neither the editor nor the game goes on without an import. Started by the game (<see cref="AppLauncher.FromGame"/>), it says the
/// game needs the ROM and ends with Play; otherwise with <see cref="Imported"/>, and the editor goes on.
/// </summary>
public partial class ImportDialog : Window
{
    private static readonly Color Problem = new(1f, 0.5f, 0.45f);

    private readonly string _pluginsDirectory;
    private readonly bool _fromGame;
    private readonly LineEdit _rom = new() { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, PlaceholderText = "The ROM file: .z64, .n64 or .v64" };
    private readonly Button _browse = new() { Text = "Browse..." };
    private readonly Button _import = new() { Text = "Import", CustomMinimumSize = new Vector2(110, 0) };
    private readonly Button _play = new() { Text = "Play", CustomMinimumSize = new Vector2(110, 0), Visible = false };
    private readonly Button _cancel = new() { Text = "Cancel", CustomMinimumSize = new Vector2(110, 0) };
    private readonly Label _status = Wrapped("");
    private readonly FileDialog _dialog = new()
    {
        FileMode = FileDialog.FileModeEnum.OpenFile,
        Access = FileDialog.AccessEnum.Filesystem,
        Filters = ["*.z64, *.n64, *.v64 ; N64 ROMs"],
        UseNativeDialog = true,
        Title = "A North American 1.0 ROM of Aidyn Chronicles: The First Mage",
    };

    /// <summary>The import finished, when the game didn't start the editor: the editor goes on with the new plugins.</summary>
    public event Action? Imported;

    /// <summary>Whether the importer is running, when the dialog can't be closed.</summary>
    public bool Busy { get; private set; }

    /// <param name="pluginsDirectory">The plugins folder the ROM plugin goes in.</param>
    /// <param name="fromGame">The game started the editor for this: it ends with Play, which starts the game and closes the editor.</param>
    public ImportDialog(string pluginsDirectory, bool fromGame)
    {
        (_pluginsDirectory, _fromGame) = (pluginsDirectory, fromGame);
        Title = "Import a ROM";
        Exclusive = true;
        Transient = true;
        Unresizable = true;
        WrapControls = true;

        var page = new VBoxContainer { CustomMinimumSize = new Vector2(Width, 0) };
        page.AddThemeConstantOverride("separation", 12);
        // Out of date rather than missing: the import has to be done again.
        if (RomPlugin.IsImported(pluginsDirectory) && RomPlugin.Diagnose(pluginsDirectory) is not null)
            page.AddChild(Wrapped("Your import is from an older version of Aidyn Chronicles Retold and has to be done again."));
        page.AddChild(Wrapped($"{(fromGame ? "Aidyn Chronicles Retold" : "The Aidyn Chronicles Retold Editor")} requires a North American 1.0 ROM."));

        var row = new HBoxContainer();
        row.AddChild(new Label { Text = "ROM:" });
        row.AddChild(_rom);
        row.AddChild(_browse);
        page.AddChild(row);

        var buttons = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.End };
        buttons.AddChild(_import);
        buttons.AddChild(_play);
        buttons.AddChild(_cancel);
        page.AddChild(buttons);
        // Shown once there's something to say: the import's steps, or what's wrong.
        _status.Visible = false;
        page.AddChild(_status);

        var margin = new MarginContainer();
        foreach (var side in new[] { "left", "top", "right", "bottom" })
            margin.AddThemeConstantOverride("margin_" + side, 16);
        margin.AddChild(page);
        AddChild(margin);
        AddChild(_dialog);

        // What the last import was made from, to do again.
        if (File.Exists(DataPaths.LastRomFor(pluginsDirectory)))
            _rom.Text = File.ReadAllText(DataPaths.LastRomFor(pluginsDirectory)).Trim();

        _browse.Pressed += () =>
        {
            if (Path.GetDirectoryName(RomPath) is { Length: > 0 } folder && Directory.Exists(folder))
                _dialog.CurrentDir = folder;
            _dialog.PopupCentered(new Vector2I(800, 500));
        };
        _dialog.FileSelected += path => RomPath = path;
        _rom.TextSubmitted += text => _ = BackgroundTask.Report(ImportAsync());
        _import.Pressed += () => _ = BackgroundTask.Report(ImportAsync());
        _play.Pressed += Play;
        _cancel.Pressed += Quit;
        CloseRequested += Quit;
    }

    public override void _Ready() => (_rom.Text.Length > 0 ? _import : _browse).GrabFocus();

    /// <summary>The ROM's path, as typed or picked (<c>--rom</c>).</summary>
    public string RomPath
    {
        get => _rom.Text.Trim().Trim('"');
        set
        {
            _rom.Text = value;
            Say("");
        }
    }

    /// <summary>
    /// What Import does (and <c>--import 1</c>): checks the ROM is the unmodified NA 1.0 (<see cref="ImportableRom"/>, ADR 0038) and runs
    /// the importer into the plugins folder off the main thread, showing its steps, then remembers the ROM for next time. What's wrong
    /// is shown in the dialog, and the controls come back.
    /// </summary>
    public async Task ImportAsync()
    {
        if (Busy)
            return;
        var path = RomPath;
        SetBusy(true);
        Say("Reading the ROM…");
        try
        {
            var progress = new Progress<string>(step => Say($"Importing: {step}…"));
            var directory = Path.Combine(_pluginsDirectory, RomPlugin.Folder);
            await Task.Run(() => RomImporter.Import(ImportableRom.Open(path), directory, progress));
        }
        catch (Exception e)
        {
            // A bad ROM or a file that can't be read is the player's to fix, and worded for them; anything else comes with its stack.
            GD.PrintErr(e is InvalidDataException or IOException ? e.Message : e.ToString());
            Say(e.Message, problem: true);
            SetBusy(false);
            return;
        }
        try
        {
            File.WriteAllText(DataPaths.LastRomFor(_pluginsDirectory), Path.GetFullPath(path));
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            GD.PushWarning($"Couldn't remember the ROM: {e.Message}");
        }
        GD.Print($"Imported {path} to {_pluginsDirectory}.");
        if (_fromGame)
        {
            Busy = false;
            _cancel.Disabled = false;
            Say("The import is done.");
            _import.Visible = false;
            _play.Visible = true;
            _play.GrabFocus();
            return;
        }
        SetBusy(false);
        Say("");
        Imported?.Invoke();
    }

    /// <summary>Starts the game and closes the editor, or says to start it if it can't.</summary>
    private void Play()
    {
        if (AppLauncher.LaunchGame())
            GetTree().Quit();
        else
            Say("The game couldn't be started. Start Aidyn Chronicles Retold yourself: the import is done.", problem: true);
    }

    /// <summary>Cancel, or the window's close: the app quits, as there's nothing to go on to. Not while the importer runs.</summary>
    private void Quit()
    {
        if (!Busy)
            GetTree().Quit();
    }

    private void SetBusy(bool busy)
    {
        Busy = busy;
        _rom.Editable = !busy;
        _browse.Disabled = _import.Disabled = _cancel.Disabled = busy;
    }

    private void Say(string text, bool problem = false)
    {
        _status.Text = text;
        _status.Visible = text.Length > 0;
        if (problem)
            _status.AddThemeColorOverride("font_color", Problem);
        else
            _status.RemoveThemeColorOverride("font_color");
    }

    /// <summary>
    /// A label that wraps at the dialog's width, which it has from the start: a wrapping label works out its height at the width it
    /// has, and at none, before the first layout, it's a word to a line, which the window, sized to its contents, would grow to.
    /// </summary>
    private static Label Wrapped(string text) =>
        new() { Text = text, AutowrapMode = TextServer.AutowrapMode.WordSmart, CustomMinimumSize = new Vector2(Width, 0) };

    /// <summary>The dialog's content width.</summary>
    private const float Width = 560;
}
