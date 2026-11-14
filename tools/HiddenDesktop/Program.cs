// Starts one process on a separate Win32 desktop that is never switched to, so none of its windows can appear on,
// or take focus from, the user's screen. The GPU still renders there, so a game can draw and read back frames.
//
// Usage:
//   HiddenDesktop run [--desktop <name>] [--appdata <dir>] [--watch-log <file>] [--timeout-sec <n>] -- <exe> [args...]
//
// It prints {"pid":<n>} once the child runs, then waits for it and exits with its exit code. While it waits it watches
// the user's own desktop every 100 ms and writes one JSON line to --watch-log for each breach: a top-level window owned
// by the child, the child holding the foreground, or a changed cursor clip rectangle. The child is in a kill-on-close
// job, so ending this launcher ends the child too and no invisible process is left behind.

using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;

namespace HiddenDesktop;

internal static class Program
{
    private const uint GenericAll = 0x10000000;
    private const uint CreateSuspended = 0x00000004;
    private const uint CreateNoWindow = 0x08000000;
    private const uint CreateUnicodeEnvironment = 0x00000400;
    private const uint WaitTimeout = 0x00000102;
    private const int PollMilliseconds = 100;

    private static int Main(string[] args)
    {
        try
        {
            return Run(Options.Parse(args));
        }
        catch (Exception e) when (e is ArgumentException or Win32Exception or IOException)
        {
            Console.Error.WriteLine($"HiddenDesktop: {e.Message}");
            return 2;
        }
    }

    private static int Run(Options options)
    {
        if (options.AppData != null)
        {
            // The child inherits this environment. Godot reads APPDATA for user://, so the user's real saves and
            // settings are never read or written by an automated run.
            Directory.CreateDirectory(options.AppData);
            Environment.SetEnvironmentVariable("APPDATA", options.AppData);
        }

        var desktop = Native.CreateDesktop(options.Desktop, IntPtr.Zero, IntPtr.Zero, 0, GenericAll, IntPtr.Zero);
        if (desktop == IntPtr.Zero)
            throw new Win32Exception(Marshal.GetLastWin32Error(), $"CreateDesktop({options.Desktop}) failed");

        var job = KillOnCloseJob();
        var startup = new Native.StartupInfo
        {
            cb = Marshal.SizeOf<Native.StartupInfo>(),
            lpDesktop = $"WinSta0\\{options.Desktop}"
        };
        var commandLine = new StringBuilder(CommandLine.Join(options.Command));
        if (!Native.CreateProcess(null, commandLine, IntPtr.Zero, IntPtr.Zero, false,
                CreateSuspended | CreateNoWindow | CreateUnicodeEnvironment, IntPtr.Zero,
                Environment.CurrentDirectory, ref startup, out var process))
            throw new Win32Exception(Marshal.GetLastWin32Error(), $"CreateProcess({options.Command[0]}) failed");

        if (!Native.AssignProcessToJobObject(job, process.hProcess))
            throw new Win32Exception(Marshal.GetLastWin32Error(), "AssignProcessToJobObject failed");
        Native.ResumeThread(process.hThread);
        Console.Out.WriteLine(JsonSerializer.Serialize(new { pid = process.dwProcessId, desktop = options.Desktop }));
        Console.Out.Flush();

        var watch = new Watch(process.dwProcessId, options.WatchLog);
        var deadline = options.TimeoutSeconds > 0
            ? DateTime.UtcNow.AddSeconds(options.TimeoutSeconds)
            : DateTime.MaxValue;
        while (Native.WaitForSingleObject(process.hProcess, PollMilliseconds) == WaitTimeout)
        {
            watch.Check();
            if (DateTime.UtcNow < deadline)
                continue;
            watch.Record("timeout", $"killed after {options.TimeoutSeconds} s");
            Native.TerminateProcess(process.hProcess, 124);
        }

        Native.GetExitCodeProcess(process.hProcess, out var exitCode);
        Console.Out.WriteLine(JsonSerializer.Serialize(new { exit = exitCode, breaches = watch.Breaches }));
        Native.CloseHandle(process.hThread);
        Native.CloseHandle(process.hProcess);
        Native.CloseHandle(job);
        Native.CloseDesktop(desktop);
        return (int)exitCode;
    }

