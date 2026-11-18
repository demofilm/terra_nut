using Avalonia.Controls;
using WinNUT.Core;

namespace WinNUT.App;

public partial class SettingsWindow : Window
{
    public AppSettings Result { get; private set; }

    public SettingsWindow() : this(new AppSettings()) { }

    public SettingsWindow(AppSettings s)
    {
        InitializeComponent();
        Result = s;
        Host.Text = s.Host; Port.Value = s.Port; Ups.Text = s.UpsName; User.Text = s.Username; Pass.Text = s.Password;
        Poll.Value = (decimal)s.PollSeconds;
        ShutEnabled.IsChecked = s.Shutdown.Enabled; ShutLb.IsChecked = s.Shutdown.OnLowBatteryFlag;
        MinOnBat.Value = (decimal)(s.Shutdown.OnBatteryFor?.TotalMinutes ?? 0);
        BatPct.Value = (decimal)(s.Shutdown.BatteryBelowPercent ?? 0);
        RtMin.Value = (decimal)(s.Shutdown.RuntimeBelow?.TotalMinutes ?? 0);
        Delay.Value = (decimal)s.ShutdownDelaySeconds;
        Tray.IsChecked = s.MinimizeToTray;
        CancelBtn.Click += (_, _) => Close(null);
        OkBtn.Click += (_, _) => { Result = Build(s); Close(Result); };
    }

    private AppSettings Build(AppSettings o)
    {
        static TimeSpan? Min(decimal? v) => v is > 0 ? TimeSpan.FromMinutes((double)v) : null;
        return new AppSettings
        {
            Host = string.IsNullOrWhiteSpace(Host.Text) ? "127.0.0.1" : Host.Text.Trim(),
            Port = (int)(Port.Value ?? 3493),
            UpsName = string.IsNullOrWhiteSpace(Ups.Text) ? "ups" : Ups.Text.Trim(),
            Username = string.IsNullOrWhiteSpace(User.Text) ? null : User.Text.Trim(),
            Password = string.IsNullOrEmpty(Pass.Text) ? null : Pass.Text,
            PollSeconds = (double)(Poll.Value ?? 2), ReconnectSeconds = o.ReconnectSeconds,
            ShutdownDelaySeconds = (double)(Delay.Value ?? 30),
            MinimizeToTray = Tray.IsChecked == true, StartMinimized = o.StartMinimized,
            Shutdown = new ShutdownPolicy
            {
                Enabled = ShutEnabled.IsChecked == true, OnLowBatteryFlag = ShutLb.IsChecked == true,
                OnBatteryFor = Min(MinOnBat.Value), RuntimeBelow = Min(RtMin.Value),
                BatteryBelowPercent = BatPct.Value is > 0 ? (double)BatPct.Value : null,
            },
        };
    }
}
