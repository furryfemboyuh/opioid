using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net;
using System.Text;
using OpioidMultitool.Core;
using static OpioidMultitool.Core.Ui;

namespace OpioidMultitool.Scenes;

/// <summary>
/// Asks for a URL, then sends a GET request every 500 ms (2 per second) and shows
/// live status codes, response times and failures until the user stops it.
/// </summary>
internal sealed class WebsiteTester
{
    private static readonly TimeSpan Interval = TimeSpan.FromMilliseconds(0.1);   // 10 requests every 0.1 ms
    private const int MaxInFlight = 16384; // slow site: skip a tick instead of piling up requests
    private const int Burst = 10;         // requests fired per interval
    private const int MaxLog = 200;

    private static readonly Rgb Title = new(255, 45, 45);
    private static readonly Rgb Shadow = new(70, 0, 0);
    private static readonly Rgb Label = new(120, 120, 120);
    private static readonly Rgb Value = new(225, 225, 225);
    private static readonly Rgb Good = new(90, 200, 120);
    private static readonly Rgb Warn = new(230, 180, 60);
    private static readonly Rgb Bad = new(255, 60, 60);

    private readonly record struct Result(DateTime At, int Status, string Text, long Ms, bool Error, bool Skipped);

    private readonly Uri _uri;
    private readonly ConcurrentQueue<Result> _incoming = new();
    private readonly List<Result> _log = [];
    private int _inFlight;

    private int _sent, _ok, _httpErrors, _failed, _skipped;
    private long _latSum, _latCount, _latMin = long.MaxValue, _latMax, _latLast;

    private WebsiteTester(Uri uri) => _uri = uri;

    public static void Run()
    {
        var uri = PromptUrl();
        if (uri is null) return;
        new WebsiteTester(uri).Test();
    }

    // ---------------------------------------------------------------- URL prompt

    private static Uri? PromptUrl()
    {
        var input = new StringBuilder();
        string error = "";
        bool first = true;

        while (true)
        {
            DrawPrompt(input.ToString(), error, first);
            first = false;

            ConsoleKeyInfo key;
            while (!Input.TryRead(out key)) Thread.Sleep(15);

            switch (key.Key)
            {
                case ConsoleKey.Escape:
                    return null;
                case ConsoleKey.Enter:
                    if (TryParseUrl(input.ToString(), out var uri)) return uri;
                    error = "That doesn't look like a valid http(s) URL.";
                    break;
                case ConsoleKey.Backspace:
                    if (input.Length > 0) input.Length--;
                    error = "";
                    break;
                default:
                    if (key.KeyChar >= ' ' && input.Length < 300)
                    {
                        input.Append(key.KeyChar);
                        error = "";
                    }
                    break;
            }
        }
    }

    private static bool TryParseUrl(string text, out Uri uri)
    {
        text = text.Trim();
        if (text.Length > 0 && !text.Contains("://")) text = "https://" + text;
        if (Uri.TryCreate(text, UriKind.Absolute, out uri!) &&
            (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps) &&
            !string.IsNullOrEmpty(uri.Host))
            return true;
        uri = null!;
        return false;
    }

    private static void DrawPrompt(string text, string error, bool fullClear)
    {
        int w = Term.Width, h = Term.Height;
        var sb = new StringBuilder();
        if (fullClear) sb.Append(Term.Reset).Append("\x1b[2J");

        int top = Math.Max(0, h / 2 - 4);
        Header(sb, top, w);

        const string prompt = "URL > ";
        int boxW = Math.Min(70, w - 4);
        int col = Math.Max(0, (w - boxW) / 2);
        string shown = text.Length > boxW - prompt.Length - 2 ? text[^(boxW - prompt.Length - 2)..] : text;

        At(sb, top + 3, col, w, prompt, Term.Fg(Bad) + "\x1b[1m");
        sb.Append(Term.MoveTo(top + 3, col + prompt.Length))
          .Append(Term.Fg(Value)).Append(shown)
          .Append(Term.Fg(Bad)).Append('█').Append(Term.Reset);

        At(sb, top + 4, col, w, new string('─', boxW), Term.Fg(Shadow));
        At(sb, top + 6, col, w, error.Length > 0 ? error : "e.g. example.com  (https:// is added if you leave it out)",
            Term.Fg(error.Length > 0 ? Bad : Label));
        Footer(sb, w, h, "ENTER start   ESC back");

        Term.Write(sb);
        Term.Flush();
    }

