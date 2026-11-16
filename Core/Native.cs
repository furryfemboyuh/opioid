using System.Runtime.InteropServices;

namespace OpioidMultitool.Core;

/// <summary>Win32 helpers for forcing the console fullscreen and enabling ANSI colors.</summary>
internal static class Native
{
    private const int SW_MAXIMIZE = 3;
    private const int STD_OUTPUT_HANDLE = -11;
    private const uint ENABLE_VIRTUAL_TERMINAL_PROCESSING = 0x0004;
    private const byte VK_MENU = 0x12;
    private const byte VK_RETURN = 0x0D;
    private const uint KEYEVENTF_KEYUP = 0x0002;

    [DllImport("kernel32.dll")] private static extern IntPtr GetConsoleWindow();
    [DllImport("kernel32.dll")] private static extern IntPtr GetStdHandle(int nStdHandle);
    [DllImport("kernel32.dll")] private static extern bool GetConsoleMode(IntPtr hConsole, out uint mode);
    [DllImport("kernel32.dll")] private static extern bool SetConsoleMode(IntPtr hConsole, uint mode);
    [DllImport("user32.dll")] private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);
    [DllImport("user32.dll")] private static extern bool SetForegroundWindow(IntPtr hWnd);
    [DllImport("user32.dll")] private static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, UIntPtr dwExtraInfo);

    /// <summary>
    /// Maximizes the console window, then simulates Alt+Enter which toggles true fullscreen
    /// in both classic conhost (cmd.exe) and Windows Terminal.
    /// </summary>
    public static void ForceFullscreen()
    {
        if (!OperatingSystem.IsWindows())
        {
            LinuxFullscreen.Enter();
            WaitForSizeToSettle();
            return;
        }

        var hwnd = GetConsoleWindow();
        if (hwnd != IntPtr.Zero)
        {
            ShowWindow(hwnd, SW_MAXIMIZE);
            SetForegroundWindow(hwnd);
        }
        Thread.Sleep(120);

        keybd_event(VK_MENU, 0x38, 0, UIntPtr.Zero);
        keybd_event(VK_RETURN, 0x1C, 0, UIntPtr.Zero);
        keybd_event(VK_RETURN, 0x1C, KEYEVENTF_KEYUP, UIntPtr.Zero);
        keybd_event(VK_MENU, 0x38, KEYEVENTF_KEYUP, UIntPtr.Zero);

        WaitForSizeToSettle();

        // Drop the scrollback so the buffer exactly matches the visible window.
        try { Console.SetBufferSize(Console.WindowWidth, Console.WindowHeight); } catch { }
    }

    /// <summary>Undoes fullscreen on Linux so the user's terminal returns to normal. (On Windows the window closes anyway.)</summary>
    public static void RestoreWindow()
    {
        if (!OperatingSystem.IsWindows()) LinuxFullscreen.Exit();
    }

    /// <summary>Lets cmd/conhost understand 24-bit ANSI color escape sequences.</summary>
    public static void EnableVirtualTerminal()
    {
        if (!OperatingSystem.IsWindows()) return;
        var hOut = GetStdHandle(STD_OUTPUT_HANDLE);
        if (GetConsoleMode(hOut, out var mode))
            SetConsoleMode(hOut, mode | ENABLE_VIRTUAL_TERMINAL_PROCESSING);
    }

    private static void WaitForSizeToSettle()
    {
        int lastW = -1, lastH = -1, stable = 0;
        var deadline = Environment.TickCount64 + 1500;
        while (Environment.TickCount64 < deadline && stable < 4)
        {
            Thread.Sleep(60);
            int w = Console.WindowWidth, h = Console.WindowHeight;
            if (w == lastW && h == lastH) stable++;
            else { stable = 0; lastW = w; lastH = h; }
        }
    }
}
