using System.Text.Json.Nodes;
using System.Threading.Tasks;
using Godot;

namespace CardCleaner.Scripts.Features.Automation;

/// <summary>Saves what the main viewport drew. Needs a real renderer; a headless run has nothing to read back.</summary>
public static class CaptureCommands
{
    public static void Register(AutomationCommands commands) =>
        commands.Add(new AutomationCommand("screenshot",
            "{path: absolute .png, width?: px (scaled, keeps aspect), viewport?: SubViewport path} saves the next "
            + "drawn frame of the window, or of one SubViewport (a PC screen, a card render)",
            args => Screenshot(commands, args)));

    private static async Task<JsonNode?> Screenshot(AutomationCommands commands, JsonObject args)
    {
        var path = args.RequiredString("path");
        if (!System.IO.Path.IsPathRooted(path) && !path.StartsWith("user://", System.StringComparison.Ordinal))
            throw new AutomationException("\"path\" must be absolute or user://");
        if (DisplayServer.GetName() == "headless")
            throw new AutomationException("this run is headless: there is no rendered frame to capture");

        var viewport = args.OptionalString("viewport") is { } viewportPath
            ? AutomationView.RequireNode(commands.Tree, viewportPath) as Viewport
              ?? throw new AutomationException($"{viewportPath} is not a Viewport")
            : commands.Tree.Root;
        await commands.Host.ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
        var image = viewport.GetTexture().GetImage()
                    ?? throw new AutomationException("the viewport returned no image");
        if (args.OptionalNumber("width") is { } width && width >= 16 && width < image.GetWidth())
            image.Resize((int)width, (int)(image.GetHeight() * width / image.GetWidth()), Image.Interpolation.Bilinear);

        var folder = System.IO.Path.GetDirectoryName(ProjectSettings.GlobalizePath(path));
        if (!string.IsNullOrEmpty(folder))
            System.IO.Directory.CreateDirectory(folder);
        var error = image.SavePng(path);
        if (error != Error.Ok)
            throw new AutomationException($"saving {path} failed: {error}");
        return new JsonObject
            { ["path"] = path, ["width"] = image.GetWidth(), ["height"] = image.GetHeight() };
    }
}
