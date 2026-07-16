using System.Diagnostics;

namespace AdvancemintLinux;

/// <summary>
/// Now-playing via MPRIS, using playerctl (works with Spotify, Cider, browsers, VLC...).
/// If playerctl isn't installed the media achievements simply never fire, nothing breaks.
/// </summary>
public static class Media
{
    public static bool Available { get; private set; } = true;

    public static (bool playing, string track) Poll()
    {
        if (!Available) return (false, "");
        var status = Run("playerctl", "status");
        if (status == null) { Available = false; return (false, ""); }   // playerctl missing
        bool playing = status.Trim().Equals("Playing", StringComparison.OrdinalIgnoreCase);
        var meta = Run("playerctl", "metadata --format \"{{title}}|{{artist}}\"") ?? "";
        return (playing, meta.Trim());
    }

    private static string? Run(string exe, string args)
    {
        try
        {
            var psi = new ProcessStartInfo(exe, args)
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };
            using var p = Process.Start(psi);
            if (p == null) return null;
            var outp = p.StandardOutput.ReadToEnd();
            if (!p.WaitForExit(1500)) { try { p.Kill(); } catch { } return ""; }
            return p.ExitCode == 0 ? outp : "";   // non-zero usually = "no players found"
        }
        catch { return null; }                     // binary not found
    }

    /// <summary>Fire and forget a sound file through whatever audio CLI exists.</summary>
    public static void PlaySound(string path)
    {
        foreach (var (exe, args) in new[] { ("paplay", $"\"{path}\""), ("aplay", $"-q \"{path}\""), ("ffplay", $"-nodisp -autoexit -loglevel quiet \"{path}\"") })
        {
            try
            {
                var psi = new ProcessStartInfo(exe, args) { UseShellExecute = false, CreateNoWindow = true, RedirectStandardError = true, RedirectStandardOutput = true };
                var p = Process.Start(psi);
                if (p != null) return;   // started ok, done
            }
            catch { /* try the next one */ }
        }
    }
}
