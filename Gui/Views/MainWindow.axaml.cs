using Avalonia;
using Avalonia.Animation;
using Avalonia.Controls;
using Avalonia.Input;

namespace OpioidMultitool.Gui.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();

        ViewHost.PageTransition = new CrossFade(TimeSpan.FromMilliseconds(220));
        ShowHome();
    }

    /// <summary>Swaps the page host back to the main menu.</summary>
    public void ShowHome() => ViewHost.Content = new HomeView(this);

    /// <summary>Swaps the page host to the website load tester.</summary>
    public void ShowWebsiteTester() => ViewHost.Content = new WebsiteTesterView(this);

    private void OnTitleBarPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            BeginMoveDrag(e);
    }

    private void OnMinimizeClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e) =>
        WindowState = WindowState.Minimized;

    private void OnCloseClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e) => Close();
}
