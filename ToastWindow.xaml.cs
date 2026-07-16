using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Windows.Threading;
using Color = System.Windows.Media.Color;
using FontFamily = System.Windows.Media.FontFamily;
using Point = System.Windows.Point;
using Image = System.Windows.Controls.Image;
using Path = System.IO.Path;
using Rectangle = System.Windows.Shapes.Rectangle;
using HorizontalAlignment = System.Windows.HorizontalAlignment;
using VerticalAlignment = System.Windows.VerticalAlignment;
using Size = System.Windows.Size;

namespace Advancemint;

public partial class ToastWindow : Window
{
    private const double PX = 3;
    private const int Inset = 4;
    private const double IconSize = 56;

    internal static readonly FontFamily McFont = LoadFont();

    /// <summary>Never throw: this runs in a static initializer, so a failure here would kill the type.</summary>
    private static FontFamily LoadFont()
    {
        try
        {
            var asm = System.Reflection.Assembly.GetExecutingAssembly().GetName().Name;
            var baseUri = new Uri("pack://application:,,,/");
            Func<FontFamily>[] forms =
            {
                () => new FontFamily(baseUri, "./assets/#Monocraft"),
                () => new FontFamily(baseUri, "./#Monocraft"),
                () => new FontFamily($"pack://application:,,,/{asm};component/assets/#Monocraft"),
            };
            foreach (var make in forms)
            {
                try
                {
                    var ff = make();
                    if (ff.FamilyNames.Values.Any(v => string.Equals(v, "Monocraft", StringComparison.OrdinalIgnoreCase)))
                        return ff;
                }
                catch { }
            }
        }
        catch { }
        return new FontFamily("Consolas");
    }

    private readonly Achievement _ach;
    private readonly double _seconds;
    private Grid _content = null!;
    private Canvas _particles = null!;
    private Point _origin;
    private double _offLeft;
    private BitmapSource? _noteSheet;
    private DispatcherTimer? _dismiss, _spawn, _anim;
    private readonly List<Particle> _live = new();
    private static readonly Random Rng = new();
    private bool _closing;

    public ToastWindow(Achievement ach, double seconds)
    {
        InitializeComponent();
        _ach = ach;
        _seconds = seconds;
        BuildUi();
    }

