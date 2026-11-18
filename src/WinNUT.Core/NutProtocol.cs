using System.Globalization;

namespace WinNUT.Core;

public class NutException(string message, string? code = null) : Exception(message)
{
    public string? Code { get; } = code;
}

/// <summary>Parsing helpers for the NUT line protocol (RFC 9271).</summary>
public static class NutProtocol
{
    /// <summary>Split a line into tokens; double-quoted tokens may contain spaces and \" \\ escapes.</summary>
    public static List<string> Tokenize(string line)
    {
        var tokens = new List<string>();
        var sb = new System.Text.StringBuilder();
        bool inQuote = false, have = false;
        for (int i = 0; i < line.Length; i++)
        {
            char ch = line[i];
            if (inQuote)
            {
                if (ch == '\\' && i + 1 < line.Length) sb.Append(line[++i]);
                else if (ch == '"') inQuote = false;
                else sb.Append(ch);
            }
            else if (ch == '"') { inQuote = true; have = true; }
            else if (char.IsWhiteSpace(ch))
            {
                if (have) { tokens.Add(sb.ToString()); sb.Clear(); have = false; }
            }
            else { sb.Append(ch); have = true; }
        }
        if (have) tokens.Add(sb.ToString());
        return tokens;
    }

    public static string Quote(string s) => "\"" + s.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";

    public static double? ToDouble(string? s) =>
        double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var d) ? d : null;
}
