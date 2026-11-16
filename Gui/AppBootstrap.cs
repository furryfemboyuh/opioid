using Avalonia;

namespace OpioidMultitool.Gui;

/// <summary>Entry point for the Avalonia desktop GUI, launched from Program.cs via `opioid --gui`.</summary>
internal static class AppBootstrap
{
    public static int Run(string[] args) =>
        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);

    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();
}
