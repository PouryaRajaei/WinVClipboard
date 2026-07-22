using System.Windows;

namespace WinVClipboard;

public partial class App : Application
{
    private MainWindow? _window;
    private Mutex? _singleInstance;
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        _singleInstance = new Mutex(true, "WinVClipboard.SingleInstance.4F1C58D2", out var isFirst);
        if (!isFirst) { Shutdown(); return; }
        _window = new MainWindow();
        var startHidden = e.Args.Any(arg => arg.Equals("--startup", StringComparison.OrdinalIgnoreCase));
        _window.InitializeInBackground(showImmediately: !startHidden);
    }
}
