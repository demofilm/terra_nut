namespace WinNUT.Core;

public sealed record ShutdownPolicy
{
    public bool Enabled { get; init; } = true;
    /// <summary>Shut down when on battery for at least this long (null = disabled).</summary>
    public TimeSpan? OnBatteryFor { get; init; } = TimeSpan.FromMinutes(5);
    /// <summary>Shut down when battery charge is at or below this percentage (null = disabled).</summary>
    public double? BatteryBelowPercent { get; init; } = 30;
    /// <summary>Shut down when estimated runtime is at or below this (null = disabled).</summary>
    public TimeSpan? RuntimeBelow { get; init; } = TimeSpan.FromMinutes(2);
    /// <summary>Shut down immediately on the LB (low battery) flag while on battery.</summary>
    public bool OnLowBatteryFlag { get; init; } = true;
    /// <summary>Shut down on FSD (forced shutdown from the NUT server).</summary>
    public bool OnForcedShutdown { get; init; } = true;
}

public sealed record ShutdownDecision(bool ShouldShutdown, string? Reason);

/// <summary>Pure decision logic so it can be unit tested without timers or hardware.</summary>
public static class ShutdownEvaluator
{
    public static ShutdownDecision Evaluate(ShutdownPolicy p, UpsSnapshot s, TimeSpan? timeOnBattery)
    {
        if (!p.Enabled) return new(false, null);
        var st = s.Status;
        if (p.OnForcedShutdown && st.HasFlag(UpsStatus.ForcedShutdown)) return new(true, "Forced shutdown (FSD) from NUT server");
        if (!st.HasFlag(UpsStatus.OnBattery)) return new(false, null);
        if (p.OnLowBatteryFlag && st.HasFlag(UpsStatus.LowBattery)) return new(true, "UPS reports low battery");
        if (p.OnBatteryFor is { } t && timeOnBattery >= t) return new(true, $"On battery for {timeOnBattery:mm\\:ss}");
        if (p.BatteryBelowPercent is { } pct && s.BatteryCharge is { } ch && ch <= pct) return new(true, $"Battery at {ch:0}%");
        if (p.RuntimeBelow is { } rt && s.Runtime is { } r && r <= rt) return new(true, $"Estimated runtime {r:mm\\:ss}");
        return new(false, null);
    }
}