    private static IntPtr KillOnCloseJob()
    {
        var job = Native.CreateJobObject(IntPtr.Zero, null);
        var info = new Native.JobExtendedLimitInformation();
        info.BasicLimitInformation.LimitFlags = 0x2000; // JOB_OBJECT_LIMIT_KILL_ON_JOB_CLOSE
        var size = Marshal.SizeOf<Native.JobExtendedLimitInformation>();
        var buffer = Marshal.AllocHGlobal(size);
        try
        {
            Marshal.StructureToPtr(info, buffer, false);
            if (!Native.SetInformationJobObject(job, 9, buffer, (uint)size)) // JobObjectExtendedLimitInformation
                throw new Win32Exception(Marshal.GetLastWin32Error(), "SetInformationJobObject failed");
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
        }

        return job;
    }
}

/// <summary>Watches the user's desktop (the one this launcher runs on) for anything the hidden child shows there.</summary>
internal sealed class Watch(int pid, string? logPath)
{
    private readonly Native.Rect _clip = CurrentClip();
    private readonly HashSet<IntPtr> _reportedWindows = [];
    private bool _reportedForeground;
    private bool _reportedClip;

    public int Breaches { get; private set; }

    public void Check()
    {
        Native.EnumWindows((window, _) =>
        {
            Native.GetWindowThreadProcessId(window, out var owner);
            if (owner == pid && _reportedWindows.Add(window))
                Record("window", $"top-level window 0x{window:X} visible={Native.IsWindowVisible(window)}");
            return true;
        }, IntPtr.Zero);

        var foreground = Native.GetForegroundWindow();
        Native.GetWindowThreadProcessId(foreground, out var foregroundOwner);
        if (foreground != IntPtr.Zero && foregroundOwner == pid && !_reportedForeground)
        {
            _reportedForeground = true;
            Record("foreground", $"window 0x{foreground:X} took the foreground");
        }

        var clip = CurrentClip();
        if (clip.Equals(_clip))
            return;
        // The cursor clip is system-wide. Release it at once so a hidden game can never trap the user's mouse.
        Native.ClipCursor(IntPtr.Zero);
        if (_reportedClip)
            return;
        _reportedClip = true;
        Record("cursor-clip", $"clip changed from {_clip} to {clip}; released");
    }

    public void Record(string kind, string detail)
    {
        Breaches++;
        var line = JsonSerializer.Serialize(new { time = DateTime.UtcNow.ToString("O"), kind, detail });
        Console.Error.WriteLine($"HiddenDesktop breach: {line}");
        if (logPath != null)
            File.AppendAllText(logPath, line + Environment.NewLine);
    }

    private static Native.Rect CurrentClip()
    {
        Native.GetClipCursor(out var rect);
        return rect;
    }
}

internal sealed class Options
{
    public string Desktop { get; private set; } = "cardcleaner-e2e";
    public string? AppData { get; private set; }
    public string? WatchLog { get; private set; }
    public int TimeoutSeconds { get; private set; }
    public List<string> Command { get; } = [];

    public static Options Parse(string[] args)
    {
        if (args.Length == 0 || args[0] != "run")
            throw new ArgumentException("usage: HiddenDesktop run [--desktop n] [--appdata dir] [--watch-log file] "
                                        + "[--timeout-sec n] -- <exe> [args...]");
        var options = new Options();
        var i = 1;
        for (; i < args.Length && args[i] != "--"; i++)
        {
            var value = i + 1 < args.Length ? args[i + 1] : throw new ArgumentException($"{args[i]} needs a value");
            switch (args[i])
            {
                case "--desktop": options.Desktop = value; break;
                case "--appdata": options.AppData = Path.GetFullPath(value); break;
                case "--watch-log": options.WatchLog = Path.GetFullPath(value); break;
                case "--timeout-sec": options.TimeoutSeconds = int.Parse(value); break;
                default: throw new ArgumentException($"unknown option {args[i]}");
            }

            i++;
        }

        options.Command.AddRange(args.Skip(i + 1));
        if (options.Command.Count == 0)
            throw new ArgumentException("no command after --");
        return options;
    }
}

internal static class CommandLine
{
    /// <summary>Quotes each argument by the rules CommandLineToArgvW reverses.</summary>
    public static string Join(IEnumerable<string> args) => string.Join(' ', args.Select(Quote));

