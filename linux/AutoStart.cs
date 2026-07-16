namespace AdvancemintLinux;

/// <summary>
/// Linux autostart = drop a .desktop file in ~/.config/autostart.
/// Points at wherever this binary actually lives, so it works no matter where
/// the tarball got extracted.
/// </summary>
public static class AutoStart
{
    private static string DesktopFile => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "autostart", "advancemint.desktop");

    public static bool Enabled => File.Exists(DesktopFile);

    public static void Set(bool on)
    {
        try
        {
            if (!on) { if (File.Exists(DesktopFile)) File.Delete(DesktopFile); return; }

            var exe = Environment.ProcessPath ?? Path.Combine(AppContext.BaseDirectory, "advancemint");
            var icon = Path.Combine(AppContext.BaseDirectory, "assets", "icon.png");
            Directory.CreateDirectory(Path.GetDirectoryName(DesktopFile)!);
            File.WriteAllText(DesktopFile, $"""
                [Desktop Entry]
                Type=Application
                Name=Advancemint
                Comment=Minecraft-style achievement toasts for your PC
                Exec="{exe}"
                Icon={icon}
                Terminal=false
                Categories=Utility;
                StartupNotify=false
                X-GNOME-Autostart-enabled=true

                """);
        }
        catch { }
    }
}
