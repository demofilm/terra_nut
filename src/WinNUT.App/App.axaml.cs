using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using WinNUT.Core;

namespace WinNUT.App;

public partial class App : Application
{
    private AppSettings _settings = AppSettings.Load();
    private UpsMonitor? _monitor;
    private MainWindow? _main;
    private TrayIcon? _tray;
    private readonly SystemShutdownExecutor _shutdown = new();
    private bool _quitting;

    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.ShutdownMode = ShutdownMode.OnExplicitShutdown;
            _main = new MainWindow();
            _main.SettingsRequested += OpenSettings;
            _main.Closing += (_, e) =>
            {
                if (!_quitting && _settings.MinimizeToTray) { e.Cancel = true; _main.Hide(); }
                else _ = StopAndExit(desktop);
            };
            desktop.MainWindow = _main;
            if (_settings.StartMinimized) _main.WindowState = WindowState.Minimized;
            SetupTray(desktop);
            StartMonitor();
        }
        base.OnFrameworkInitializationCompleted();
    }

    private void SetupTray(IClassicDesktopStyleApplicationLifetime desktop)
    {
        var show = new NativeMenuItem("Show");
        show.Click += (_, _) => ShowMain();
        var cancel = new NativeMenuItem("Cancel pending shutdown");
        cancel.Click += async (_, _) => { await _shutdown.CancelAsync(); _main?.Log("Shutdown cancelled by user"); };
        var quit = new NativeMenuItem("Quit");
        quit.Click += (_, _) => { _quitting = true; _ = StopAndExit(desktop); };
        _tray = new TrayIcon
        {
            Icon = MakeIcon(Colors.SeaGreen), ToolTipText = "WinNUT 2.0",
            Menu = new NativeMenu { show, cancel, new NativeMenuItemSeparator(), quit },
        };
        _tray.Clicked += (_, _) => ShowMain();
        TrayIcon.SetIcons(this, [_tray]);
    }

    private void ShowMain()
    {
        if (_main is null) return;
        _main.Show();
        if (_main.WindowState == WindowState.Minimized) _main.WindowState = WindowState.Normal;
        _main.Activate();
    }

    private static WindowIcon MakeIcon(Color c)
    {
        var bmp = new RenderTargetBitmap(new PixelSize(32, 32));
        using (var ctx = bmp.CreateDrawingContext())
        {
            ctx.DrawEllipse(new SolidColorBrush(c), null, new Point(16, 16), 14, 14);
            ctx.DrawRectangle(Brushes.White, null, new Rect(13, 7, 6, 18), 2);
        }
        return new WindowIcon(bmp);
    }

    private void StartMonitor()
    {
        _ = StopMonitorAsync();
        IShutdownExecutor exec = _settings.ShutdownDryRun ? new DryRunShutdownExecutor(msg => _main?.Log(msg)) : _shutdown;
               var m = new UpsMonitor(_settings.ToMonitorOptions(), exec);
               m.ShutdownFailed += err => _main?.Log("SHUTDOWN FAILED: " + err);
        m.ConnectionChanged += (s, d) => { _main?.SetConnection(s, d); SetTray(s == ConnectionState.Connected ? Colors.SeaGreen : Colors.Gray, $"WinNUT: {s}"); };
        m.SnapshotUpdated += s =>
        {
            _main?.SetSnapshot(s);
            var ob = s.Status.HasFlag(UpsStatus.OnBattery);
            SetTray(ob ? Colors.Orange : Colors.SeaGreen, $"{MainWindow.Describe(s.Status)} — {s.BatteryCharge:0}%");
        };
        m.StatusChanged += (a, b) => _main?.Log($"Status: {MainWindow.Describe(a)} → {MainWindow.Describe(b)}");
        m.ShutdownTriggered += r => { if (!_settings.ShutdownDryRun) _main?.Log($"SHUTDOWN in {_settings.ShutdownDelaySeconds:0}s: {r}"); SetTray(Colors.IndianRed, (_settings.ShutdownDryRun ? "Shutdown (test mode): " : "Shutdown pending: ") + r); };
        _monitor = m;
        m.Start();
        _main?.Log($"Monitoring {_settings.UpsName}@{_settings.Host}:{_settings.Port}" + (_settings.ShutdownDryRun ? "  [shutdown TEST MODE]" : ""));
    }

    private void SetTray(Color c, string tip) => Dispatcher.UIThread.Post(() =>
    { if (_tray is null) return; _tray.Icon = MakeIcon(c); _tray.ToolTipText = tip.Length > 120 ? tip[..120] : tip; });

    private async Task StopMonitorAsync() { var m = _monitor; _monitor = null; if (m is not null) await m.DisposeAsync(); }

    private async void OpenSettings()
    {
        if (_main is null) return;
        var dlg = new SettingsWindow(_settings);
        var res = await dlg.ShowDialog<AppSettings?>(_main);
        if (res is null) return;
        _settings = res;
        try { _settings.Save(); } catch (Exception ex) { _main.Log("Could not save settings: " + ex.Message); }
        await StopMonitorAsync();
        StartMonitor();
    }

    private async Task StopAndExit(IClassicDesktopStyleApplicationLifetime desktop)
    {
        _quitting = true;
        await StopMonitorAsync();
        _tray?.Dispose();
        Dispatcher.UIThread.Post(() => desktop.Shutdown());
    }
}


