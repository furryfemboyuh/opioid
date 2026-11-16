using System.Text;

namespace OpioidMultitool.Core;

/// <summary>Small drawing helpers shared by the screens.</summary>
internal static class Ui
{
    /// <summary>Moves to the start of a row and clears it.</summary>
    public static void Line(StringBuilder sb, int row) =>
        sb.Append(Term.Reset).Append(Term.MoveTo(row, 0)).Append(Term.ClearLine);

    public static void Centered(StringBuilder sb, int row, int w, string text, string style)
    {
        Line(sb, row);
        if (text.Length == 0) return;
        if (text.Length > w - 1) text = text[..Math.Max(0, w - 1)];
        sb.Append(Term.MoveTo(row, Math.Max(0, (w - text.Length) / 2))).Append(style).Append(text).Append(Term.Reset);
    }

    /// <summary>Clears a row and writes text at a column, cut off at the screen edge.</summary>
    public static void At(StringBuilder sb, int row, int col, int w, string text, string style)
    {
        Line(sb, row);
        int max = w - col - 1;
        if (max <= 0 || text.Length == 0) return;
        if (text.Length > max) text = text[..max];
        sb.Append(Term.MoveTo(row, col)).Append(style).Append(text).Append(Term.Reset);
    }
}
