using System.Collections.Generic;
using System.Globalization;
using Godot;

namespace CardCleaner.Scripts.Features.Automation;

/// <summary>
///     Whether this run was started for automation (<c>-- --automation-port=N</c>, see tools/e2e/README.md). An
///     automated run lives on a hidden desktop, so nothing in it may grab the user's real mouse.
/// </summary>
public static class AutomationLaunch
{
    public const string PortArgument = "--automation-port=";

    /// <summary>
    ///     The requested port, or null when the run is not automated. Only debug builds honour the argument.
    /// </summary>
    public static int? Port { get; } = OS.IsDebugBuild() ? ParsePort(OS.GetCmdlineUserArgs()) : null;

    public static bool Requested => Port != null;

    /// <summary>
    ///     The mouse mode to hand the engine. Captured and confined modes clip and warp the system cursor, which is
    ///     shared across desktops, so an automated run keeps the pointer visible and sends relative motion instead.
    /// </summary>
    public static Input.MouseModeEnum SafeMouseMode(Input.MouseModeEnum wanted) =>
        Requested && wanted != Input.MouseModeEnum.Hidden ? Input.MouseModeEnum.Visible : wanted;

    public static int? ParsePort(IEnumerable<string> userArgs)
    {
        foreach (var arg in userArgs)
        {
            if (!arg.StartsWith(PortArgument, System.StringComparison.Ordinal))
                continue;
            var text = arg[PortArgument.Length..];
            return int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out var port)
                   && port is > 0 and < 65536
                ? port
                : null;
        }

        return null;
    }
}
