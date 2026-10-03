#if TOOLS
using Godot;

namespace CardCleaner.Addons.TileEditor;

public partial class TileEditorDock
{
    private Button? _compileTmxButton;
    private Button? _insertPropsButton;

    internal string TiledDirectory { get; set; } = "res://Data/Tiled";
    internal bool SuppressDialogs { get; set; }
    internal Button? CompileTmxButton => _compileTmxButton;
    internal Button? InsertTsxPropsButton => _insertPropsButton;

    private void ShowToolDialog(Window dialog)
    {
        if (!SuppressDialogs)
        {
            AddChild(dialog);
            dialog.PopupCentered();
            return;
        }

        if (dialog is ConfirmationDialog confirmation)
            confirmation.EmitSignal(ConfirmationDialog.SignalName.Confirmed);
        else
            dialog.Free();
    }
}
#endif
