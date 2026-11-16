using System.Diagnostics;
using System.Text.Json;

namespace OpioidMultitool.Core;

/// <summary>
/// Linux has no single "make my terminal fullscreen" API, so this asks whichever
/// window manager is running. Whatever it changes is undone on exit, so the user's
/// terminal goes back to how it was.
/// </summary>
internal static class LinuxFullscreen
{
    private static Action? _undo;

    public static void Enter()
    {
        // Hyprland
        if (HasEnv("HYPRLAND_INSTANCE_SIGNATURE") && OnPath("hyprctl"))
        {
            if (HyprIsFullscreen()) return; // already fullscreen, leave it alone
            Run("hyprctl", "dispatch fullscreen 0");
            _undo = () => Run("hyprctl", "dispatch fullscreen 0"); // toggle back
            return;
        }

        // Sway / i3-compatible
        if (HasEnv("SWAYSOCK") && OnPath("swaymsg"))
        {
            Run("swaymsg", "fullscreen enable");
            _undo = () => Run("swaymsg", "fullscreen disable");
            return;
        }

        // X11 (KDE/GNOME/XFCE on Xorg, etc.)
        if (HasEnv("DISPLAY") && !HasEnv("WAYLAND_DISPLAY") && OnPath("wmctrl"))
        {
            Run("wmctrl", "-r :ACTIVE: -b add,fullscreen");
            _undo = () => Run("wmctrl", "-r :ACTIVE: -b remove,fullscreen");
            return;
        }

        // Fallback: xterm window-op escape. Terminals that don't support it ignore it.
        Console.Out.Write("\x1b[10;1t");
        Console.Out.Flush();
        _undo = () => { Console.Out.Write("\x1b[10;0t"); Console.Out.Flush(); };
    }

    public static void Exit()
    {
        var undo = _undo;
        _undo = null;
        try { undo?.Invoke(); } catch { }
    }

    private static bool HyprIsFullscreen()
    {
        try
        {
            using var doc = JsonDocument.Parse(Run("hyprctl", "activewindow -j"));
            if (!doc.RootElement.TryGetProperty("fullscreen", out var fs)) return false;
            return fs.ValueKind switch
            {
                JsonValueKind.Number => fs.GetInt32() != 0,
                JsonValueKind.True => true,
                _ => false,
            };
        }
        catch { return false; }
    }

    private static bool HasEnv(string name) => !string.IsNullOrEmpty(Environment.GetEnvironmentVariable(name));

    private static bool OnPath(string exe) =>
        (Environment.GetEnvironmentVariable("PATH") ?? "")
            .Split(':', StringSplitOptions.RemoveEmptyEntries)
            .Any(dir => File.Exists(Path.Combine(dir, exe)));

    private static string Run(string file, string args)
    {
        try
        {
            using var p = Process.Start(new ProcessStartInfo(file, args)
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
            });
            if (p is null) return "";
            string output = p.StandardOutput.ReadToEnd();
            p.WaitForExit(1500);
            return output;
        }
        catch { return ""; }
    }
}
