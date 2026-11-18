using System.Text.Json;
using System.Text.Json.Serialization;

namespace WinNUT.Core;

public sealed class AppSettings
{
    public string Host { get; set; } = "127.0.0.1";
    public int Port { get; set; } = 3493;
    public string UpsName { get; set; } = "ups";
    public string? Username { get; set; }
    public string? Password { get; set; }
    public double PollSeconds { get; set; } = 2;
    public double ReconnectSeconds { get; set; } = 5;
    public double ShutdownDelaySeconds { get; set; } = 30;
    public bool MinimizeToTray { get; set; } = true;
    public bool StartMinimized { get; set; }
    public ShutdownPolicy Shutdown { get; set; } = new();

    public MonitorOptions ToMonitorOptions() => new(
        new NutServerOptions(Host, Port, Username, Password), UpsName,
        TimeSpan.FromSeconds(Math.Max(0.5, PollSeconds)), TimeSpan.FromSeconds(Math.Max(1, ReconnectSeconds)),
        Shutdown, TimeSpan.FromSeconds(Math.Max(0, ShutdownDelaySeconds)));

    private static readonly JsonSerializerOptions Json = new() { WriteIndented = true, Converters = { new TimeSpanConverter() } };

    public static string DefaultPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "WinNUT2", "settings.json");

    public static AppSettings Load(string? path = null)
    {
        path ??= DefaultPath;
        try { return File.Exists(path) ? JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(path), Json) ?? new() : new(); }
        catch (Exception ex) when (ex is JsonException or IOException) { return new(); }
    }

    public void Save(string? path = null)
    {
        path ??= DefaultPath;
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var tmp = path + ".tmp";
        File.WriteAllText(tmp, JsonSerializer.Serialize(this, Json));
        File.Move(tmp, path, overwrite: true);
    }

    private sealed class TimeSpanConverter : JsonConverter<TimeSpan>
    {
        public override TimeSpan Read(ref Utf8JsonReader r, Type t, JsonSerializerOptions o) => TimeSpan.FromSeconds(r.GetDouble());
        public override void Write(Utf8JsonWriter w, TimeSpan v, JsonSerializerOptions o) => w.WriteNumberValue(v.TotalSeconds);
    }
}
