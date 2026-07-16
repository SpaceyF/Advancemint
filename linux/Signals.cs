using System.Diagnostics;
using System.Globalization;
using System.Net.NetworkInformation;

namespace AdvancemintLinux;

/// <summary>
/// A snapshot of "what's happening on this Linux box right now".
/// Everything here reads /proc, /sys or standard .NET APIs, so it works on
/// X11 and Wayland alike (no window-manager poking).
/// </summary>
public class Signals
{
    // time / session
    public DateTime Now = DateTime.Now;
    public double AppUptimeMin;
    public int Sessions;
    public bool FreshDay;

    // system
    public int Monitors = 1;
    public int Battery = -1;
    public bool Charging;
    public bool HasBattery;
    public double Cpu;
    public double RamPercent;
    public long FreeDiskBytes;
    public bool Network = true;
    public double LoadAvg;
    public double SysUptimeHours;

    // apps
    public HashSet<string> Procs = new(StringComparer.OrdinalIgnoreCase);
    public HashSet<string> AppsSeen = new(StringComparer.OrdinalIgnoreCase);

    // media (fed by MPRIS)
    public int SongsEver;
    public int SongsSession;
    public int DistinctSongs;
    public bool MusicPlaying;

    // meta
    public int Unlocked;

    public bool Proc(params string[] names) => names.Any(n => Procs.Contains(n));

    public void Refresh()
    {
        Now = DateTime.Now;
        Cpu = CpuUsage();
        RamPercent = RamUsage();
        RefreshPower();
        try { var d = new DriveInfo("/"); FreeDiskBytes = d.AvailableFreeSpace; } catch { }
        try { Network = NetworkInterface.GetIsNetworkAvailable(); } catch { }
        LoadAvg = LoadAverage();
        SysUptimeHours = SysUptime();
        RefreshProcs();
    }

    // ---- /proc/stat -> cpu% ----
    private static ulong _pIdle, _pTotal;
    private static double CpuUsage()
    {
        try
        {
            var line = File.ReadLines("/proc/stat").FirstOrDefault(l => l.StartsWith("cpu "));
            if (line == null) return 0;
            var f = line.Split(' ', StringSplitOptions.RemoveEmptyEntries).Skip(1).Select(ulong.Parse).ToArray();
            if (f.Length < 4) return 0;
            ulong idle = f[3] + (f.Length > 4 ? f[4] : 0);       // idle + iowait
            ulong total = 0; foreach (var v in f) total += v;
            ulong di = idle - _pIdle, dt = total - _pTotal;
            _pIdle = idle; _pTotal = total;
            if (dt == 0) return 0;
            return Math.Clamp((1.0 - (double)di / dt) * 100.0, 0, 100);
        }
        catch { return 0; }
    }

    // ---- /proc/meminfo -> ram% ----
    private static double RamUsage()
    {
        try
        {
            double total = 0, avail = 0;
            foreach (var l in File.ReadLines("/proc/meminfo"))
            {
                if (l.StartsWith("MemTotal:")) total = Num(l);
                else if (l.StartsWith("MemAvailable:")) { avail = Num(l); break; }
            }
            if (total <= 0) return 0;
            return Math.Clamp((1.0 - avail / total) * 100.0, 0, 100);
        }
        catch { return 0; }
        static double Num(string l) => double.TryParse(new string(l.Where(char.IsDigit).ToArray()), out var v) ? v : 0;
    }

    // ---- /sys/class/power_supply -> battery ----
    private void RefreshPower()
    {
        try
        {
            var root = "/sys/class/power_supply";
            if (!Directory.Exists(root)) { HasBattery = false; return; }
            foreach (var dir in Directory.GetDirectories(root))
            {
                var typeFile = Path.Combine(dir, "type");
                if (!File.Exists(typeFile) || File.ReadAllText(typeFile).Trim() != "Battery") continue;
                HasBattery = true;
                var cap = Path.Combine(dir, "capacity");
                if (File.Exists(cap) && int.TryParse(File.ReadAllText(cap).Trim(), out var p)) Battery = p;
                var st = Path.Combine(dir, "status");
                if (File.Exists(st))
                {
                    var s = File.ReadAllText(st).Trim();
                    Charging = s is "Charging" or "Full";
                }
                return;
            }
            HasBattery = false;
        }
        catch { }
    }

    private static double LoadAverage()
    {
        try { return double.Parse(File.ReadAllText("/proc/loadavg").Split(' ')[0], CultureInfo.InvariantCulture); }
        catch { return 0; }
    }

    private static double SysUptime()
    {
        try { return double.Parse(File.ReadAllText("/proc/uptime").Split(' ')[0], CultureInfo.InvariantCulture) / 3600.0; }
        catch { return 0; }
    }

    private void RefreshProcs()
    {
        var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        try
        {
            foreach (var p in Process.GetProcesses())
            {
                try { var n = p.ProcessName.ToLowerInvariant(); set.Add(n); AppsSeen.Add(n); } catch { }
            }
        }
        catch { }
        Procs = set;
    }
}
