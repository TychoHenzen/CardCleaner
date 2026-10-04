#if TOOLS
namespace CardCleaner.Addons.TileEditor;

public sealed class TileEditorOperationResult
{
    public TileEditorOperationResult(bool success, string message)
    {
        Success = success;
        Message = message;
    }

    public bool Success { get; }
    public string Message { get; }

    public void Deconstruct(out bool success, out string message)
    {
        success = Success;
        message = Message;
    }
}
#endif
