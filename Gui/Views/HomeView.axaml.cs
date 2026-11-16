using System.Threading.Tasks;
using Avalonia.Animation;
using Avalonia.Animation.Easings;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Styling;
using OpioidMultitool.Core;

namespace OpioidMultitool.Gui.Views;

public partial class HomeView : UserControl
{
    private readonly MainWindow _owner;
    private CancellationTokenSource? _statusFade;

    public HomeView() : this(null!) { }

    public HomeView(MainWindow owner)
    {
        InitializeComponent();
        _owner = owner;
        Focusable = true;
        BuildMenu();
    }

    protected override void OnKeyDown(Avalonia.Input.KeyEventArgs e)
    {
        if (e.Key == Avalonia.Input.Key.Escape)
        {
            _owner?.Close();
            e.Handled = true;
        }
        base.OnKeyDown(e);
    }

    private void BuildMenu()
    {
        var items = MenuOptions.Items;
        for (int i = 0; i < items.Length; i++)
        {
            int index = i;
            bool isExit = i == items.Length - 1;
            string num = isExit ? "00" : (i + 1).ToString("00");

            var button = new Button
            {
                Classes = { "menuItem" },
                Content = $"[{num}]   {items[i]}",
            };
            if (isExit) button.Classes.Add("exit");
            button.Click += (_, _) => Activate(index);

            MenuItems.Children.Add(button);
        }
    }

    private void Activate(int index)
    {
        var items = MenuOptions.Items;
        if (index == 0)
        {
            _owner?.ShowWebsiteTester();
            return;
        }
        if (index == items.Length - 1)
        {
            _owner?.Close();
            return;
        }
        ShowStatus($"{items[index]} isn't ready yet... ( ´•ω•` )");
    }

    private async void ShowStatus(string text)
    {
        _statusFade?.Cancel();
        var cts = new CancellationTokenSource();
        _statusFade = cts;

        StatusText.Text = text;
        StatusText.Opacity = 0;
        var fadeIn = new Animation
        {
            Duration = TimeSpan.FromMilliseconds(180),
            Easing = new CubicEaseOut(),
            Children =
            {
                new KeyFrame { Cue = new Cue(0), Setters = { new Setter(OpacityProperty, 0d) } },
                new KeyFrame { Cue = new Cue(1), Setters = { new Setter(OpacityProperty, 1d) } },
            },
        };
        await fadeIn.RunAsync(StatusText, cts.Token);
        if (cts.IsCancellationRequested) return;

        try { await Task.Delay(2600, cts.Token); }
        catch (TaskCanceledException) { return; }

        var fadeOut = new Animation
        {
            Duration = TimeSpan.FromMilliseconds(300),
            Easing = new CubicEaseIn(),
            Children =
            {
                new KeyFrame { Cue = new Cue(0), Setters = { new Setter(OpacityProperty, 1d) } },
                new KeyFrame { Cue = new Cue(1), Setters = { new Setter(OpacityProperty, 0d) } },
            },
        };
        await fadeOut.RunAsync(StatusText, cts.Token);
    }
}
