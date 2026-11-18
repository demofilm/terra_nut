// Fake NUT server for manual testing: type  ol | ob | lb | fsd  in this console to change state.
using System.Net;
using System.Net.Sockets;
using System.Text;

int port = args.Length > 0 ? int.Parse(args[0]) : 3493;
string status = "OL CHRG"; double charge = 100; double load = 28;
var l = new TcpListener(IPAddress.Loopback, port); l.Start();
Console.WriteLine($"Fake NUT on 127.0.0.1:{port} (ups name: ups). Commands: ol | ob | lb | fsd | quit");

_ = Task.Run(async () => { while (true) { var c = await l.AcceptTcpClientAsync(); _ = Task.Run(() => Serve(c)); } });
_ = Task.Run(async () => { while (true) { await Task.Delay(1000); if (status.StartsWith("OB")) charge = Math.Max(0, charge - 1.5); else charge = Math.Min(100, charge + 0.5); } });
while (Console.ReadLine()?.Trim().ToLowerInvariant() is { } cmd && cmd != "quit")
    status = cmd switch { "ol" => "OL CHRG", "ob" => "OB DISCHRG", "lb" => "OB DISCHRG LB", "fsd" => "OL FSD", _ => status };

if (Console.IsInputRedirected) await Task.Delay(-1);

async Task Serve(TcpClient c)
{
    using var _ = c; var s = c.GetStream();
    using var rd = new StreamReader(s); using var wr = new StreamWriter(s, new UTF8Encoding(false)) { NewLine = "\n", AutoFlush = true };
    try
    {
        while (await rd.ReadLineAsync() is { } line)
        {
            if (line.StartsWith("USERNAME") || line.StartsWith("PASSWORD")) await wr.WriteLineAsync("OK");
            else if (line == "LOGOUT") { await wr.WriteLineAsync("OK Goodbye"); return; }
            else if (line == "LIST UPS") { await wr.WriteLineAsync("BEGIN LIST UPS\nUPS ups \"Fake UPS\"\nEND LIST UPS"); }
            else if (line == "LIST VAR ups")
            {
                bool ob = status.StartsWith("OB");
                var v = new (string, string)[] {
                    ("ups.mfr", "FakePower"), ("ups.model", "Smart-UPS 1500"), ("ups.status", status),
                    ("battery.charge", charge.ToString("0")), ("battery.runtime", ((int)(charge * 18)).ToString()),
                    ("input.voltage", ob ? "0" : "231.2"), ("input.frequency", ob ? "0" : "50.0"),
                    ("output.voltage", "230.0"), ("ups.load", load.ToString("0")) };
                await wr.WriteLineAsync("BEGIN LIST VAR ups");
                foreach (var (k, val) in v) await wr.WriteLineAsync($"VAR ups {k} \"{val}\"");
                await wr.WriteLineAsync("END LIST VAR ups");
            }
            else await wr.WriteLineAsync("ERR UNKNOWN-COMMAND");
        }
    }
    catch (IOException) { }
}

