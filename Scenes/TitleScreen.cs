using System.Diagnostics;
using System.Text;
using OpioidMultitool.Core;
using static OpioidMultitool.Core.Ui;

namespace OpioidMultitool.Scenes;

/// <summary>Main menu with a big OPIOID banner and placeholder options.</summary>
internal static class TitleScreen
{
    /// <summary>Version passed in by the `opioid` launcher, or the one baked into the build.</summary>
    private static readonly string Version = ResolveVersion();

    /// <summary>Set by the launcher when GitHub couldn't be reached and a cached build is running.</summary>
    private static readonly bool Offline =
        Environment.GetEnvironmentVariable("OPIOID_UPDATE_STATUS") == "offline";

    private static string ResolveVersion()
    {
        var fromLauncher = Environment.GetEnvironmentVariable("OPIOID_VERSION");
        if (!string.IsNullOrWhiteSpace(fromLauncher)) return fromLauncher;
        var v = typeof(TitleScreen).Assembly
            .GetCustomAttributes(typeof(System.Reflection.AssemblyInformationalVersionAttribute), false)
            .OfType<System.Reflection.AssemblyInformationalVersionAttribute>()
            .FirstOrDefault()?.InformationalVersion ?? "0.0.0";
        int plus = v.IndexOf('+');
        return "v" + (plus >= 0 ? v[..plus] : v);
    }

    private static readonly string[] Options = MenuOptions.Items;

    private static readonly Rgb BannerTop = new(255, 120, 200);
    private static readonly Rgb BannerBottom = new(190, 30, 130);
    private static readonly Rgb Shadow = new(100, 0, 75);
    private static readonly Rgb Grey = new(210, 175, 215);
    private static readonly Rgb DimGrey = new(125, 95, 130);
    private static readonly Rgb Red = new(225, 70, 160);
    private static readonly Rgb BrightRed = new(255, 155, 220);
    private static readonly Rgb SelectBg = new(95, 0, 80);

    private static readonly string[] Banner = BuildBanner();
    private static readonly int BannerWidth = Banner.Max(l => l.Length);
    private const int MenuWidth = 34;

    public static void Run()
    {
        Input.Drain();
        int sel = 0;
        string status = "";

        Animate(sel, status, fadeIn: true);

        int lastW = Term.Width, lastH = Term.Height;
        Draw(sel, status, 1f, fullClear: true);

        while (true)
        {
            if (Term.Width != lastW || Term.Height != lastH)
            {
                lastW = Term.Width;
                lastH = Term.Height;
                Draw(sel, status, 1f, fullClear: true);
            }

            if (!Input.TryRead(out var key))
            {
                Thread.Sleep(25);
                continue;
            }

            int activate = -1;
            switch (key.Key)
            {
                case ConsoleKey.UpArrow or ConsoleKey.W:
                    sel = (sel - 1 + Options.Length) % Options.Length;
                    status = "";
                    break;
                case ConsoleKey.DownArrow or ConsoleKey.S:
                    sel = (sel + 1) % Options.Length;
                    status = "";
                    break;
                case ConsoleKey.Enter or ConsoleKey.Spacebar:
                    activate = sel;
                    break;
                case ConsoleKey.Escape:
                    activate = Options.Length - 1;
                    break;
                default:
                    if (key.KeyChar == '0') activate = Options.Length - 1;
                    else if (key.KeyChar is >= '1' and <= '9' && key.KeyChar - '1' < Options.Length - 1)
                        activate = key.KeyChar - '1';
                    break;
            }

            if (activate >= 0)
            {
                sel = activate;
                if (activate == Options.Length - 1)
                {
                    Animate(sel, "", fadeIn: false);
                    return;
                }
                if (activate == 0)
                {
                    WebsiteTester.Run();
                    Input.Drain();
                    status = "";
                    Draw(sel, status, 1f, fullClear: true);
                    continue;
                }
                status = $"{Options[activate]} isn't ready yet... ( ´•ω•` )";
            }

            Draw(sel, status, 1f, fullClear: false);
        }
    }

    private static void Animate(int sel, string status, bool fadeIn)
    {
        const float Ms = 650f;
        var sw = Stopwatch.StartNew();
        bool first = true;
        while (sw.ElapsedMilliseconds < Ms)
        {
            float t = Ease.Smooth(sw.ElapsedMilliseconds / Ms);
            Draw(sel, status, fadeIn ? t : 1f - t, fullClear: first);
            first = false;
            Thread.Sleep(16);
        }
    }