    // ---------------------------------------------------------------- running the test

    private void Test()
    {
        using var http = new HttpClient(new SocketsHttpHandler
        {
            AllowAutoRedirect = true,
            PooledConnectionLifetime = TimeSpan.FromMinutes(2),
        })
        {
            Timeout = TimeSpan.FromSeconds(10),
        };
        http.DefaultRequestHeaders.UserAgent.ParseAdd("OpioidMultitool/1.0 (website tester)");

        using var cts = new CancellationTokenSource();
        var clock = Stopwatch.StartNew();
        var scheduler = Task.Run(() => Schedule(http, cts.Token));

        bool firstDraw = true;
        int lastW = Term.Width, lastH = Term.Height;
        TimeSpan elapsed;

        while (true)
        {
            Collect();
            bool resized = Term.Width != lastW || Term.Height != lastH;
            lastW = Term.Width; lastH = Term.Height;
            Draw(running: true, clock.Elapsed, firstDraw || resized);
            firstDraw = false;

            if (Input.TryRead(out var key) &&
                key.Key is ConsoleKey.Escape or ConsoleKey.Q or ConsoleKey.Enter)
                break;
            Thread.Sleep(100);
        }

        elapsed = clock.Elapsed;
        cts.Cancel();
        try { scheduler.Wait(2000); } catch { }

        // Show the final numbers until a key is pressed.
        Input.Drain();
        bool full = true;
        while (true)
        {
            Collect();
            Draw(running: false, elapsed, full);
            full = false;
            if (Input.TryRead(out _)) break;
            Thread.Sleep(100);
        }
    }

    private async Task Schedule(HttpClient http, CancellationToken ct)
    {
        long period = (long)(Interval.TotalMilliseconds * Stopwatch.Frequency / 1000.0);
        long next = Stopwatch.GetTimestamp();

        while (!ct.IsCancellationRequested)
        {
            for (int i = 0; i < Burst && !ct.IsCancellationRequested; i++)
            {
                if (Volatile.Read(ref _inFlight) >= MaxInFlight)
                {
                    _incoming.Enqueue(new Result(DateTime.Now, 0, "skipped (too many requests still waiting)", 0, false, true));
                    continue;
                }
                Interlocked.Increment(ref _inFlight);
                _ = Send(http, ct);
            }

            next += period;
            while (Stopwatch.GetTimestamp() < next && !ct.IsCancellationRequested)
                Thread.SpinWait(50);
        }
    }

    private async Task Send(HttpClient http, CancellationToken ct)
    {
        var sw = Stopwatch.StartNew();
        try
        {
            using var resp = await http.GetAsync(_uri, HttpCompletionOption.ResponseHeadersRead, ct);
            int code = (int)resp.StatusCode;
            string text = resp.ReasonPhrase ?? ((HttpStatusCode)code).ToString();
            _incoming.Enqueue(new Result(DateTime.Now, code, text, sw.ElapsedMilliseconds, false, false));
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            // stopped by the user; don't count it
        }
        catch (TaskCanceledException)
        {
            _incoming.Enqueue(new Result(DateTime.Now, 0, "timed out (10s)", sw.ElapsedMilliseconds, true, false));
        }
        catch (HttpRequestException e)
        {
            _incoming.Enqueue(new Result(DateTime.Now, 0, Describe(e), sw.ElapsedMilliseconds, true, false));
        }
        catch (Exception e)
        {
            _incoming.Enqueue(new Result(DateTime.Now, 0, e.GetType().Name, sw.ElapsedMilliseconds, true, false));
        }
        finally
        {
            Interlocked.Decrement(ref _inFlight);
        }
    }

    private static string Describe(HttpRequestException e) => e.HttpRequestError switch
    {
        HttpRequestError.NameResolutionError => "DNS lookup failed",
        HttpRequestError.ConnectionError => "connection failed",
        HttpRequestError.SecureConnectionError => "TLS/SSL error",
        HttpRequestError.ResponseEnded => "connection closed early",
        _ => e.InnerException?.Message ?? e.Message,
    };

    private void Collect()
    {
        while (_incoming.TryDequeue(out var r))
        {
            if (r.Skipped) _skipped++;
            else
            {
                _sent++;
                if (r.Error) _failed++;
                else
                {
                    if (r.Status < 400) _ok++; else _httpErrors++;
                    _latLast = r.Ms;
                    _latSum += r.Ms;
                    _latCount++;
                    _latMin = Math.Min(_latMin, r.Ms);
                    _latMax = Math.Max(_latMax, r.Ms);
                }
            }
            _log.Insert(0, r);
            if (_log.Count > MaxLog) _log.RemoveAt(_log.Count - 1);
        }
    }