    private static string Quote(string arg)
    {
        if (arg.Length > 0 && arg.IndexOfAny([' ', '\t', '"']) < 0)
            return arg;
        var quoted = new StringBuilder("\"");
        var backslashes = 0;
        foreach (var c in arg)
        {
            if (c == '\\')
            {
                backslashes++;
                continue;
            }

            quoted.Append('\\', c == '"' ? backslashes * 2 + 1 : backslashes).Append(c);
            backslashes = 0;
        }

        return quoted.Append('\\', backslashes * 2).Append('"').ToString();
    }
}

internal static class Native
{
    public delegate bool EnumWindowsProc(IntPtr window, IntPtr param);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    public struct StartupInfo
    {
        public int cb;
        public string? lpReserved;
        public string? lpDesktop;
        public string? lpTitle;
        public int dwX, dwY, dwXSize, dwYSize, dwXCountChars, dwYCountChars, dwFillAttribute, dwFlags;
        public short wShowWindow, cbReserved2;
        public IntPtr lpReserved2, hStdInput, hStdOutput, hStdError;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct ProcessInformation
    {
        public IntPtr hProcess, hThread;
        public int dwProcessId, dwThreadId;
    }

    [StructLayout(LayoutKind.Sequential)]
    public record struct Rect(int Left, int Top, int Right, int Bottom);

    [StructLayout(LayoutKind.Sequential)]
    public struct JobBasicLimitInformation
    {
        public long PerProcessUserTimeLimit, PerJobUserTimeLimit;
        public uint LimitFlags;
        public UIntPtr MinimumWorkingSetSize, MaximumWorkingSetSize;
        public uint ActiveProcessLimit;
        public UIntPtr Affinity;
        public uint PriorityClass, SchedulingClass;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct IoCounters
    {
        public ulong ReadOperationCount, WriteOperationCount, OtherOperationCount;
        public ulong ReadTransferCount, WriteTransferCount, OtherTransferCount;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct JobExtendedLimitInformation
    {
        public JobBasicLimitInformation BasicLimitInformation;
        public IoCounters IoInfo;
        public UIntPtr ProcessMemoryLimit, JobMemoryLimit, PeakProcessMemoryUsed, PeakJobMemoryUsed;
    }

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    public static extern IntPtr CreateDesktop(string name, IntPtr device, IntPtr devMode, int flags, uint access,
        IntPtr attributes);

    [DllImport("user32.dll")]
    public static extern bool CloseDesktop(IntPtr desktop);

    [DllImport("user32.dll")]
    public static extern bool EnumWindows(EnumWindowsProc callback, IntPtr param);

    [DllImport("user32.dll")]
    public static extern uint GetWindowThreadProcessId(IntPtr window, out int processId);

    [DllImport("user32.dll")]
    public static extern bool IsWindowVisible(IntPtr window);

    [DllImport("user32.dll")]
    public static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    public static extern bool GetClipCursor(out Rect rect);

    [DllImport("user32.dll")]
    public static extern bool ClipCursor(IntPtr rect);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    public static extern bool CreateProcess(string? application, StringBuilder commandLine, IntPtr processAttributes,
        IntPtr threadAttributes, bool inheritHandles, uint flags, IntPtr environment, string currentDirectory,
        ref StartupInfo startupInfo, out ProcessInformation processInformation);

    [DllImport("kernel32.dll")]
    public static extern uint ResumeThread(IntPtr thread);

    [DllImport("kernel32.dll")]
    public static extern uint WaitForSingleObject(IntPtr handle, int milliseconds);

    [DllImport("kernel32.dll")]
    public static extern bool GetExitCodeProcess(IntPtr process, out uint exitCode);

    [DllImport("kernel32.dll")]
    public static extern bool TerminateProcess(IntPtr process, uint exitCode);

    [DllImport("kernel32.dll")]
    public static extern bool CloseHandle(IntPtr handle);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    public static extern IntPtr CreateJobObject(IntPtr attributes, string? name);

    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern bool SetInformationJobObject(IntPtr job, int infoClass, IntPtr info, uint length);

    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern bool AssignProcessToJobObject(IntPtr job, IntPtr process);
}
