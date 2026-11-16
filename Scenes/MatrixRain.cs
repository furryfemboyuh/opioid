using System.Diagnostics;
using System.Text;
using OpioidMultitool.Core;

namespace OpioidMultitool.Scenes;

/// <summary>
/// Falling "matrix" text. Starts white, bleeds into dark red, then fades to black.
/// Press any key to skip.
/// </summary>
internal sealed class MatrixRain
{
    private const string Glyphs = "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZ@#$%&*+=-<>?/\\|{}[]~^!;:";

    // Timeline (seconds)
    private const float FadeIn = 0.4f;
    private const float WhiteHold = 1.8f;
    private const float ToRed = 2.6f;
    private const float FadeOut = 2.2f;

    private const float Decay = 0.925f;   // trail falloff per frame
    private const int FrameMs = 16;       // ~60 fps

    private static readonly Rgb DarkRed = new(130, 0, 90);
    private static readonly Rgb HeadRed = new(255, 110, 190);

    private readonly Random _rng = new();
    private readonly StringBuilder _sb = new(1 << 18);

    private int _w, _h;
    private char[,] _chars = new char[0, 0];
    private float[,] _bright = new float[0, 0];
    private float[] _dropY = [];
    private float[] _dropSpeed = [];
    private bool[] _dropAlive = [];

    public static void Run() => new MatrixRain().Loop();

    private void Loop()
    {
        Term.Clear();
        Allocate();

        var clock = Stopwatch.StartNew();
        while (true)
        {
            long frameStart = clock.ElapsedMilliseconds;
            float t = (float)clock.Elapsed.TotalSeconds;

            if (Input.AnyKey()) break;
            if (Term.Width != _w || Term.Height != _h) Allocate();

            // Work out where we are on the color timeline.
            Rgb baseColor, headColor;
            float global = 1f;
            bool spawning = true;

            if (t < WhiteHold)
            {
                baseColor = Rgb.White;
                headColor = new Rgb(255, 255, 255);
                global = Math.Min(1f, t / FadeIn);
            }
            else if (t < WhiteHold + ToRed)
            {
                float k = Ease.Smooth((t - WhiteHold) / ToRed);
                baseColor = Rgb.Lerp(Rgb.White, DarkRed, k);
                headColor = Rgb.Lerp(new Rgb(255, 255, 255), HeadRed, k);
            }
            else
            {
                baseColor = DarkRed;
                headColor = HeadRed;
                spawning = false;
                global = 1f - (t - WhiteHold - ToRed) / FadeOut;
                if (global <= 0f) break;
            }

            Step(spawning);
            Render(baseColor, headColor, global);

            int sleep = FrameMs - (int)(clock.ElapsedMilliseconds - frameStart);
            if (sleep > 0) Thread.Sleep(sleep);
        }

        Term.Clear();
    }

    private void Allocate()
    {
        _w = Term.Width;
        _h = Term.Height;
        _chars = new char[_w, _h];
        _bright = new float[_w, _h];
        _dropY = new float[_w];
        _dropSpeed = new float[_w];
        _dropAlive = new bool[_w];
        for (int c = 0; c < _w; c++) Spawn(c, initial: true);
        Term.Clear();
    }

    private void Spawn(int c, bool initial)
    {
        _dropY[c] = initial ? -_rng.Next(0, (int)(_h * 1.2f) + 1) : -_rng.Next(0, _h / 2 + 1);
        _dropSpeed[c] = 0.25f + (float)_rng.NextDouble() * 0.7f;
        _dropAlive[c] = true;
    }

    private char RandomGlyph() => Glyphs[_rng.Next(Glyphs.Length)];

    private void Step(bool spawning)
    {
        // Fade trails + occasional glyph flicker.
        for (int c = 0; c < _w; c++)
        for (int r = 0; r < _h; r++)
        {
            ref float b = ref _bright[c, r];
            if (b <= 0.01f) { b = 0f; continue; }
            b *= Decay;
            if (_rng.NextDouble() < 0.02) _chars[c, r] = RandomGlyph();
        }

        // Advance drops, painting every row they pass through.
        for (int c = 0; c < _w; c++)
        {
            if (!_dropAlive[c]) continue;

            float y = _dropY[c];
            float ny = y + _dropSpeed[c];
            int from = (int)MathF.Floor(y) + 1;
            int to = (int)MathF.Floor(ny);
            for (int r = from; r <= to; r++)
            {
                if (r < 0 || r >= _h) continue;
                _chars[c, r] = RandomGlyph();
                _bright[c, r] = 1f;
            }
            _dropY[c] = ny;

            if (to > _h + 45) // trail has fully faded off-screen
            {
                if (spawning) Spawn(c, initial: false);
                else _dropAlive[c] = false;
            }
        }
    }

    private void Render(Rgb baseColor, Rgb headColor, float global)
    {
        const int Levels = 24;
        _sb.Clear();

        for (int r = 0; r < _h; r++)
        {
            _sb.Append(Term.MoveTo(r, 0));
            int lastKey = -1;
            // Never write the bottom-right cell; some consoles scroll when you do.
            int cols = r == _h - 1 ? _w - 1 : _w;

            for (int c = 0; c < cols; c++)
            {
                float b = _bright[c, r] * global;
                if (b < 0.035f)
                {
                    _sb.Append(' ');
                    continue;
                }

                bool isHead = _dropAlive[c] && (int)MathF.Floor(_dropY[c]) == r;
                int level = Math.Max(1, (int)(b * Levels));
                int key = isHead ? -2 : level;

                if (key != lastKey)
                {
                    _sb.Append(isHead
                        ? Term.Fg(headColor, global)
                        : Term.Fg(baseColor, (float)level / Levels));
                    lastKey = key;
                }
                _sb.Append(_chars[c, r]);
            }
        }

        Term.Write(_sb);
        Term.Flush();
    }
}
