using Xunit;

namespace WinNUT.Core.Tests;

public class ProtocolTests
{
    [Fact] public void Tokenize_handles_quotes_and_escapes() =>
        Assert.Equal(["VAR", "ups", "ups.model", "A \"B\" C"], NutProtocol.Tokenize("VAR ups ups.model \"A \\\"B\\\" C\""));

    [Fact] public void Tokenize_keeps_empty_quoted_value() =>
        Assert.Equal(["VAR", "ups", "x", ""], NutProtocol.Tokenize("VAR ups x \"\""));

    [Theory]
    [InlineData("OL CHRG", UpsStatus.Online | UpsStatus.Charging)]
    [InlineData("OB DISCHRG LB", UpsStatus.OnBattery | UpsStatus.Discharging | UpsStatus.LowBattery)]
    [InlineData("", UpsStatus.None)]
    [InlineData("ol", UpsStatus.Online)]
    public void ParseStatus(string raw, UpsStatus expected) => Assert.Equal(expected, UpsSnapshot.ParseStatus(raw));
}

public class ClientTests
{
    [Fact]
    public async Task Lists_ups_and_reads_snapshot()
    {
        using var srv = new FakeNutServer();
        await using var c = new NutClient(new("127.0.0.1", srv.Port, "mon", "secret"));
        await c.ConnectAsync();
        var ups = await c.ListUpsAsync();
        Assert.Equal(("ups", "Main \"rack\" UPS"), ups.Single());
        var snap = await c.GetSnapshotAsync("ups");
        Assert.Equal(100, snap.BatteryCharge);
        Assert.Equal(TimeSpan.FromMinutes(30), snap.Runtime);
        Assert.Equal("ACME Power", snap.Manufacturer);
        Assert.Equal(230.4, snap.InputVoltage);
        Assert.Equal(UpsStatus.Online, snap.Status);
        Assert.Equal("23", await c.GetVarAsync("ups", "ups.load"));
    }

    [Fact]
    public async Task Wrong_password_throws_access_denied()
    {
        using var srv = new FakeNutServer();
        await using var c = new NutClient(new("127.0.0.1", srv.Port, "mon", "nope"));
        var ex = await Assert.ThrowsAsync<NutException>(() => c.ConnectAsync());
        Assert.Equal("ACCESS-DENIED", ex.Code);
    }

    [Fact]
    public async Task Unknown_var_throws_with_code()
    {
        using var srv = new FakeNutServer();
        await using var c = new NutClient(new("127.0.0.1", srv.Port));
        await c.ConnectAsync();
        var ex = await Assert.ThrowsAsync<NutException>(() => c.GetVarAsync("ups", "nope"));
        Assert.Equal("VAR-NOT-SUPPORTED", ex.Code);
    }

    [Fact]
    public async Task Connection_refused_surfaces_error()
    {
        int port; using (var l = new System.Net.Sockets.TcpListener(System.Net.IPAddress.Loopback, 0)) { l.Start(); port = ((System.Net.IPEndPoint)l.LocalEndpoint).Port; }
        await using var c = new NutClient(new("127.0.0.1", port));
        await Assert.ThrowsAnyAsync<Exception>(() => c.ConnectAsync());
    }
}

public class ShutdownTests
{
    private static UpsSnapshot Snap(string status, double? charge = null, double? runtime = null)
    {
        var d = new Dictionary<string, string> { ["ups.status"] = status };
        if (charge is { } c) d["battery.charge"] = c.ToString(System.Globalization.CultureInfo.InvariantCulture);
        if (runtime is { } r) d["battery.runtime"] = r.ToString(System.Globalization.CultureInfo.InvariantCulture);
        return new UpsSnapshot(d, DateTimeOffset.Now);
    }

    [Fact] public void Online_never_shuts_down() =>
        Assert.False(ShutdownEvaluator.Evaluate(new(), Snap("OL", 5, 10), TimeSpan.FromHours(1)).ShouldShutdown);

    [Fact] public void Low_battery_flag_on_battery_shuts_down() =>
        Assert.True(ShutdownEvaluator.Evaluate(new(), Snap("OB LB"), TimeSpan.Zero).ShouldShutdown);

    [Fact] public void Charge_threshold() =>
        Assert.True(ShutdownEvaluator.Evaluate(new(), Snap("OB", 25, 9999), TimeSpan.Zero).ShouldShutdown);

    [Fact] public void Healthy_on_battery_does_not_shut_down() =>
        Assert.False(ShutdownEvaluator.Evaluate(new(), Snap("OB", 90, 1800), TimeSpan.FromSeconds(30)).ShouldShutdown);

    [Fact] public void Time_on_battery() =>
        Assert.True(ShutdownEvaluator.Evaluate(new(), Snap("OB", 90, 1800), TimeSpan.FromMinutes(6)).ShouldShutdown);