    private static void Draw(int sel, string status, float f, bool fullClear)
    {
        int w = Term.Width, h = Term.Height;
        var sb = new StringBuilder(8192);
        if (fullClear) sb.Append(Term.Reset).Append("\x1b[2J");

        bool bigBanner = w >= BannerWidth + 2;
        int bannerH = bigBanner ? Banner.Length : 1;
        // banner, blank, subtitle, divider, blank, menu..., blank, status
        int blockH = bannerH + 4 + Options.Length + 2 + (Offline ? 2 : 0);
        int top = Math.Max(0, (h - blockH) / 2);
        int row = top;

        // --- Banner ---
        if (bigBanner)
        {
            int col = (w - BannerWidth) / 2;
            for (int i = 0; i < Banner.Length; i++)
            {
                var main = Rgb.Lerp(BannerTop, BannerBottom, i / (float)(Banner.Length - 1));
                Line(sb, row++);
                sb.Append(Term.MoveTo(row - 1, col));
                bool? lastSolid = null;
                foreach (char ch in Banner[i])
                {
                    bool solid = ch == '█';
                    if (ch != ' ' && solid != lastSolid)
                    {
                        sb.Append(Term.Fg(solid ? main : Shadow, f));
                        lastSolid = solid;
                    }
                    sb.Append(ch);
                }
            }
        }
        else
        {
            Centered(sb, row++, w, "OPIOID", Term.Fg(BannerTop, f) + "\x1b[1m");
        }

        row++;
        Centered(sb, row++, w, "✦  M U L T I T O O L  ✦", Term.Fg(Grey, f));
        Centered(sb, row++, w, new string('·', Math.Min(BannerWidth, w - 2)), Term.Fg(Shadow, f));
        row++;

        // --- Offline warning ---
        if (Offline)
        {
            Centered(sb, row++, w, $" ! GitHub unreachable - running cached {Version} ! ",
                Term.Bg(SelectBg, f) + Term.Fg(new Rgb(255, 255, 255), f) + "\x1b[1m");
            row++;
        }

        // --- Menu ---
        int menuCol = Math.Max(0, (w - MenuWidth) / 2);
        for (int i = 0; i < Options.Length; i++)
        {
            bool isExit = i == Options.Length - 1;
            string num = isExit ? "00" : (i + 1).ToString("00");
            string label = $" [{num}]  {Options[i]}".PadRight(MenuWidth - 3);

            Line(sb, row);
            sb.Append(Term.MoveTo(row++, menuCol));
            if (i == sel)
            {
                sb.Append(Term.Fg(BrightRed, f)).Append(" » ")
                  .Append(Term.Bg(SelectBg, f)).Append(Term.Fg(new Rgb(255, 255, 255), f)).Append("\x1b[1m")
                  .Append(label).Append(Term.Reset);
            }
            else
            {
                sb.Append("   ")
                  .Append(Term.Fg(Red, f)).Append($" [{num}]")
                  .Append(Term.Fg(isExit ? DimGrey : Grey, f)).Append($"  {Options[i]}");
            }
        }

        // --- Status line ---
        row++;
        Centered(sb, row, w, status, Term.Fg(BrightRed, f));

        // --- Footer ---
        if (h > blockH + 2)
        {
            int fr = h - 1;
            Line(sb, fr);
            string hints = "↑/↓ navigate   ENTER select   ESC exit  :3";
            sb.Append(Term.MoveTo(fr, 1)).Append(Term.Fg(DimGrey, f)).Append(Version);
            int hc = w - hints.Length - 2;
            if (hc > Version.Length + 3)
                sb.Append(Term.MoveTo(fr, hc)).Append(Term.Fg(DimGrey, f)).Append(hints);
        }

        sb.Append(Term.Reset);
        Term.Write(sb);
        Term.Flush();
    }

    private static string[] BuildBanner()
    {
        string[] o =
        [
            " ██████╗ ",
            "██╔═══██╗",
            "██║   ██║",
            "██║   ██║",
            "╚██████╔╝",
            " ╚═════╝ ",
        ];
        string[] p =
        [
            "██████╗ ",
            "██╔══██╗",
            "██████╔╝",
            "██╔═══╝ ",
            "██║     ",
            "╚═╝     ",
        ];
        string[] i =
        [
            "██╗",
            "██║",
            "██║",
            "██║",
            "██║",
            "╚═╝",
        ];
        string[] d =
        [
            "██████╗ ",
            "██╔══██╗",
            "██║  ██║",
            "██║  ██║",
            "██████╔╝",
            "╚═════╝ ",
        ];

        string[][] word = [o, p, i, o, i, d];
        var lines = new string[6];
        for (int r = 0; r < 6; r++)
            lines[r] = string.Concat(word.Select(letter => letter[r])).TrimEnd();
        return lines;
    }
}
