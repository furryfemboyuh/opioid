using System.Diagnostics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Threading;
using OpioidMultitool.Core;

namespace OpioidMultitool.Gui.Views;

public partial class WebsiteTesterView : UserControl
{
    private const int MaxLogRows = 200;

    private static readonly IBrush Good = new SolidColorBrush(Color.Parse("#5AC878"));
    private static readonly IBrush Warn = new SolidColorBrush(Color.Parse("#E6B43C"));
    private static readonly IBrush Bad = new SolidColorBrush(Color.Parse("#FF3C3C"));
    private static readonly IBrush Label = new SolidColorBrush(Color.Parse("#8C7A94"));
    private static readonly IBrush Value = new SolidColorBrush(Color.Parse("#E8D8E4"));

    private readonly MainWindow _owner;
    private readonly ProbeStats _stats = new();
    private readonly Stopwatch _clock = new();

    private WebsiteTesterEngine? _engine;
    private CancellationTokenSource? _cts;
    private Task? _runTask;
    private DispatcherTimer? _timer;
    private bool _running;

    public WebsiteTesterView() : this(null!) { }

    public WebsiteTesterView(MainWindow owner)
    {
        InitializeComponent();
        _owner = owner;
        Focusable = true;
        AttachedToVisualTree += (_, _) => Focus();
        UrlBox.Focus();
    }

    private void OnUrlKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter) TryStart();
        else if (e.Key == Key.Escape) GoBack();
    }

    private void OnStartClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e) => TryStart();

    private void OnBackClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e) => GoBack();

    private void TryStart()
    {
        if (!WebsiteTesterEngine.TryParseUrl(UrlBox.Text ?? "", out var uri))
        {
            PromptError.Text = "That doesn't look like a valid http(s) URL.";
            PromptError.IsVisible = true;
            return;
        }

        PromptPanel.IsVisible = false;
        RunPanel.IsVisible = true;
        TargetText.Text = $"TARGET   {uri}";
        LatencyText.Text = "LATENCY  -";
        SentText.Text = OkText.Text = HttpErrText.Text = FailedText.Text = "0";
        LogItems.Items.Clear();
        StopButton.Content = "STOP";

        _engine = new WebsiteTesterEngine(uri);
        _cts = new CancellationTokenSource();
        _clock.Restart();
        _running = true;

        _runTask = Task.Run(() => _engine.RunAsync(_cts.Token));

        _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(100) };
        _timer.Tick += (_, _) => Tick();
        _timer.Start();
    }

    private void Tick()
    {
        if (_engine is null) return;

        foreach (var r in _engine.Drain())
        {
            _stats.Apply(r);

            var row = new TextBlock
            {
                Classes = { "logRow" },
                Text = FormatRow(r),
                Foreground = r.Skipped ? Warn : r.Error ? Bad : r.Status >= 400 ? Warn : Good,
            };
            LogItems.Items.Insert(0, row);
            while (LogItems.Items.Count > MaxLogRows) LogItems.Items.RemoveAt(LogItems.Items.Count - 1);
        }

        SentText.Text = _stats.Sent.ToString();
        OkText.Text = _stats.Ok.ToString();
        HttpErrText.Text = _stats.HttpErrors.ToString();
        FailedText.Text = _stats.Failed.ToString();
        LatencyText.Text = _stats.LatCount == 0
            ? "LATENCY  -"
            : $"LATENCY  last {_stats.LatLast} ms   avg {_stats.LatSum / _stats.LatCount} ms   min {_stats.LatMin} ms   max {_stats.LatMax} ms";

        var e = _clock.Elapsed;
        ClockText.Text = $"{(_running ? "●" : "■")} {(int)e.TotalMinutes:00}:{e.Seconds:00}";
    }

    private void OnStopClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (_running) Stop();
        else GoBack();
    }

    private void Stop()
    {
        _running = false;
        _clock.Stop();
        _cts?.Cancel();
        _timer?.Stop();
        try { _runTask?.Wait(2000); } catch { }
        Tick();
        StopButton.Content = "BACK";
    }

    private void GoBack()
    {
        if (_running) Stop();
        _owner?.ShowHome();
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            if (RunPanel.IsVisible) { if (_running) Stop(); else GoBack(); }
            else GoBack();
            e.Handled = true;
        }
        base.OnKeyDown(e);
    }

    private static string FormatRow(ProbeResult r)
    {
        string status = r.Skipped ? "SKIP" : r.Error ? "ERR " : r.Status.ToString().PadRight(4);
        string ms = r.Skipped ? "" : $"{r.Ms,6} ms  ";
        return $"{r.At:HH:mm:ss.fff}   {status}   {ms}{r.Text}";
    }
}