    [Fact] public void Fsd_shuts_down_even_when_online() =>
        Assert.True(ShutdownEvaluator.Evaluate(new(), Snap("OL FSD"), null).ShouldShutdown);

    [Fact] public void Disabled_policy() =>
        Assert.False(ShutdownEvaluator.Evaluate(new ShutdownPolicy { Enabled = false }, Snap("OB LB"), TimeSpan.Zero).ShouldShutdown);
}

public class MonitorTests
{
    private sealed class Rec : IShutdownExecutor
    {
        public List<string> Calls = [];
        public Task ShutdownAsync(string reason, TimeSpan delay) { Calls.Add(reason); return Task.CompletedTask; }
        public Task CancelAsync() => Task.CompletedTask;
    }

    [Fact]
    public async Task Monitor_reports_status_change_and_triggers_shutdown_once()
    {
        using var srv = new FakeNutServer();
        var rec = new Rec();
        var opts = new MonitorOptions(new("127.0.0.1", srv.Port), "ups", TimeSpan.FromMilliseconds(50), TimeSpan.FromMilliseconds(50),
            new ShutdownPolicy(), TimeSpan.Zero);
        await using var m = new UpsMonitor(opts, rec);
        var changed = new TaskCompletionSource<(UpsStatus, UpsStatus)>();
        var down = new TaskCompletionSource<string>();
        m.StatusChanged += (a, b) => changed.TrySetResult((a, b));
        m.ShutdownTriggered += r => down.TrySetResult(r);
        m.Start();
        await Task.Delay(300);
        lock (srv.Vars) { srv.Vars["ups.status"] = "OB LB"; }
        Assert.Equal((UpsStatus.Online, UpsStatus.OnBattery | UpsStatus.LowBattery), await changed.Task.WaitAsync(TimeSpan.FromSeconds(5)));
        Assert.Contains("low battery", await down.Task.WaitAsync(TimeSpan.FromSeconds(5)), StringComparison.OrdinalIgnoreCase);
        await Task.Delay(300);
        Assert.Single(rec.Calls);
    }

    [Fact]
    public async Task Monitor_reconnects_after_server_comes_up()
    {
        int port; using (var l = new System.Net.Sockets.TcpListener(System.Net.IPAddress.Loopback, 0)) { l.Start(); port = ((System.Net.IPEndPoint)l.LocalEndpoint).Port; }
        var opts = new MonitorOptions(new("127.0.0.1", port), "ups", TimeSpan.FromMilliseconds(50), TimeSpan.FromMilliseconds(100));
        await using var m = new UpsMonitor(opts, new Rec());
        var errored = new TaskCompletionSource(); var snap = new TaskCompletionSource<UpsSnapshot>();
        m.ConnectionChanged += (s, _) => { if (s == ConnectionState.Error) errored.TrySetResult(); };
        m.SnapshotUpdated += s => snap.TrySetResult(s);
        m.Start();
        await errored.Task.WaitAsync(TimeSpan.FromSeconds(5));
        // start a server on the same port
        var l2 = new System.Net.Sockets.TcpListener(System.Net.IPAddress.Loopback, port); l2.Start();
        _ = Task.Run(async () => { using var c = await l2.AcceptTcpClientAsync(); var s = c.GetStream(); using var rd = new StreamReader(s); using var wr = new StreamWriter(s) { NewLine = "\n", AutoFlush = true };
            while (await rd.ReadLineAsync() is { } ln) if (ln == "LIST VAR ups") { await wr.WriteLineAsync("BEGIN LIST VAR ups"); await wr.WriteLineAsync("VAR ups ups.status \"OL\""); await wr.WriteLineAsync("END LIST VAR ups"); } });
        Assert.Equal(UpsStatus.Online, (await snap.Task.WaitAsync(TimeSpan.FromSeconds(5))).Status);
        l2.Stop();
    }
}

public class SettingsTests
{
    [Fact]
    public void Roundtrip()
    {
        var p = Path.Combine(Path.GetTempPath(), "winnut-test-" + Guid.NewGuid() + ".json");
        try
        {
            new AppSettings { Host = "nas", Port = 3494, Shutdown = new ShutdownPolicy { BatteryBelowPercent = 42, RuntimeBelow = null } }.Save(p);
            var s = AppSettings.Load(p);
            Assert.Equal("nas", s.Host); Assert.Equal(3494, s.Port);
            Assert.Equal(42, s.Shutdown.BatteryBelowPercent); Assert.Null(s.Shutdown.RuntimeBelow);
            Assert.Equal(TimeSpan.FromMinutes(5), s.Shutdown.OnBatteryFor);
        }
        finally { File.Delete(p); }
    }

    [Fact] public void Corrupt_file_falls_back_to_defaults()
    {
        var p = Path.Combine(Path.GetTempPath(), "winnut-test-" + Guid.NewGuid() + ".json");
        File.WriteAllText(p, "{ not json");
        try { Assert.Equal(3493, AppSettings.Load(p).Port); } finally { File.Delete(p); }
    }
}
