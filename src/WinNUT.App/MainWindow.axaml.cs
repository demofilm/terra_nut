using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Threading;
using WinNUT.Core;

namespace WinNUT.App;

public partial class MainWindow : Window
{
    private readonly List<string> _log = [];
    public event Action? SettingsRequested;

    public MainWindow()
    {
        InitializeComponent();
        SettingsBtn.Click += (_, _) => SettingsRequested?.Invoke();
    }

    public void SetConnection(ConnectionState s, string? detail) => Dispatcher.UIThread.Post(() =>
    {
        StateDot.Fill = s switch
        {
            ConnectionState.Connected => Brushes.SeaGreen, ConnectionState.Connecting => Brushes.Orange,
            ConnectionState.Error => Brushes.IndianRed, _ => Brushes.Gray,
        };
        StateText.Text = s + (detail is null ? "" : ": " + detail);
        if (s is ConnectionState.Connected or ConnectionState.Error) Log(StateText.Text);
        if (s != ConnectionState.Connected) { foreach (var g in Gauges) g.HasValue = false; Scanner.Active = false; }
    });

    private IEnumerable<ArcGauge> Gauges => [GBattery, GLoad, GRuntime, GInV, GOutV, GFreq];

    public void SetSnapshot(UpsSnapshot s) => Dispatcher.UIThread.Post(() =>
    {
        UpsTitle.Text = string.Join(' ', new[] { s.Manufacturer, s.Model }.Where(x => !string.IsNullOrWhiteSpace(x))) is { Length: > 0 } t ? t : "UPS";
        Set(GBattery, s.BatteryCharge); Set(GLoad, s.Load); Set(GRuntime, s.Runtime?.TotalMinutes);
        if (s.InputVoltageNominal is { } inom)
        { GInV.Nominal = inom; GInV.Minimum = s.InputTransferLow ?? inom * 0.75; GInV.Maximum = s.InputTransferHigh ?? inom * 1.3; }
        else { GInV.Nominal = double.NaN; GInV.Minimum = 0; GInV.Maximum = 300; }
        Set(GInV, s.InputVoltage);
        // Many UPSes (e.g. APC Back-UPS via usbhid) do not report output voltage / frequency: fall back to what they do report.
        if (s.OutputVoltage is null && s.EstimatedPower is { } pw)
        {
            GOutV.Title = "Power"; GOutV.Unit = "W"; GOutV.Minimum = 0; GOutV.Maximum = s.NominalPower ?? Math.Max(1000, pw * 1.5);
            GOutV.InvertColors = true; GOutV.Nominal = double.NaN; Set(GOutV, pw);
        }
        else
        {
            GOutV.Title = "Output"; GOutV.Unit = "V"; GOutV.InvertColors = false;
            if (s.OutputVoltageNominal is { } onom) { GOutV.Nominal = onom; GOutV.Minimum = s.InputTransferLow ?? onom * 0.75; GOutV.Maximum = s.InputTransferHigh ?? onom * 1.3; }
            else { GOutV.Nominal = double.NaN; GOutV.Minimum = 0; GOutV.Maximum = 300; }
            Set(GOutV, s.OutputVoltage);
        }
        if (s.InputFrequency is null && s.BatteryVoltage is { } bv)
        {
            GFreq.Title = "Battery"; GFreq.Unit = "V"; GFreq.Decimals = 1; GFreq.Minimum = 0;
            GFreq.Maximum = (s.BatteryVoltageNominal ?? 24) * 1.3; Set(GFreq, bv);
        }
        else { GFreq.Title = "Frequency"; GFreq.Unit = "Hz"; GFreq.Decimals = 1; GFreq.Minimum = 0; GFreq.Maximum = 70; Set(GFreq, s.InputFrequency); }
        var ob = s.Status.HasFlag(UpsStatus.OnBattery);
        Scanner.Active = true; Scanner.Warn = ob || s.Status.HasFlag(UpsStatus.LowBattery); Scanner.Speed = ob ? 2.2 : 1.0;
        StatusLine.Text = Describe(s.Status);
    });

    private static void Set(ArcGauge g, double? v) { g.HasValue = v.HasValue; if (v is { } x) g.Value = x; }

    public static string Describe(UpsStatus st)
    {
        var parts = new List<string>();
        if (st.HasFlag(UpsStatus.OnBattery)) parts.Add("On battery");
        else if (st.HasFlag(UpsStatus.Online)) parts.Add("Online (mains power)");
        if (st.HasFlag(UpsStatus.LowBattery)) parts.Add("LOW BATTERY");
        if (st.HasFlag(UpsStatus.Charging)) parts.Add("charging");
        if (st.HasFlag(UpsStatus.Replace)) parts.Add("replace battery");
        if (st.HasFlag(UpsStatus.Overload)) parts.Add("OVERLOAD");
        if (st.HasFlag(UpsStatus.ForcedShutdown)) parts.Add("forced shutdown");
        return parts.Count == 0 ? "Status unknown" : string.Join(" · ", parts);
    }

    public void Log(string msg) => Dispatcher.UIThread.Post(() =>
    {
        _log.Add($"{DateTime.Now:HH:mm:ss}  {msg}");
        if (_log.Count > 200) _log.RemoveAt(0);
        LogText.Text = string.Join('\n', _log.TakeLast(50));
    });
}


