using OpioidMultitool.Core;
using OpioidMultitool.Scenes;

namespace OpioidMultitool;

internal static class Program
{
    private static int Main()
    {
        try { Console.Title = "Opioid Multitool"; } catch { /* not supported everywhere */ }

        Native.ForceFullscreen();
        Term.Init();
        Console.CancelKeyPress += (_, _) =>
        {
            Term.Shutdown();
            Native.RestoreWindow();
        };

        try
        {
            MatrixRain.Run();
            TypingIntro.Run("made by ", "furryfemboyuh :3");
            TitleScreen.Run();
        }
        finally
        {
            Term.Shutdown();
            Native.RestoreWindow();
        }

        return 0;
    }
}
