using System.Diagnostics;
using System.Runtime.InteropServices;

namespace WinNUT.Core;

public interface IShutdownExecutor
{
    Task ShutdownAsync(string reason, TimeSpan delay);
    Task CancelAsync();
}

/// <summary>Builds the OS shutdown command lines (separate so they can be unit tested without shutting anything down).</summary>
public static class ShutdownCommand
{
    public static (string File, string[] Args) Build(string reason, TimeSpan delay, bool windows)
    {
        int secs = (int)Math.Max(0, delay.TotalSeconds);
        return windows
            ? ("shutdown", ["/s", "/t", secs.ToString(), "/c", "WinNUT: " + reason])
            : ("shutdown", ["-h", secs == 0 ? "now" : "+" + Math.Max(1, (secs + 59) / 60), "WinNUT: " + reason]);
    }

    public static (string File, string[] Args) BuildCancel(bool windows) => ("shutdown", [windows ? "/a" : "-c"]);
}

/// <summary>Uses shutdown.exe on Windows and `shutdown` on Linux/macOS.</summary>
public sealed class SystemShutdownExecutor : IShutdownExecutor
{
    private static bool Win => RuntimeInformation.IsOSPlatform(OSPlatform.Windows);

    public Task ShutdownAsync(string reason, TimeSpan delay) => RunAsync(ShutdownCommand.Build(reason, delay, Win), throwOnFailure: true);
    public Task CancelAsync() => RunAsync(ShutdownCommand.BuildCancel(Win), throwOnFailure: false);

    private static async Task RunAsync((string File, string[] Args) cmd, bool throwOnFailure)
    {
        var psi = new ProcessStartInfo(cmd.File) { UseShellExecute = false, CreateNoWindow = true, RedirectStandardError = true, RedirectStandardOutput = true };
        foreach (var a in cmd.Args) psi.ArgumentList.Add(a);
        try
        {
            using var p = Process.Start(psi) ?? throw new InvalidOperationException("Could not start " + cmd.File);
            await p.WaitForExitAsync();
            if (p.ExitCode != 0 && throwOnFailure)
                throw new InvalidOperationException($"{cmd.File} exited with {p.ExitCode}: {(await p.StandardError.ReadToEndAsync()).Trim()}");
        }
        catch (Exception) when (!throwOnFailure) { }
    }
}

/// <summary>Test mode: only reports what would happen, never shuts anything down.</summary>
public sealed class DryRunShutdownExecutor(Action<string> log) : IShutdownExecutor
{
    public Task ShutdownAsync(string reason, TimeSpan delay)
    { log($"TEST MODE - would shut down in {delay.TotalSeconds:0}s ({reason}). Nothing was shut down."); return Task.CompletedTask; }
    public Task CancelAsync() => Task.CompletedTask;
}
