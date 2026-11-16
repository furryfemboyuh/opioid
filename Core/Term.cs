using System.Text;

namespace OpioidMultitool.Core;

/// <summary>Buffered ANSI terminal output. Every frame is built up and flushed in one write to avoid flicker.</summary>
internal static class Term
{
    private static StreamWriter? _out;
    private static bool _shutdown;

    public static int Width => Math.Max(1, Console.WindowWidth);
    public static int Height => Math.Max(1, Console.WindowHeight);

    public static void Init()
    {
        Native.EnableVirtualTerminal();
        Console.OutputEncoding = new UTF8Encoding(false);
        _out = new StreamWriter(Console.OpenStandardOutput(), new UTF8Encoding(false), 1 << 16) { AutoFlush = false };
        try { Console.CursorVisible = false; } catch { }

        // Alternate screen buffer + hidden cursor.
        Write("\x1b[?1049h\x1b[?25l\x1b[0m\x1b[2J\x1b[H");
        Flush();
        Input.Drain(); // swallow anything left over from the simulated Alt+Enter
    }

    public static void Shutdown()
    {
        if (_shutdown || _out is null) return;
        _shutdown = true;
        Write("\x1b[0m\x1b[2J\x1b[?25h\x1b[?1049l");
        Flush();
        try { Console.CursorVisible = true; } catch { }
    }

    public static void Write(string s) => _out!.Write(s);
    public static void Write(StringBuilder sb) => _out!.Write(sb);
    public static void Flush() => _out!.Flush();

    public static void Clear()
    {
        Write("\x1b[0m\x1b[2J\x1b[H");
        Flush();
    }

    public static string MoveTo(int row, int col) => $"\x1b[{row + 1};{col + 1}H";
    public const string ClearLine = "\x1b[2K";
    public const string Reset = "\x1b[0m";

    public static string Fg(Rgb c, float k = 1f) => $"\x1b[38;2;{c.Scale(k)}m";
    public static string Bg(Rgb c, float k = 1f) => $"\x1b[48;2;{c.Scale(k)}m";
}

internal readonly record struct Rgb(int R, int G, int B)
{
    public static readonly Rgb White = new(240, 240, 240);
    public static readonly Rgb Black = new(0, 0, 0);

    public static Rgb Lerp(Rgb a, Rgb b, float t)
    {
        t = Math.Clamp(t, 0f, 1f);
        return new Rgb(
            (int)(a.R + (b.R - a.R) * t),
            (int)(a.G + (b.G - a.G) * t),
            (int)(a.B + (b.B - a.B) * t));
    }

    /// <summary>"r;g;b" scaled by k, ready to drop into an SGR sequence.</summary>
    public string Scale(float k)
    {
        k = Math.Clamp(k, 0f, 1f);
        return $"{(int)(R * k)};{(int)(G * k)};{(int)(B * k)}";
    }
}

internal static class Ease
{
    public static float Smooth(float t)
    {
        t = Math.Clamp(t, 0f, 1f);
        return t * t * (3f - 2f * t);
    }
}
