namespace WinNUT.Core;

public enum ConnectionState { Disconnected, Connecting, Connected, Error }

public sealed record MonitorOptions(NutServerOptions Server, string UpsName = "ups", TimeSpan? PollInterval = null,
    TimeSpan? ReconnectDelay = null, ShutdownPolicy? Shutdown = null, TimeSpan? ShutdownDelay = null);

/// <summary>Polls a NUT server, raises events, reconnects on failure and applies the shutdown policy.</summary>
public sealed class UpsMonitor : IAsyncDisposable
{
    private readonly MonitorOptions _o;
    private readonly Func<NutServerOptions, NutClient> _factory;
    private readonly IShutdownExecutor _shutdown;
    private CancellationTokenSource? _cts;
    private Task? _loop;
    private DateTimeOffset? _onBatterySince;
    private bool _shutdownIssued;

    public UpsMonitor(MonitorOptions options, IShutdownExecutor shutdown, Func<NutServerOptions, NutClient>? factory = null)
    { _o = options; _shutdown = shutdown; _factory = factory ?? (s => new NutClient(s)); }

    public event Action<ConnectionState, string?>? ConnectionChanged;
    public event Action<UpsSnapshot>? SnapshotUpdated;
    public event Action<UpsStatus, UpsStatus>? StatusChanged;
    public event Action<string>? ShutdownTriggered;

    public UpsSnapshot? Last { get; private set; }

    public void Start()
    {
        if (_loop is not null) return;
        _cts = new CancellationTokenSource();
        _loop = Task.Run(() => RunAsync(_cts.Token));
    }

    public async Task StopAsync()
    {
        if (_cts is null) return;
        _cts.Cancel();
        try { await _loop!; } catch (OperationCanceledException) { }
        _cts.Dispose(); _cts = null; _loop = null;
    }

    private async Task RunAsync(CancellationToken ct)
    {
        var poll = _o.PollInterval ?? TimeSpan.FromSeconds(2);
        var retry = _o.ReconnectDelay ?? TimeSpan.FromSeconds(5);
        while (!ct.IsCancellationRequested)
        {
            await using var client = _factory(_o.Server);
            try
            {
                ConnectionChanged?.Invoke(ConnectionState.Connecting, null);
                await client.ConnectAsync(ct);
                ConnectionChanged?.Invoke(ConnectionState.Connected, null);
                while (!ct.IsCancellationRequested)
                {
                    Process(await client.GetSnapshotAsync(_o.UpsName, ct));
                    await Task.Delay(poll, ct);
                }
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { break; }
            catch (Exception ex) when (ex is NutException or IOException or System.Net.Sockets.SocketException)
            {
                ConnectionChanged?.Invoke(ConnectionState.Error, ex.Message);
            }
            ConnectionChanged?.Invoke(ConnectionState.Disconnected, null);
            try { await Task.Delay(retry, ct); } catch (OperationCanceledException) { break; }
        }
        ConnectionChanged?.Invoke(ConnectionState.Disconnected, null);
    }

    internal void Process(UpsSnapshot snap)
    {
        var prev = Last?.Status;
        Last = snap;
        var st = snap.Status;
        if (st.HasFlag(UpsStatus.OnBattery)) _onBatterySince ??= snap.Timestamp;
        else { _onBatterySince = null; _shutdownIssued = false; }
        SnapshotUpdated?.Invoke(snap);
        if (prev is { } p && p != st) StatusChanged?.Invoke(p, st);

        if (_o.Shutdown is { } policy && !_shutdownIssued)
        {
            var onBat = _onBatterySince is { } since ? snap.Timestamp - since : (TimeSpan?)null;
            var d = ShutdownEvaluator.Evaluate(policy, snap, onBat);
            if (d.ShouldShutdown)
            {
                _shutdownIssued = true;
                ShutdownTriggered?.Invoke(d.Reason!);
                _ = _shutdown.ShutdownAsync(d.Reason!, _o.ShutdownDelay ?? TimeSpan.FromSeconds(30));
            }
        }
    }

    public async ValueTask DisposeAsync() => await StopAsync();
}
