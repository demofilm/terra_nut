namespace WinNUT.Core;

[Flags]
public enum UpsStatus
{
    None = 0, Online = 1, OnBattery = 2, LowBattery = 4, HighBattery = 8,
    Replace = 16, Charging = 32, Discharging = 64, Bypass = 128, Overload = 256,
    Calibrating = 512, Off = 1024, Trim = 2048, Boost = 4096, ForcedShutdown = 8192,
}

/// <summary>Immutable snapshot of the variables of one UPS.</summary>
public sealed class UpsSnapshot(IReadOnlyDictionary<string, string> vars, DateTimeOffset timestamp)
{
    public IReadOnlyDictionary<string, string> Variables { get; } = vars;
    public DateTimeOffset Timestamp { get; } = timestamp;

    public string? Get(string name) => Variables.TryGetValue(name, out var v) ? v : null;
    public double? GetNumber(string name) => NutProtocol.ToDouble(Get(name));

    public string? Manufacturer => Get("device.mfr") ?? Get("ups.mfr");
    public string? Model => Get("device.model") ?? Get("ups.model");
    public double? BatteryCharge => GetNumber("battery.charge");
    public double? BatteryVoltage => GetNumber("battery.voltage");
    public TimeSpan? Runtime => GetNumber("battery.runtime") is { } s ? TimeSpan.FromSeconds(s) : null;
    public double? InputVoltage => GetNumber("input.voltage");
    public double? InputFrequency => GetNumber("input.frequency");
    public double? OutputVoltage => GetNumber("output.voltage");
    public double? Load => GetNumber("ups.load");
    public double? Power => GetNumber("ups.power") ?? GetNumber("ups.realpower");
    public double? LowBatteryThreshold => GetNumber("battery.charge.low");
    public double? NominalPower => GetNumber("ups.realpower.nominal") ?? GetNumber("ups.power.nominal");
    public double? BatteryVoltageNominal => GetNumber("battery.voltage.nominal");
    /// <summary>Reported power, or load% x nominal power when the UPS does not report watts.</summary>
    public double? EstimatedPower => Power ?? (Load is { } l && NominalPower is { } n ? l * n / 100 : null);

    public UpsStatus Status => ParseStatus(Get("ups.status"));

    public static UpsStatus ParseStatus(string? raw)
    {
        var st = UpsStatus.None;
        if (string.IsNullOrWhiteSpace(raw)) return st;
        foreach (var t in raw.Split(' ', StringSplitOptions.RemoveEmptyEntries))
            st |= t.ToUpperInvariant() switch
            {
                "OL" => UpsStatus.Online, "OB" => UpsStatus.OnBattery, "LB" => UpsStatus.LowBattery,
                "HB" => UpsStatus.HighBattery, "RB" => UpsStatus.Replace, "CHRG" => UpsStatus.Charging,
                "DISCHRG" => UpsStatus.Discharging, "BYPASS" => UpsStatus.Bypass, "OVER" => UpsStatus.Overload,
                "CAL" => UpsStatus.Calibrating, "OFF" => UpsStatus.Off, "TRIM" => UpsStatus.Trim,
                "BOOST" => UpsStatus.Boost, "FSD" => UpsStatus.ForcedShutdown, _ => UpsStatus.None,
            };
        return st;
    }
}

