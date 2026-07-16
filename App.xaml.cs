using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using Microsoft.Win32;
using Windows.Media.Control;
using Forms = System.Windows.Forms;
using Drawing = System.Drawing;
using Application = System.Windows.Application;
using Color = System.Windows.Media.Color;
using HorizontalAlignment = System.Windows.HorizontalAlignment;
using VerticalAlignment = System.Windows.VerticalAlignment;
using PlaybackStatus = Windows.Media.Control.GlobalSystemMediaTransportControlsSessionPlaybackStatus;

namespace Advancemint;

public partial class App : Application
{
    private const double ToastSeconds = 7.0;

    private State _state = new();
    private Signals _sig = new();
    private DateTime _start = DateTime.Now;
    private bool _freshDay;
    private int _songsSession;
    private bool _musicPlaying;
    private string _lastTrack = "";

    private Forms.NotifyIcon? _tray;
    private Forms.ToolStripMenuItem? _countItem;
    private GlobalSystemMediaTransportControlsSessionManager? _smtc;

    private readonly Queue<Achievement> _queue = new();
    private ToastWindow? _current;
    private Window? _progress;

    private static string Dir => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Advancemint");
    private static string StatePath => Path.Combine(Dir, "state.json");

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        DispatcherUnhandledException += (_, ex) => { Log(ex.Exception.ToString()); ex.Handled = true; };
        try
        {
            LoadState();
            var today = DateTime.Now.ToString("yyyy-MM-dd");
            _freshDay = _state.LastRunDate.Length > 0 && _state.LastRunDate != today;
            _state.LastRunDate = today;
            _state.Sessions++;
            SaveState();

            SetupTray();

            if (e.Args.Contains("--test"))
                ShowToast(Achievements.All[new Random().Next(Achievements.All.Count)]);

            var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(3) };
            timer.Tick += async (_, _) => await Poll();
            timer.Start();
            _ = Poll();   // fire one immediately
        }
        catch (Exception ex) { Log("startup: " + ex); }
    }

    // ---- polling / evaluation ----

    private async Task Poll()
    {
        try
        {
            await UpdateMedia();

            _sig.AppUptimeMin = (DateTime.Now - _start).TotalMinutes;
            _sig.Sessions = _state.Sessions;
            _sig.FreshDay = _freshDay;
            _sig.SongsEver = _state.SongsEver;
            _sig.SongsSession = _songsSession;
            _sig.DistinctSongs = _state.DistinctSongs.Count;
            _sig.MusicPlaying = _musicPlaying;
            _sig.Unlocked = _state.Unlocked.Count;
            _sig.Refresh();

            foreach (var a in Achievements.All)
            {
                if (_state.Unlocked.Contains(a.Id)) continue;
                bool ok;
                try { ok = a.Cond(_sig); } catch { ok = false; }
                if (ok) Unlock(a);
            }
        }
        catch (Exception ex) { Log("poll: " + ex.Message); }
    }

    private void Unlock(Achievement a)
    {
        _state.Unlocked.Add(a.Id);
        _sig.Unlocked = _state.Unlocked.Count;   // let meta achievements chain in the same pass
        SaveState();
        UpdateCount();
        ShowToast(a);
    }

    private async Task UpdateMedia()
    {
        try
        {
            _smtc ??= await GlobalSystemMediaTransportControlsSessionManager.RequestAsync();
            var s = _smtc.GetCurrentSession();
            if (s == null) { _musicPlaying = false; return; }
            _musicPlaying = s.GetPlaybackInfo().PlaybackStatus == PlaybackStatus.Playing;
            var p = await s.TryGetMediaPropertiesAsync();
            if (p == null || string.IsNullOrWhiteSpace(p.Title)) return;
            var key = p.Title + " | " + (p.Artist ?? "");
            if (key != _lastTrack && _musicPlaying)
            {
                _lastTrack = key;
                _state.SongsEver++;
                _songsSession++;
                _state.DistinctSongs.Add(key);
                SaveState();
            }
        }
        catch { }
    }

    // ---- toasts ----

    private void ShowToast(Achievement a)
    {
        _queue.Enqueue(a);
        Pump();
    }

    private void Pump()
    {
        if (_current != null || _queue.Count == 0) return;
        var a = _queue.Dequeue();
        var w = new ToastWindow(a, ToastSeconds);
        _current = w;
        w.Closed += (_, _) => { if (ReferenceEquals(_current, w)) _current = null; Pump(); };
        w.ShowToast();
    }

    // ---- tray + progress window ----

    private void SetupTray()
    {
        _tray = new Forms.NotifyIcon { Icon = LoadIcon(), Visible = true, Text = "Advancemint" };
        var menu = new Forms.ContextMenuStrip();
        _countItem = new Forms.ToolStripMenuItem(CountText()) { Enabled = false };
        menu.Items.Add(_countItem);
        menu.Items.Add(new Forms.ToolStripSeparator());
        menu.Items.Add("Show progress", null, (_, _) => ShowProgress());
        menu.Items.Add("Test toast", null, (_, _) => ShowToast(Achievements.All[new Random().Next(Achievements.All.Count)]));

        var auto = new Forms.ToolStripMenuItem("Start with Windows") { Checked = IsAutoStart(), CheckOnClick = true };
        auto.CheckedChanged += (_, _) => SetAutoStart(auto.Checked);
        menu.Items.Add(auto);

        menu.Items.Add("Reset progress", null, (_, _) =>
        {
            if (Forms.MessageBox.Show("Reset all achievement progress?", "Advancemint", Forms.MessageBoxButtons.YesNo) == Forms.DialogResult.Yes)
            { _state = new State { LastRunDate = DateTime.Now.ToString("yyyy-MM-dd"), Sessions = 1 }; _songsSession = 0; SaveState(); UpdateCount(); }
        });
        menu.Items.Add(new Forms.ToolStripSeparator());
        menu.Items.Add("Exit", null, (_, _) => Shutdown());
        _tray.ContextMenuStrip = menu;
        _tray.DoubleClick += (_, _) => ShowProgress();
    }

    private string CountText() => $"Achievements: {_state.Unlocked.Count} / {Achievements.All.Count}";
    private void UpdateCount() { if (_countItem != null) _countItem.Text = CountText(); }

    private void ShowProgress()
    {
        if (_progress != null) { _progress.Activate(); return; }
        var win = new Window
        {
            Title = "Advancemint", Width = 460, Height = 620, Background = new SolidColorBrush(Color.FromRgb(0x1a, 0x18, 0x20)),
            WindowStartupLocation = WindowStartupLocation.CenterScreen, FontFamily = ToastWindow.McFont
        };
        var root = new StackPanel { Margin = new Thickness(14) };
        root.Children.Add(new TextBlock { Text = CountText(), FontSize = 20, Foreground = new SolidColorBrush(Color.FromRgb(0xFF, 0xCC, 0x44)), Margin = new Thickness(0, 0, 0, 10) });
        var list = new StackPanel();
        foreach (var a in Achievements.All)
        {
            bool got = _state.Unlocked.Contains(a.Id);
            var row = new Grid { Margin = new Thickness(0, 2, 0, 2) };
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(34) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            row.Children.Add(new TextBlock { Text = a.Icon, FontSize = 20, Opacity = got ? 1 : 0.25, VerticalAlignment = VerticalAlignment.Center });
            var tx = new StackPanel();
            tx.Children.Add(new TextBlock { Text = a.Title, FontSize = 14, Foreground = new SolidColorBrush(got ? Color.FromRgb(0xFF, 0xFF, 0xFF) : Color.FromRgb(0x66, 0x62, 0x70)) });
            tx.Children.Add(new TextBlock { Text = a.Desc, FontSize = 11, Foreground = new SolidColorBrush(Color.FromRgb(0x8a, 0x86, 0x96)), TextWrapping = TextWrapping.Wrap, Opacity = got ? 1 : 0.6 });
            Grid.SetColumn(tx, 1); row.Children.Add(tx);
            list.Children.Add(row);
        }
        root.Children.Add(new ScrollViewer { Content = list, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Height = 520 });
        win.Content = root;
        win.Closed += (_, _) => _progress = null;
        _progress = win;
        win.Show();
    }

    // ---- autostart ----

    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string RunName = "Advancemint";

    internal static bool IsAutoStart()
    {
        try { using var k = Registry.CurrentUser.OpenSubKey(RunKey); return k?.GetValue(RunName) != null; }
        catch { return false; }
    }

    internal static void SetAutoStart(bool on)
    {
        try
        {
            using var k = Registry.CurrentUser.OpenSubKey(RunKey, true) ?? Registry.CurrentUser.CreateSubKey(RunKey);
            if (on) k!.SetValue(RunName, $"\"{Environment.ProcessPath}\"");
            else k!.DeleteValue(RunName, false);
        }
        catch (Exception ex) { Log("autostart: " + ex.Message); }
    }

    private static Drawing.Icon LoadIcon()
    {
        try { var exe = Environment.ProcessPath; if (exe != null) { var i = Drawing.Icon.ExtractAssociatedIcon(exe); if (i != null) return i; } } catch { }
        return Drawing.SystemIcons.Application;
    }

    // ---- persistence ----

    private void LoadState()
    {
        try { if (File.Exists(StatePath)) { var st = JsonSerializer.Deserialize<State>(File.ReadAllText(StatePath)); if (st != null) _state = st; } }
        catch { }
    }

    private void SaveState()
    {
        try { Directory.CreateDirectory(Dir); File.WriteAllText(StatePath, JsonSerializer.Serialize(_state, new JsonSerializerOptions { WriteIndented = true })); }
        catch { }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        if (_tray != null) { _tray.Visible = false; _tray.Dispose(); }
        base.OnExit(e);
    }

    private static void Log(string m) { try { File.AppendAllText(Path.Combine(Dir, "advancemint.log"), $"{DateTime.Now:HH:mm:ss} {m}{Environment.NewLine}"); } catch { } }
}

public class State
{
    public HashSet<string> Unlocked { get; set; } = new();
    public int Sessions { get; set; }
    public int SongsEver { get; set; }
    public HashSet<string> DistinctSongs { get; set; } = new();
    public string LastRunDate { get; set; } = "";
}
