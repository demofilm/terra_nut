using System.Net.Sockets;
using System.Text;

namespace WinNUT.Core;

public sealed record NutServerOptions(string Host = "127.0.0.1", int Port = 3493, string? Username = null, string? Password = null,
    TimeSpan? Timeout = null);

/// <summary>Async client for the NUT (Network UPS Tools) TCP protocol.</summary>
public sealed class NutClient : IAsyncDisposable
{
    private readonly NutServerOptions _opt;
    private TcpClient? _tcp;
    private StreamReader? _reader;
    private StreamWriter? _writer;
    private readonly SemaphoreSlim _lock = new(1, 1);

    public NutClient(NutServerOptions options) => _opt = options;
    public bool IsConnected => _tcp?.Connected == true;
    private TimeSpan Timeout => _opt.Timeout ?? TimeSpan.FromSeconds(5);

    public async Task ConnectAsync(CancellationToken ct = default)
    {
        await DisconnectAsync();
        var tcp = new TcpClient { NoDelay = true };
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(Timeout);
        try { await tcp.ConnectAsync(_opt.Host, _opt.Port, cts.Token); }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        { tcp.Dispose(); throw new NutException($"Timeout connecting to {_opt.Host}:{_opt.Port}"); }
        catch { tcp.Dispose(); throw; }
        _tcp = tcp;
        var ns = tcp.GetStream();
        var enc = new UTF8Encoding(false);
        _reader = new StreamReader(ns, enc);
        _writer = new StreamWriter(ns, enc) { NewLine = "\n", AutoFlush = true };
        if (!string.IsNullOrEmpty(_opt.Username))
        {
            await ExpectOkAsync($"USERNAME {_opt.Username}", ct);
            if (!string.IsNullOrEmpty(_opt.Password)) await ExpectOkAsync($"PASSWORD {_opt.Password}", ct);
        }
    }

    public async Task<IReadOnlyList<(string Name, string Description)>> ListUpsAsync(CancellationToken ct = default)
    {
        var rows = await ListAsync("LIST UPS", "UPS", ct);
        return rows.Select(r => (r[0], r.Count > 1 ? r[1] : "")).ToList();
    }

    public async Task<UpsSnapshot> GetSnapshotAsync(string ups, CancellationToken ct = default)
    {
        var rows = await ListAsync($"LIST VAR {ups}", $"VAR {ups}", ct);
        var d = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var r in rows) if (r.Count >= 2) d[r[0]] = r[1];
        return new UpsSnapshot(d, DateTimeOffset.Now);
    }

    public async Task<string> GetVarAsync(string ups, string name, CancellationToken ct = default)
    {
        var line = await SendAsync($"GET VAR {ups} {name}", ct);
        var t = NutProtocol.Tokenize(line);
        if (t.Count < 4 || t[0] != "VAR") throw new NutException("Unexpected reply: " + line);
        return t[3];
    }

    public async Task<IReadOnlyList<string>> ListCommandsAsync(string ups, CancellationToken ct = default) =>
        (await ListAsync($"LIST CMD {ups}", $"CMD {ups}", ct)).Select(r => r[0]).ToList();

    public async Task InstCmdAsync(string ups, string cmd, CancellationToken ct = default) =>
        await ExpectOkAsync($"INSTCMD {ups} {cmd}", ct);

    private async Task ExpectOkAsync(string cmd, CancellationToken ct)
    {
        var l = await SendAsync(cmd, ct);
        if (!l.StartsWith("OK", StringComparison.Ordinal)) throw new NutException("Unexpected reply: " + l);
    }

    /// <summary>Sends one command and returns the first reply line; ERR lines become NutException.</summary>
    private async Task<string> SendAsync(string cmd, CancellationToken ct)
    {
        await _lock.WaitAsync(ct);
        try { return await SendCoreAsync(cmd, ct); }
        finally { _lock.Release(); }
    }

    private async Task<string> SendCoreAsync(string cmd, CancellationToken ct)
    {
        if (_writer is null || _reader is null) throw new NutException("Not connected");
        await _writer.WriteLineAsync(cmd.AsMemory(), ct);
        return await ReadLineAsync(ct);
    }

    private async Task<string> ReadLineAsync(CancellationToken ct)
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(Timeout);
        string? line;
        try { line = await _reader!.ReadLineAsync(cts.Token); }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested) { throw new NutException("Timeout waiting for server"); }
        if (line is null) throw new NutException("Connection closed by server");
        if (line.StartsWith("ERR ", StringComparison.Ordinal))
            throw new NutException(line, line[4..].Split(' ')[0]);
        return line;
    }

    /// <summary>Runs a LIST command; returns the tokens after the BEGIN prefix for each row.</summary>
    private async Task<List<List<string>>> ListAsync(string cmd, string prefix, CancellationToken ct)
    {
        await _lock.WaitAsync(ct);
        try
        {
            var first = await SendCoreAsync(cmd, ct);
            if (first != "BEGIN " + cmd) throw new NutException("Unexpected reply: " + first);
            var rows = new List<List<string>>();
            var skip = prefix.Split(' ').Length;
            while (true)
            {
                var line = await ReadLineAsync(ct);
                if (line == "END " + cmd) break;
                var t = NutProtocol.Tokenize(line);
                if (t.Count > skip && t[0] == prefix.Split(' ')[0]) rows.Add(t.Skip(skip).ToList());
            }
            return rows;
        }
        finally { _lock.Release(); }
    }

    public async Task DisconnectAsync()
    {
        var tcp = _tcp; _tcp = null;
        if (tcp is null) return;
        try { if (tcp.Connected && _writer is not null) await _writer.WriteLineAsync("LOGOUT"); } catch { }
        _reader = null; _writer = null;
        tcp.Dispose();
    }

    public async ValueTask DisposeAsync() => await DisconnectAsync();
}