    private void BuildUi()
    {
        RootGrid.Children.Add(BuildNineSlice());

        var content = _content = new Grid { Margin = new Thickness(14, 11, 16, 11) };
        content.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        content.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(12) });
        content.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        // icon slot with the emoji
        var iconBox = new Border
        {
            Width = IconSize, Height = IconSize, CornerRadius = new CornerRadius(6),
            Background = new SolidColorBrush(Color.FromArgb(0x33, 0xFF, 0xFF, 0xFF)),
            VerticalAlignment = VerticalAlignment.Center,
            Child = new TextBlock
            {
                Text = _ach.Icon, FontSize = 30,
                HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center
            }
        };
        Grid.SetColumn(iconBox, 0);
        content.Children.Add(iconBox);

        var lines = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        lines.Children.Add(Line("Advancement Made!", Rgb(0xFF, 0xFF, 0x55), 12));
        lines.Children.Add(Line(Trim(_ach.Title, 30), Rgb(0xFF, 0xFF, 0xFF), 18, 3));
        lines.Children.Add(Line(Trim(_ach.Desc, 46), Rgb(0xA8, 0xA4, 0xB4), 12, 3));
        Grid.SetColumn(lines, 2);
        content.Children.Add(lines);

        RootGrid.Children.Add(content);
        _particles = new Canvas { IsHitTestVisible = false, ClipToBounds = false };
        RootGrid.Children.Add(_particles);
    }

    private Grid BuildNineSlice()
    {
        var g = new Grid();
        var src = TryLoad("now_playing.png");
        if (src == null)
        {
            g.Children.Add(new Border { Background = new SolidColorBrush(Rgb(0x21, 0x21, 0x21)), BorderBrush = new SolidColorBrush(Rgb(0x55, 0x55, 0x55)), BorderThickness = new Thickness(2), CornerRadius = new CornerRadius(2) });
            return g;
        }
        int w = src.PixelWidth, h = src.PixelHeight, i = Inset; double edge = i * PX;
        foreach (var c in new[] { new GridLength(edge), new GridLength(1, GridUnitType.Star), new GridLength(edge) }) g.ColumnDefinitions.Add(new ColumnDefinition { Width = c });
        foreach (var r in new[] { new GridLength(edge), new GridLength(1, GridUnitType.Star), new GridLength(edge) }) g.RowDefinitions.Add(new RowDefinition { Height = r });
        (int x, int y, int cw, int ch)[] rects = { (0,0,i,i),(i,0,w-2*i,i),(w-i,0,i,i),(0,i,i,h-2*i),(i,i,w-2*i,h-2*i),(w-i,i,i,h-2*i),(0,h-i,i,i),(i,h-i,w-2*i,i),(w-i,h-i,i,i) };
        for (int idx = 0; idx < 9; idx++)
        {
            var r = rects[idx];
            var piece = new Image { Source = new CroppedBitmap(src, new Int32Rect(r.x, r.y, r.cw, r.ch)), Stretch = Stretch.Fill };
            RenderOptions.SetBitmapScalingMode(piece, BitmapScalingMode.NearestNeighbor);
            Grid.SetColumn(piece, idx % 3); Grid.SetRow(piece, idx / 3); g.Children.Add(piece);
        }
        return g;
    }

    private FrameworkElement Line(string text, Color color, double size, double top = 0)
    {
        var host = new Grid { Margin = new Thickness(0, top, 0, 0), HorizontalAlignment = HorizontalAlignment.Left };
        TextBlock Make(Color c, double dx, double dy) => new() { Text = text, FontFamily = McFont, FontSize = size, Foreground = new SolidColorBrush(c), RenderTransform = new TranslateTransform(dx, dy) };
        host.Children.Add(Make(Shadow(color), 2, 2));
        host.Children.Add(Make(color, 0, 0));
        return host;
    }

    public void ShowToast()
    {
        _content.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        Width = Math.Max(360, Math.Ceiling(_content.DesiredSize.Width));
        Height = Math.Ceiling(_content.DesiredSize.Height);

        var wa = SystemParameters.WorkArea;
        double restLeft = wa.Right - Width - 16;   // top-right, like Minecraft advancements
        Top = wa.Top + 16;
        _offLeft = wa.Right + 8;

        Show();
        Left = _offLeft;
        _origin = new Point(14 + IconSize / 2, 11 + IconSize / 2);

        PlaySound("advancement.wav");
        StartBurst();

        Tween(_offLeft, restLeft, 0.34, () =>
        {
            _dismiss = new DispatcherTimer { Interval = TimeSpan.FromSeconds(Math.Max(1.0, _seconds)) };
            _dismiss.Tick += (_, _) => Dismiss();
            _dismiss.Start();
        });
    }

    private void Dismiss()
    {
        if (_closing) return;
        _closing = true;
        _dismiss?.Stop(); _spawn?.Stop();
        Tween(Left, _offLeft, 0.3, () => { try { Close(); } catch { } });
    }

    public void Kill()
    {
        if (_closing) return;
        _closing = true;
        _dismiss?.Stop(); _spawn?.Stop(); _anim?.Stop();
        try { Close(); } catch { }
    }

    private void Tween(double from, double to, double seconds, Action? done)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var t = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(15) };
        t.Tick += (_, _) => { double p = Math.Min(1, sw.Elapsed.TotalSeconds / seconds); Left = from + (to - from) * (1 - Math.Pow(1 - p, 3)); if (p >= 1) { t.Stop(); done?.Invoke(); } };
        t.Start();
    }

    // celebratory gold particle burst
    private void StartBurst()
    {
        _anim = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(16) };
        _anim.Tick += (_, _) => Step();
        _anim.Start();
        for (int i = 0; i < 16; i++) Spawn();
        int spawned = 0;
        _spawn = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(60) };
        _spawn.Tick += (_, _) => { if (_closing || spawned++ > 12) { _spawn?.Stop(); return; } Spawn(); };
        _spawn.Start();
    }

    private void Spawn()
    {
        var gold = new[] { Rgb(0xFF, 0xE0, 0x66), Rgb(0xFF, 0xC8, 0x33), Rgb(0xFF, 0xF2, 0xB0), Rgb(0xFF, 0xAA, 0x00) };
        var color = gold[Rng.Next(gold.Length)];
        double sz = 5 + Rng.NextDouble() * 6;
        var el = NoteGlyph(sz, color);
        var p = new Particle { El = el, BaseX = _origin.X - sz / 2 + (Rng.NextDouble() * 12 - 6), Y = _origin.Y - sz / 2, Life = 0.9 + Rng.NextDouble() * 0.6, RiseSpeed = 22 + Rng.NextDouble() * 20, Amp = 5 + Rng.NextDouble() * 7, Freq = 3 + Rng.NextDouble() * 2, Phase = Rng.NextDouble() * Math.PI * 2 };
        Canvas.SetLeft(el, p.BaseX); Canvas.SetTop(el, p.Y);
        _particles.Children.Add(el); _live.Add(p);
    }

    private void Step()
    {
        double dt = 0.016;
        for (int k = _live.Count - 1; k >= 0; k--)
        {
            var p = _live[k]; p.Age += dt; double t = p.Age / p.Life;
            if (t >= 1) { _particles.Children.Remove(p.El); _live.RemoveAt(k); continue; }
            p.Y -= p.RiseSpeed * dt;
            Canvas.SetLeft(p.El, p.BaseX + Math.Sin(p.Age * p.Freq + p.Phase) * p.Amp);
            Canvas.SetTop(p.El, p.Y);
            p.El.Opacity = 1 - t * t;
        }
        if (_closing && _live.Count == 0) _anim?.Stop();
    }

    private UIElement NoteGlyph(double size, Color color)
    {
        _noteSheet ??= TryLoad("music_notes.png");
        if (_noteSheet != null)
        {
            int fw = _noteSheet.PixelWidth, frames = Math.Max(1, _noteSheet.PixelHeight / fw);
            var cropped = new CroppedBitmap(_noteSheet, new Int32Rect(0, Rng.Next(frames) * fw, fw, fw));
            var rect = new Rectangle { Width = size, Height = size, Fill = new SolidColorBrush(color), OpacityMask = new ImageBrush(cropped) { Stretch = Stretch.Uniform } };
            RenderOptions.SetBitmapScalingMode(rect, BitmapScalingMode.NearestNeighbor);
            return rect;
        }
        return new Rectangle { Width = size, Height = size, Fill = new SolidColorBrush(color) };
    }

    private static BitmapSource? TryLoad(string file)
    {
        try
        {
            // read from the embedded resource stream (UriSource with pack:// is unreliable in single-file publishes)
            var info = System.Windows.Application.GetResourceStream(new Uri($"pack://application:,,,/assets/{file}"));
            if (info == null) return null;
            using var s = info.Stream;
            var ms = new MemoryStream();
            s.CopyTo(ms);
            ms.Position = 0;
            var bmp = new BitmapImage();
            bmp.BeginInit(); bmp.CacheOption = BitmapCacheOption.OnLoad; bmp.CreateOptions = BitmapCreateOptions.PreservePixelFormat; bmp.StreamSource = ms; bmp.EndInit(); bmp.Freeze();
            return bmp;
        }
        catch { return null; }
    }

    private static readonly List<System.Media.SoundPlayer> Players = new();
    private static void PlaySound(string file)
    {
        try
        {
            var info = System.Windows.Application.GetResourceStream(new Uri($"pack://application:,,,/assets/{file}"));
            if (info == null) return;
            var sp = new System.Media.SoundPlayer(info.Stream);
            Players.Add(sp);
            if (Players.Count > 6) Players.RemoveAt(0);
            sp.Play();
        }
        catch { }
    }

    private static string Trim(string s, int max) => s.Length <= max ? s : s.Substring(0, max - 3).TrimEnd() + "...";
    private static Color Rgb(byte r, byte g, byte b) => Color.FromRgb(r, g, b);
    private static Color Shadow(Color c) => Color.FromRgb((byte)(c.R / 4), (byte)(c.G / 4), (byte)(c.B / 4));

    private sealed class Particle { public UIElement El = null!; public double BaseX, Y, Life, Age, RiseSpeed, Amp, Freq, Phase; }
}
