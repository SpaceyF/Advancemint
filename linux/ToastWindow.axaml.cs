using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Layout;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Threading;

namespace AdvancemintLinux;

public partial class ToastWindow : Window
{
    private const double PX = 3;
    private const int Inset = 4;
    private const double IconSize = 56;

    internal static readonly FontFamily McFont = new("avares://advancemint/assets#Monocraft");

    private readonly Achievement _ach;
    private readonly double _seconds;
    private Grid _root = null!;
    private DispatcherTimer? _dismiss;
    private bool _closing;
    private int _restX, _offX, _y;

    // Avalonia needs a parameterless ctor for XAML loading
    public ToastWindow() : this(new Achievement("x", "Test", "Test toast", "⭐", _ => false, "Meta"), 6) { }

    public ToastWindow(Achievement ach, double seconds)
    {
        _ach = ach;
        _seconds = seconds;
        AvaloniaXamlLoader.Load(this);
        _root = this.FindControl<Grid>("RootGrid")!;
        BuildUi();
    }

    private static Bitmap? Load(string file)
    {
        try { return new Bitmap(AssetLoader.Open(new Uri($"avares://advancemint/assets/{file}"))); }
        catch { return null; }
    }

    private void BuildUi()
    {
        _root.Children.Add(BuildNineSlice());

        var content = new Grid { Margin = new Thickness(14, 11, 16, 11) };
        content.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));
        content.ColumnDefinitions.Add(new ColumnDefinition(new GridLength(12)));
        content.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));

        var iconBox = new Border
        {
            Width = IconSize,
            Height = IconSize,
            CornerRadius = new CornerRadius(6),
            Background = new SolidColorBrush(Color.FromArgb(0x33, 0xFF, 0xFF, 0xFF)),
            VerticalAlignment = VerticalAlignment.Center,
            Child = new TextBlock
            {
                Text = _ach.Icon,
                FontSize = 30,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
            },
        };
        Grid.SetColumn(iconBox, 0);
        content.Children.Add(iconBox);

        var lines = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        lines.Children.Add(Line("Advancement Made!", Color.FromRgb(0xFF, 0xFF, 0x55), 12));
        lines.Children.Add(Line(Trim(_ach.Title, 30), Colors.White, 18, 3));
        lines.Children.Add(Line(Trim(_ach.Desc, 46), Color.FromRgb(0xA8, 0xA4, 0xB4), 12, 3));
        Grid.SetColumn(lines, 2);
        content.Children.Add(lines);

        _root.Children.Add(content);
    }

    private Control BuildNineSlice()
    {
        var src = Load("now_playing.png");
        if (src == null)
            return new Border { Background = new SolidColorBrush(Color.FromRgb(0x21, 0x21, 0x21)), BorderBrush = new SolidColorBrush(Color.FromRgb(0x55, 0x55, 0x55)), BorderThickness = new Thickness(2), CornerRadius = new CornerRadius(2) };

        int w = src.PixelSize.Width, h = src.PixelSize.Height, i = Inset;
        double edge = i * PX;
        var g = new Grid();
        foreach (var c in new[] { new GridLength(edge), new GridLength(1, GridUnitType.Star), new GridLength(edge) })
            g.ColumnDefinitions.Add(new ColumnDefinition(c));
        foreach (var r in new[] { new GridLength(edge), new GridLength(1, GridUnitType.Star), new GridLength(edge) })
            g.RowDefinitions.Add(new RowDefinition(r));

        (int x, int y, int cw, int ch)[] rects =
        {
            (0,0,i,i), (i,0,w-2*i,i), (w-i,0,i,i),
            (0,i,i,h-2*i), (i,i,w-2*i,h-2*i), (w-i,i,i,h-2*i),
            (0,h-i,i,i), (i,h-i,w-2*i,i), (w-i,h-i,i,i),
        };
        for (int idx = 0; idx < 9; idx++)
        {
            var r = rects[idx];
            var img = new Image
            {
                Source = new CroppedBitmap(src, new PixelRect(r.x, r.y, r.cw, r.ch)),
                Stretch = Stretch.Fill,
            };
            RenderOptions.SetBitmapInterpolationMode(img, BitmapInterpolationMode.None);  // crisp pixels
            Grid.SetColumn(img, idx % 3);
            Grid.SetRow(img, idx / 3);
            g.Children.Add(img);
        }
        return g;
    }

    private Control Line(string text, Color color, double size, double top = 0)
    {
        var host = new Grid { Margin = new Thickness(0, top, 0, 0), HorizontalAlignment = HorizontalAlignment.Left };
        TextBlock Make(Color c, double dx, double dy) => new()
        {
            Text = text,
            FontFamily = McFont,
            FontSize = size,
            Foreground = new SolidColorBrush(c),
            RenderTransform = new TranslateTransform(dx, dy),
        };
        host.Children.Add(Make(Shadow(color), 2, 2));
        host.Children.Add(Make(color, 0, 0));
        return host;
    }

    public void ShowToast()
    {
        // size first so we can place it against the right edge
        Measure(Size.Infinity);
        var want = DesiredSize;
        var screen = Screens.Primary ?? Screens.All.FirstOrDefault();
        var wa = screen?.WorkingArea ?? new PixelRect(0, 0, 1280, 720);
        double scale = screen?.Scaling ?? 1.0;

        int wpx = (int)(Math.Max(360, want.Width) * scale);
        _restX = wa.X + wa.Width - wpx - (int)(16 * scale);
        _offX = wa.X + wa.Width + 8;
        _y = wa.Y + (int)(16 * scale);

        Position = new PixelPoint(_offX, _y);
        Show();

        Media.PlaySound(System.IO.Path.Combine(AppContext.BaseDirectory, "assets", "advancement.wav"));

        Tween(_offX, _restX, 0.34, () =>
        {
            _dismiss = new DispatcherTimer { Interval = TimeSpan.FromSeconds(Math.Max(1, _seconds)) };
            _dismiss.Tick += (_, _) => Dismiss();
            _dismiss.Start();
        });
    }

    private void Dismiss()
    {
        if (_closing) return;
        _closing = true;
        _dismiss?.Stop();
        Tween(Position.X, _offX, 0.3, () => { try { Close(); } catch { } });
    }

    private void Tween(int from, int to, double seconds, Action? done)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var t = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(16) };
        t.Tick += (_, _) =>
        {
            double p = Math.Min(1, sw.Elapsed.TotalSeconds / seconds);
            double e = 1 - Math.Pow(1 - p, 3);
            Position = new PixelPoint((int)(from + (to - from) * e), _y);
            if (p >= 1) { t.Stop(); done?.Invoke(); }
        };
        t.Start();
    }

    private static string Trim(string s, int max) => s.Length <= max ? s : s[..(max - 3)].TrimEnd() + "...";
    private static Color Shadow(Color c) => Color.FromRgb((byte)(c.R / 4), (byte)(c.G / 4), (byte)(c.B / 4));
}
