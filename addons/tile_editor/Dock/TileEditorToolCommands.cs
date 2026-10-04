#if TOOLS
using System;
using System.Linq;
using Godot;

namespace CardCleaner.Addons.TileEditor;

/// <summary>
/// Runs the dock's TMX compilation and TSX property insertion tools, reporting progress on the
/// status label and results through dialogs the owner decides how to show.
/// </summary>
internal sealed class TileEditorToolCommands
{
    private readonly Label _statusLabel;
    private readonly Action<Window> _showDialog;

    internal TileEditorToolCommands(Label statusLabel, Action<Window> showDialog)
    {
        _statusLabel = statusLabel;
        _showDialog = showDialog;
    }

    /// <summary>
    /// Compiles the atlas from the TMX/TSX files in a directory and reports the outcome.
    /// </summary>
    internal void CompileTmx(string tiledDir)
    {
        var compiler = new TmxAtlasCompiler();
        var (success, message) = compiler.CompileFromTmx(tiledDir);

        if (success)
        {
            _statusLabel.Text = "TMX compilation complete!";
            GD.Print($"[TileEditorDock] {message}");
            ShowMessage("TMX Compilation Complete", message);
            return;
        }

        _statusLabel.Text = $"TMX compilation failed: {message}";
        GD.PrintErr($"[TileEditorDock] TMX compilation failed: {message}");
        ShowMessage("TMX Compilation Error", $"Compilation failed:\n{message}");
    }

    /// <summary>
    /// Analyzes the TSX files in a directory and, after the user confirms, inserts the missing properties.
    /// </summary>
    internal void ConfirmInsertTsxProps(string tiledDir)
    {
        // First analyze what would be changed
        var reports = TsxPropertyAnalyzer.AnalyzeDirectory(tiledDir);
        var filesWithMissing = reports.Count(r => r.HasMissingProperties);

        if (filesWithMissing == 0)
        {
            ShowMessage("No Changes Needed", "All TSX files already have the required properties.");
            return;
        }

        // Confirm with user
        var dialog = new ConfirmationDialog
        {
            DialogText =
                $"Insert default properties into {filesWithMissing} " +
                "TSX file(s)?",
            Title = "Insert TSX Properties"
        };
        dialog.Confirmed += () =>
        {
            InsertTsxProps(tiledDir);
            dialog.QueueFree();
        };
        dialog.Canceled += () => dialog.QueueFree();
        _showDialog(dialog);
    }

    private void InsertTsxProps(string tiledDir)
    {
        _statusLabel.Text = "Inserting TSX properties...";

        var result = TsxPropertyInserter.InsertPropertiesInDirectory(tiledDir);

        if (result.Success)
        {
            var resultMessage = $"Updated {result.FilesModified} file(s):\n" +
                               $"- {result.TotalTiles} tile(s) modified\n" +
                               $"- {result.TotalWangSets} wang set(s) modified";
            _statusLabel.Text = $"Inserted properties into {result.FilesModified} files";
            GD.Print($"[TileEditorDock] TSX properties inserted: {resultMessage}");
            ShowMessage("Properties Inserted", resultMessage);
            return;
        }

        _statusLabel.Text = $"Property insertion failed: {result.Message}";
        GD.PrintErr($"[TileEditorDock] TSX property insertion failed: {result.Message}");
        ShowMessage("Error", $"Failed:\n{result.Message}");
    }

    private void ShowMessage(string title, string text)
    {
        var dialog = new AcceptDialog { DialogText = text, Title = title };
        dialog.Confirmed += () => dialog.QueueFree();
        _showDialog(dialog);
    }
}
#endif
