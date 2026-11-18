using System.Diagnostics;
using System.Runtime.InteropServices;

namespace WinNUT.Core;

public interface IShutdownExecutor
{
    Task ShutdownAsync(string reason, TimeSpan delay);
    Task CancelAsync();
}

/// <summary>Uses shutdown.exe on Windows and `shutdown` on Linux/macOS.</summary>
public sealed class SystemShutdownExecutor : IShutdownExecutor
{
    public Task ShutdownAsync(string reason, TimeSpan delay)
    {
        int secs = (int)Math.Max(0, delay.TotalSeconds);
        var psi = new ProcessStartInfo { UseShellExecute = false, CreateNoWindow = true };
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            psi.FileName = "shutdown";
            psi.ArgumentList.Add("/s"); psi.ArgumentList.Add("/t"); psi.ArgumentList.Add(secs.ToString());
            psi.ArgumentList.Add("/c"); psi.ArgumentList.Add("WinNUT: " + reason);
        }
        else
        {
            psi.FileName = "shutdown";
            psi.ArgumentList.Add("-h");
            psi.ArgumentList.Add(secs == 0 ? "now" : "+" + Math.Max(1, (secs + 59) / 60));
            psi.ArgumentList.Add("WinNUT: " + reason);
        }
        Process.Start(psi)?.Dispose();
        return Task.CompletedTask;
    }

    public Task CancelAsync()
    {
        var psi = new ProcessStartInfo { UseShellExecute = false, CreateNoWindow = true, FileName = "shutdown" };
        psi.ArgumentList.Add(RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "/a" : "-c");
        try { Process.Start(psi)?.Dispose(); } catch { }
        return Task.CompletedTask;
    }
}
