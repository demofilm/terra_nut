using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace WinNUT.Core;

public sealed class AppSettings
{
    public string Host { get; set; } = "127.0.0.1";
    public int Port { get; set; } = 3493;
    public string UpsName { get; set; } = "ups";
    public string? Username { get; set; }

    /// <summary>Plain text in memory only; stored encrypted (see <see cref="PasswordEnc"/>).</summary>
    [JsonIgnore] public string? Password { get; set; }

    /// <summary>On disk: "dpapi:&lt;base64&gt;" on Windows (current user), "plain:&lt;text&gt;" elsewhere (file is chmod 600).</summary>
    public string? PasswordEnc { get => PasswordProtector.Protect(Password); set => Password = PasswordProtector.Unprotect(value); }

    /// <summary>Read-only compatibility with settings files from before the password was encrypted.</summary>
    [JsonPropertyName("Password"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? LegacyPassword { get => null; set { if (!string.IsNullOrEmpty(value)) Password = value; } }

    public double PollSeconds { get; set; } = 2;
    public double ReconnectSeconds { get; set; } = 5;
    public double ShutdownDelaySeconds { get; set; } = 30;
    public bool MinimizeToTray { get; set; } = true;
    public bool StartMinimized { get; set; }
    /// <summary>Test mode: evaluate the shutdown policy and log the result, but never shut down.</summary>
    public bool ShutdownDryRun { get; set; }
    public ShutdownPolicy Shutdown { get; set; } = new();

    public MonitorOptions ToMonitorOptions() => new(
        new NutServerOptions(Host, Port, Username, Password), UpsName,
        TimeSpan.FromSeconds(Math.Max(0.5, PollSeconds)), TimeSpan.FromSeconds(Math.Max(1, ReconnectSeconds)),
        Shutdown, TimeSpan.FromSeconds(Math.Max(0, ShutdownDelaySeconds)));

    private static readonly JsonSerializerOptions Json = new() { WriteIndented = true, Converters = { new TimeSpanConverter() } };

    /// <summary>%APPDATA%\WinNUT2\settings.json, or the file named by the WINNUT_SETTINGS environment variable.</summary>
    public static string DefaultPath => Environment.GetEnvironmentVariable("WINNUT_SETTINGS") is { Length: > 0 } p ? p : Path.Combine(
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
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        var tmp = path + ".tmp";
        File.WriteAllText(tmp, JsonSerializer.Serialize(this, Json));
        if (!OperatingSystem.IsWindows()) File.SetUnixFileMode(tmp, UnixFileMode.UserRead | UnixFileMode.UserWrite);
        File.Move(tmp, path, overwrite: true);
    }

    private sealed class TimeSpanConverter : JsonConverter<TimeSpan>
    {
        public override TimeSpan Read(ref Utf8JsonReader r, Type t, JsonSerializerOptions o) => TimeSpan.FromSeconds(r.GetDouble());
        public override void Write(Utf8JsonWriter w, TimeSpan v, JsonSerializerOptions o) => w.WriteNumberValue(v.TotalSeconds);
    }
}

public static class PasswordProtector
{
    private static readonly byte[] Entropy = "WinNUT2.settings.v1"u8.ToArray();

    public static string? Protect(string? plain)
    {
        if (string.IsNullOrEmpty(plain)) return null;
        if (OperatingSystem.IsWindows())
            return "dpapi:" + Convert.ToBase64String(ProtectedData.Protect(Encoding.UTF8.GetBytes(plain), Entropy, DataProtectionScope.CurrentUser));
        return "plain:" + plain;
    }

    public static string? Unprotect(string? stored)
    {
        if (string.IsNullOrEmpty(stored)) return null;
        if (stored.StartsWith("plain:", StringComparison.Ordinal)) return stored[6..];
        if (stored.StartsWith("dpapi:", StringComparison.Ordinal) && OperatingSystem.IsWindows())
        {
            try { return Encoding.UTF8.GetString(ProtectedData.Unprotect(Convert.FromBase64String(stored[6..]), Entropy, DataProtectionScope.CurrentUser)); }
            catch (Exception ex) when (ex is CryptographicException or FormatException) { return null; }   // other user/machine
        }
        return null;
    }
}
