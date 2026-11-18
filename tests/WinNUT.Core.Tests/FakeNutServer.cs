using System.Net;
using System.Net.Sockets;
using System.Text;

namespace WinNUT.Core.Tests;

/// <summary>Minimal fake NUT server for one UPS named "ups".</summary>
public sealed class FakeNutServer : IDisposable
{
    private readonly TcpListener _l = new(IPAddress.Loopback, 0);
    private readonly CancellationTokenSource _cts = new();
    public Dictionary<string, string> Vars { get; } = new()
    {
        ["ups.status"] = "OL", ["battery.charge"] = "100", ["battery.runtime"] = "1800",
        ["ups.mfr"] = "ACME Power", ["input.voltage"] = "230.4", ["ups.load"] = "23",
    };
    public List<string> Received { get; } = [];
    public string Password = "secret";
    public int Port => ((IPEndPoint)_l.LocalEndpoint).Port;

    public FakeNutServer() { _l.Start(); _ = Task.Run(AcceptLoop); }

    private async Task AcceptLoop()
    {
        try { while (!_cts.IsCancellationRequested) _ = Task.Run(() => Serve(_l.AcceptTcpClientAsync(_cts.Token).AsTask().Result)); }
        catch { }
    }

    private async Task Serve(TcpClient c)
    {
        using var _ = c;
        var s = c.GetStream();
        using var rd = new StreamReader(s, new UTF8Encoding(false));
        using var wr = new StreamWriter(s, new UTF8Encoding(false)) { NewLine = "\n", AutoFlush = true };
        string? line;
        while ((line = await rd.ReadLineAsync()) is not null)
        {
            lock (Received) Received.Add(line);
            if (line == "LOGOUT") { await wr.WriteLineAsync("OK Goodbye"); return; }
            else if (line.StartsWith("USERNAME ")) await wr.WriteLineAsync("OK");
            else if (line.StartsWith("PASSWORD ")) await wr.WriteLineAsync(line[9..] == Password ? "OK" : "ERR ACCESS-DENIED");
            else if (line == "LIST UPS")
            { await wr.WriteLineAsync("BEGIN LIST UPS"); await wr.WriteLineAsync("UPS ups \"Main \\\"rack\\\" UPS\""); await wr.WriteLineAsync("END LIST UPS"); }
            else if (line == "LIST VAR ups")
            {
                await wr.WriteLineAsync("BEGIN LIST VAR ups");
                Dictionary<string, string> copy; lock (Vars) copy = new(Vars);
                foreach (var kv in copy) await wr.WriteLineAsync($"VAR ups {kv.Key} \"{kv.Value}\"");
                await wr.WriteLineAsync("END LIST VAR ups");
            }
            else if (line.StartsWith("GET VAR ups "))
            { var n = line[12..]; await wr.WriteLineAsync(Vars.TryGetValue(n, out var v) ? $"VAR ups {n} \"{v}\"" : "ERR VAR-NOT-SUPPORTED"); }
            else await wr.WriteLineAsync("ERR UNKNOWN-COMMAND");
        }
    }
    public void Dispose() { _cts.Cancel(); _l.Stop(); }
}
