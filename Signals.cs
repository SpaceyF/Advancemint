using System.Diagnostics;
using System.IO;
using System.Net.NetworkInformation;
using System.Runtime.InteropServices;

namespace Advancemint;

/// <summary>A snapshot of "what's happening on the PC right now", refreshed each poll.</summary>
public class Signals
{
    // time / session
    public DateTime Now = DateTime.Now;
    public double AppUptimeMin;         // minutes this app has been running
    public double SessionMin;           // minutes of activity this session
    public int Sessions;                // total launches
    public bool FreshDay;               // first run on a new calendar day

    // system
    public int Monitors = 1;
    public int Battery = -1;            // 0-100, -1 = no battery
    public bool Charging;
    public bool HasBattery;
    public double Cpu;                  // 0-100
    public double RamPercent;           // 0-100
    public long FreeDiskBytes;
    public bool Network = true;
    public long IdleMs;
    public int WindowCount;

    // apps
    public HashSet<string> Procs = new();          // running process names, lowercase, no .exe
    public string Foreground = "";                  // foreground app process name, lowercase
    public HashSet<string> AppsSeen = new();        // distinct foreground apps seen (persisted)

    // media (fed by SMTC)
    public int SongsEver;
    public int SongsSession;
    public int DistinctSongs;
    public bool MusicPlaying;

    // meta
    public int Unlocked;                // how many achievements unlocked so far

    public bool Proc(params string[] names) => names.Any(n => Procs.Contains(n));

    public void Refresh()
    {
        Now = DateTime.Now;
        try { Monitors = System.Windows.Forms.Screen.AllScreens.Length; } catch { }
        RefreshPower();
        Cpu = CpuUsage();
        RamPercent = RamUsage();
        try { var d = new DriveInfo(Path.GetPathRoot(Environment.SystemDirectory) ?? "C:\\"); FreeDiskBytes = d.AvailableFreeSpace; } catch { }
        try { Network = NetworkInterface.GetIsNetworkAvailable(); } catch { }
        IdleMs = IdleTime();
        RefreshProcs();
        Foreground = ForegroundProc();
        if (Foreground.Length > 0) AppsSeen.Add(Foreground);
        WindowCount = CountWindows();
    }

    private void RefreshProcs()
    {
        var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        try { foreach (var p in Process.GetProcesses()) { try { set.Add(p.ProcessName.ToLowerInvariant()); } catch { } } }
        catch { }
        Procs = set;
    }

    // ---- native ----

    [StructLayout(LayoutKind.Sequential)]
    private struct SYSTEM_POWER_STATUS { public byte ACLineStatus, BatteryFlag, BatteryLifePercent, Reserved1; public int BatteryLifeTime, BatteryFullLifeTime; }
    [DllImport("kernel32.dll")] private static extern bool GetSystemPowerStatus(out SYSTEM_POWER_STATUS s);

    private void RefreshPower()
    {
        try
        {
            if (GetSystemPowerStatus(out var s))
            {
                HasBattery = (s.BatteryFlag & 128) == 0;   // bit 7 set = no system battery
                Charging = s.ACLineStatus == 1;
                Battery = s.BatteryLifePercent <= 100 ? s.BatteryLifePercent : -1;
            }
        }
        catch { }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MEMORYSTATUSEX { public uint dwLength; public uint dwMemoryLoad; public ulong ullTotalPhys, ullAvailPhys, ullTotalPageFile, ullAvailPageFile, ullTotalVirtual, ullAvailVirtual, ullAvailExtendedVirtual; }
    [DllImport("kernel32.dll")] private static extern bool GlobalMemoryStatusEx(ref MEMORYSTATUSEX m);

    private static double RamUsage()
    {
        try { var m = new MEMORYSTATUSEX { dwLength = (uint)Marshal.SizeOf<MEMORYSTATUSEX>() }; if (GlobalMemoryStatusEx(ref m)) return m.dwMemoryLoad; }
        catch { }
        return 0;
    }

    [DllImport("kernel32.dll")] private static extern bool GetSystemTimes(out System.Runtime.InteropServices.ComTypes.FILETIME idle, out System.Runtime.InteropServices.ComTypes.FILETIME kernel, out System.Runtime.InteropServices.ComTypes.FILETIME user);
    private static ulong _pIdle, _pKernel, _pUser;
    private static ulong Ft(System.Runtime.InteropServices.ComTypes.FILETIME f) => ((ulong)(uint)f.dwHighDateTime << 32) | (uint)f.dwLowDateTime;

    private static double CpuUsage()
    {
        try
        {
            if (!GetSystemTimes(out var idle, out var kernel, out var user)) return 0;
            ulong i = Ft(idle), k = Ft(kernel), u = Ft(user);
            ulong di = i - _pIdle, dk = k - _pKernel, du = u - _pUser;
            _pIdle = i; _pKernel = k; _pUser = u;
            ulong total = dk + du;
            if (total == 0) return 0;
            return Math.Clamp((1.0 - (double)di / total) * 100.0, 0, 100);
        }
        catch { return 0; }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct LASTINPUTINFO { public uint cbSize; public uint dwTime; }
    [DllImport("user32.dll")] private static extern bool GetLastInputInfo(ref LASTINPUTINFO p);

    private static long IdleTime()
    {
        try { var l = new LASTINPUTINFO { cbSize = (uint)Marshal.SizeOf<LASTINPUTINFO>() }; if (GetLastInputInfo(ref l)) return Environment.TickCount - (long)l.dwTime; }
        catch { }
        return 0;
    }

    [DllImport("user32.dll")] private static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(IntPtr h, out uint pid);
    private static string ForegroundProc()
    {
        try { var h = GetForegroundWindow(); if (h == IntPtr.Zero) return ""; GetWindowThreadProcessId(h, out uint pid); return Process.GetProcessById((int)pid).ProcessName.ToLowerInvariant(); }
        catch { return ""; }
    }

    private delegate bool EnumProc(IntPtr h, IntPtr p);
    [DllImport("user32.dll")] private static extern bool EnumWindows(EnumProc cb, IntPtr p);
    [DllImport("user32.dll")] private static extern bool IsWindowVisible(IntPtr h);
    [DllImport("user32.dll")] private static extern int GetWindowTextLength(IntPtr h);
    private static int CountWindows()
    {
        int c = 0;
        try { EnumWindows((h, _) => { if (IsWindowVisible(h) && GetWindowTextLength(h) > 0) c++; return true; }, IntPtr.Zero); }
        catch { }
        return c;
    }
}
