namespace OpioidMultitool.Core;

internal static class Input
{
    public static bool TryRead(out ConsoleKeyInfo key)
    {
        try
        {
            if (Console.KeyAvailable)
            {
                key = Console.ReadKey(intercept: true);
                return true;
            }
        }
        catch (InvalidOperationException) { /* input redirected */ }

        key = default;
        return false;
    }

    /// <summary>Consumes all pending keys; returns true if any were pressed (used for "press any key to skip").</summary>
    public static bool AnyKey()
    {
        bool any = false;
        while (TryRead(out _)) any = true;
        return any;
    }

    public static void Drain() => AnyKey();
}