    // ---------------------------------------------------------------- drawing

    private void Draw(bool running, TimeSpan elapsed, bool fullClear)
    {
        int w = Term.Width, h = Term.Height;
        var sb = new StringBuilder(8192);
        if (fullClear) sb.Append(Term.Reset).Append("\x1b[2J");

        const int col = 4;
        Header(sb, 1, w);

        Field(sb, 4, col, w, "Target", _uri.ToString(), Value);
        Field(sb, 6, col, w, "Status",
            (running ? "● RUNNING  " : "■ STOPPED  ") + $"{(int)elapsed.TotalMinutes:00}:{elapsed.Seconds:00}",
            running ? Good : Warn);

        Line(sb, 8);
        sb.Append(Term.MoveTo(8, col))
          .Append(Stat("Sent", _sent, Value)).Append("   ")
          .Append(Stat("OK", _ok, Good)).Append("   ")
          .Append(Stat("HTTP errors", _httpErrors, _httpErrors > 0 ? Warn : Value)).Append("   ")
          .Append(Stat("Failed", _failed, _failed > 0 ? Bad : Value));
        if (_skipped > 0) sb.Append("   ").Append(Stat("Skipped", _skipped, Warn));

        string lat = _latCount == 0
            ? "-"
            : $"last {_latLast} ms   avg {_latSum / _latCount} ms   min {_latMin} ms   max {_latMax} ms";
        Field(sb, 9, col, w, "Latency", lat, Value);

        At(sb, 11, col, w, "Recent requests", Term.Fg(Title) + "\x1b[1m");
        At(sb, 12, col, w, new string('─', Math.Max(0, Math.Min(70, w - col - 2))), Term.Fg(Shadow));

        int logTop = 13, logBottom = h - 3;
        for (int row = logTop, i = 0; row <= logBottom; row++, i++)
        {
            if (i >= _log.Count) { Line(sb, row); continue; }
            var r = _log[i];
            string status = r.Skipped ? "SKIP" : r.Error ? "ERR " : r.Status.ToString().PadRight(4);
            Rgb color = r.Skipped ? Warn : r.Error ? Bad : r.Status >= 400 ? Warn : Good;
            string ms = r.Skipped ? "" : $"{r.Ms,6} ms   ";

            Line(sb, row);
            string line = $"{r.At:HH:mm:ss.fff}   ";
            sb.Append(Term.MoveTo(row, col)).Append(Term.Fg(Label)).Append(line)
              .Append(Term.Fg(color)).Append("\x1b[1m").Append(status).Append(Term.Reset)
              .Append(Term.Fg(Label)).Append("  ").Append(ms)
              .Append(Term.Fg(Value)).Append(Clip(r.Text, w - col - line.Length - 4 - 2 - ms.Length - 2))
              .Append(Term.Reset);
        }

        Footer(sb, w, h, running ? "ESC stop" : "Stopped - press any key to go back");

        Term.Write(sb);
        Term.Flush();
    }

    private static void Header(StringBuilder sb, int row, int w)
    {
        Centered(sb, row, w, "D D O S", Term.Fg(Title) + "\x1b[1m");
        Centered(sb, row + 1, w, new string('─', Math.Min(46, w - 2)), Term.Fg(Shadow));
    }

    private static void Footer(StringBuilder sb, int w, int h, string text)
    {
        Line(sb, h - 1);
        if (text.Length < w - 2) sb.Append(Term.MoveTo(h - 1, 1)).Append(Term.Fg(Label)).Append(text).Append(Term.Reset);
    }

    private static void Field(StringBuilder sb, int row, int col, int w, string label, string value, Rgb color)
    {
        Line(sb, row);
        sb.Append(Term.MoveTo(row, col)).Append(Term.Fg(Label)).Append(label.PadRight(10))
          .Append(Term.Fg(color)).Append(Clip(value, w - col - 12)).Append(Term.Reset);
    }

    private static string Stat(string label, int n, Rgb color) =>
        $"{Term.Fg(Label)}{label} {Term.Fg(color)}\x1b[1m{n}{Term.Reset}";

    private static string Clip(string s, int max) =>
        max <= 0 ? "" : s.Length <= max ? s : s[..Math.Max(0, max - 1)] + "…";
}
