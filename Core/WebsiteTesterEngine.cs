using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net;

namespace OpioidMultitool.Core;

/// <summary>
/// One sample from the request loop: either a completed request (ok/HTTP-error/failed)
/// or a dropped sample. UI-agnostic so both the TUI scene and the GUI can render it.
/// </summary>
internal readonly record struct ProbeResult(DateTime At, int Status, string Text, long Ms, bool Error, bool Skipped);

/// <summary>Running totals kept up to date as results stream in.</summary>
internal sealed class ProbeStats
{
    public int Sent, Ok, HttpErrors, Failed, Skipped;
    public long LatSum, LatCount, LatMin = long.MaxValue, LatMax, LatLast;

    public void Apply(ProbeResult r)
    {
        if (r.Skipped) { Skipped++; return; }
        Sent++;
        if (r.Error) { Failed++; return; }
        if (r.Status < 400) Ok++; else HttpErrors++;
        LatLast = r.Ms;
        LatSum += r.Ms;
        LatCount++;
        LatMin = Math.Min(LatMin, r.Ms);
        LatMax = Math.Max(LatMax, r.Ms);
    }
}

/// <summary>
/// Repeatedly GETs a URL with many concurrent workers and reports live status codes,
/// response times and failures until stopped. Pure engine: no console or UI code here,
/// so the TUI scene and the desktop GUI can share one implementation.
/// </summary>
internal sealed class WebsiteTesterEngine
{
    public const int Workers = 1024; // concurrent requests; throughput = Workers / RTT

    public Uri Target { get; }
    private readonly ConcurrentQueue<ProbeResult> _incoming = new();

    public WebsiteTesterEngine(Uri target) => Target = target;

    /// <summary>Drains every result that has arrived since the last call.</summary>
    public IEnumerable<ProbeResult> Drain()
    {
        while (_incoming.TryDequeue(out var r)) yield return r;
    }

    /// <summary>Runs the flood until <paramref name="ct"/> is cancelled. Safe to await from a background task.</summary>
    public async Task RunAsync(CancellationToken ct)
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

        var workers = new Task[Workers];
        for (int i = 0; i < workers.Length; i++) workers[i] = Worker(http, ct);
        try { await Task.WhenAll(workers); } catch { }
    }

    private async Task Worker(HttpClient http, CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
            await Send(http, ct);
    }

    private async Task Send(HttpClient http, CancellationToken ct)
    {
        var sw = Stopwatch.StartNew();
        try
        {
            using var resp = await http.GetAsync(Target, HttpCompletionOption.ResponseHeadersRead, ct);
            int code = (int)resp.StatusCode;
            string text = resp.ReasonPhrase ?? ((HttpStatusCode)code).ToString();
            _incoming.Enqueue(new ProbeResult(DateTime.Now, code, text, sw.ElapsedMilliseconds, false, false));
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            // stopped by the user; don't count it
        }
        catch (TaskCanceledException)
        {
            _incoming.Enqueue(new ProbeResult(DateTime.Now, 0, "timed out (10s)", sw.ElapsedMilliseconds, true, false));
        }
        catch (HttpRequestException e)
        {
            _incoming.Enqueue(new ProbeResult(DateTime.Now, 0, Describe(e), sw.ElapsedMilliseconds, true, false));
        }
        catch (Exception e)
        {
            _incoming.Enqueue(new ProbeResult(DateTime.Now, 0, e.GetType().Name, sw.ElapsedMilliseconds, true, false));
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

    public static bool TryParseUrl(string text, out Uri uri)
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
}
