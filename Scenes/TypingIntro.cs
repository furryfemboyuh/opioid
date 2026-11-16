using System.Diagnostics;
using System.Text;
using OpioidMultitool.Core;

namespace OpioidMultitool.Scenes;

/// <summary>Types out "made by opioid" in the middle of the screen with a blinking cursor.</summary>
internal static class TypingIntro
{
    private static readonly Rgb PrefixColor = new(215, 215, 215);
    private static readonly Rgb AccentColor = new(200, 0, 0);
    private static readonly Rgb CursorColor = new(255, 40, 40);

    public static void Run(string prefix, string accent)
    {
        string text = prefix + accent;
        var rng = new Random();
        bool skip = false;

        Term.Clear();

        // A few idle cursor blinks before typing starts.
        for (int i = 0; i < 4 && !skip; i++)
        {
            Draw(text, prefix.Length, 0, cursor: i % 2 == 0, fade: 1f);
            skip = Wait(280);
        }

        // Type it out.
        for (int n = 1; n <= text.Length && !skip; n++)
        {
            Draw(text, prefix.Length, n, cursor: true, fade: 1f);
            int delay = rng.Next(55, 135);
            if (text[n - 1] == ' ') delay += 120;      // little pause between words
            if (n == prefix.Length) delay += 220;      // dramatic beat before the name
            skip = Wait(delay);
        }

        // Hold with blinking cursor.
        for (int i = 0; i < 5 && !skip; i++)
        {
            Draw(text, prefix.Length, text.Length, cursor: i % 2 == 1, fade: 1f);
            skip = Wait(320);
        }

        // Fade out.
        if (!skip)
        {
            var sw = Stopwatch.StartNew();
            const float FadeMs = 900f;
            while (sw.ElapsedMilliseconds < FadeMs)
            {
                float k = 1f - Ease.Smooth(sw.ElapsedMilliseconds / FadeMs);
                Draw(text, prefix.Length, text.Length, cursor: false, fade: k);
                if (Wait(16)) break;
            }
        }

        Term.Clear();
    }

    private static void Draw(string text, int prefixLen, int shown, bool cursor, float fade)
    {
        int w = Term.Width, h = Term.Height;
        int row = h / 2;
        int col = Math.Max(0, (w - text.Length - 1) / 2);

        var sb = new StringBuilder();
        sb.Append(Term.MoveTo(row, 0)).Append(Term.ClearLine).Append(Term.MoveTo(row, col));

        for (int i = 0; i < shown; i++)
        {
            if (i == 0) sb.Append(Term.Fg(PrefixColor, fade));
            if (i == prefixLen) sb.Append(Term.Fg(AccentColor, fade)).Append("\x1b[1m");
            sb.Append(text[i]);
        }

        sb.Append(Term.Reset);
        if (cursor) sb.Append(Term.Fg(CursorColor, fade)).Append('█').Append(Term.Reset);

        Term.Write(sb);
        Term.Flush();
    }

    /// <summary>Sleeps, returning true early if the user pressed a key to skip.</summary>
    private static bool Wait(int ms)
    {
        var end = Environment.TickCount64 + ms;
        while (Environment.TickCount64 < end)
        {
            if (Input.AnyKey()) return true;
            Thread.Sleep(10);
        }
        return false;
    }
}
