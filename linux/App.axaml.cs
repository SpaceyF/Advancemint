using System.Text.Json;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Layout;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using Avalonia.Threading;

namespace AdvancemintLinux;

public partial class App : Application
{
    private const double ToastSeconds = 7.0;

    private State _state = new();
    private readonly Signals _sig = new();
    private readonly DateTime _start = DateTime.Now;
    private bool _freshDay;
    private int _songsSession;
    private bool _musicPlaying;
    private string _lastTrack = "";

    private readonly Queue<Achievement> _queue = new();
    private ToastWindow? _current;
    private Window? _progress;

    private static string Dir => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "advancemint");
    private static string StatePath => Path.Combine(Dir, "state.json");

    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            desktop.ShutdownMode = ShutdownMode.OnExplicitShutdown;

        // reflect current autostart state in the tray menu
        try
        {
            var item = TrayIcon.GetIcons(this)?.FirstOrDefault()?.Menu?
                .Items.OfType<NativeMenuItem>().FirstOrDefault(i => i.Header == "Start on login");
            if (item != null) item.IsChecked = AutoStart.Enabled;
        }
        catch { }

        try
        {
            LoadState();
            var today = DateTime.Now.ToString("yyyy-MM-dd");
            _freshDay = _state.LastRunDate.Length > 0 && _state.LastRunDate != today;
            _state.LastRunDate = today;
            _state.Sessions++;
            SaveState();

            var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(3) };
            timer.Tick += (_, _) => Poll();
            timer.Start();
            Poll();
        }
        catch (Exception ex) { Log("startup: " + ex); }

        base.OnFrameworkInitializationCompleted();
    }

    private void Poll()
    {
        try
        {
            UpdateMedia();

            _sig.AppUptimeMin = (DateTime.Now - _start).TotalMinutes;
            _sig.Sessions = _state.Sessions;
            _sig.FreshDay = _freshDay;
            _sig.SongsEver = _state.SongsEver;
            _sig.SongsSession = _songsSession;
            _sig.DistinctSongs = _state.DistinctSongs.Count;
            _sig.MusicPlaying = _musicPlaying;
            _sig.Unlocked = _state.Unlocked.Count;
            _sig.Monitors = Monitors();
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

    private int Monitors()
    {
        try
        {
            if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime d && d.MainWindow is { } w)
                return w.Screens.ScreenCount;
            return _current?.Screens.ScreenCount ?? 1;
        }
        catch { return 1; }
    }

    private void Unlock(Achievement a)
    {
        _state.Unlocked.Add(a.Id);
        _sig.Unlocked = _state.Unlocked.Count;
        SaveState();
        ShowToast(a);
    }

    private void UpdateMedia()
    {
        try
        {
            var (playing, track) = Media.Poll();
            _musicPlaying = playing;
            if (string.IsNullOrWhiteSpace(track)) return;
            if (track != _lastTrack && playing)
            {
                _lastTrack = track;
                _state.SongsEver++;
                _songsSession++;
                _state.DistinctSongs.Add(track);
                SaveState();
            }
        }
        catch { }
    }

    private void ShowToast(Achievement a) { _queue.Enqueue(a); Pump(); }

    private void Pump()
    {
        if (_current != null || _queue.Count == 0) return;
        var a = _queue.Dequeue();
        var w = new ToastWindow(a, ToastSeconds);
        _current = w;
        w.Closed += (_, _) => { if (ReferenceEquals(_current, w)) _current = null; Pump(); };
        w.ShowToast();
    }

    // ---- tray handlers ----

    private void OnTest(object? s, EventArgs e) => ShowToast(Achievements.All[Random.Shared.Next(Achievements.All.Count)]);

    private void OnAutoStart(object? s, EventArgs e)
    {
        // the menu item toggles itself; mirror that into ~/.config/autostart
        var on = s is NativeMenuItem { IsChecked: true };
        AutoStart.Set(on);
        if (s is NativeMenuItem mi) mi.IsChecked = AutoStart.Enabled;   // reflect what actually happened
    }
    private void OnExit(object? s, EventArgs e) { if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime d) d.Shutdown(); }
    private void OnReset(object? s, EventArgs e)
    {
        _state = new State { LastRunDate = DateTime.Now.ToString("yyyy-MM-dd"), Sessions = 1 };
        _songsSession = 0;
        SaveState();
    }

    private void OnProgress(object? s, EventArgs e)
    {
        if (_progress != null) { _progress.Activate(); return; }
        var list = new StackPanel();
        foreach (var a in Achievements.All)
        {
            bool got = _state.Unlocked.Contains(a.Id);
            var row = new Grid { Margin = new Thickness(0, 2, 0, 2) };
            row.ColumnDefinitions.Add(new ColumnDefinition(new GridLength(34)));
            row.ColumnDefinitions.Add(new ColumnDefinition(new GridLength(1, GridUnitType.Star)));
            row.Children.Add(new TextBlock { Text = a.Icon, FontSize = 20, Opacity = got ? 1 : 0.25, VerticalAlignment = VerticalAlignment.Center });
            var tx = new StackPanel();
            tx.Children.Add(new TextBlock { Text = a.Title, FontSize = 14, FontFamily = ToastWindow.McFont, Foreground = new SolidColorBrush(got ? Colors.White : Color.FromRgb(0x66, 0x62, 0x70)) });
            tx.Children.Add(new TextBlock { Text = a.Desc, FontSize = 11, FontFamily = ToastWindow.McFont, Foreground = new SolidColorBrush(Color.FromRgb(0x8a, 0x86, 0x96)), TextWrapping = TextWrapping.Wrap, Opacity = got ? 1 : 0.6 });
            Grid.SetColumn(tx, 1);
            row.Children.Add(tx);
            list.Children.Add(row);
        }
        var root = new StackPanel { Margin = new Thickness(14) };
        root.Children.Add(new TextBlock { Text = $"Achievements: {_state.Unlocked.Count} / {Achievements.All.Count}", FontSize = 20, FontFamily = ToastWindow.McFont, Foreground = new SolidColorBrush(Color.FromRgb(0xFF, 0xCC, 0x44)), Margin = new Thickness(0, 0, 0, 10) });
        root.Children.Add(new ScrollViewer { Content = list, Height = 520 });

        var win = new Window
        {
            Title = "Advancemint",
            Width = 460,
            Height = 620,
            Background = new SolidColorBrush(Color.FromRgb(0x1a, 0x18, 0x20)),
            WindowStartupLocation = WindowStartupLocation.CenterScreen,
            Content = root,
        };
        win.Closed += (_, _) => _progress = null;
        _progress = win;
        win.Show();
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

    private static void Log(string m)
    {
        try { Directory.CreateDirectory(Dir); File.AppendAllText(Path.Combine(Dir, "advancemint.log"), $"{DateTime.Now:HH:mm:ss} {m}{Environment.NewLine}"); } catch { }
    }
}

public class State
{
    public HashSet<string> Unlocked { get; set; } = new();
    public int Sessions { get; set; }
    public int SongsEver { get; set; }
    public HashSet<string> DistinctSongs { get; set; } = new();
    public string LastRunDate { get; set; } = "";
}
