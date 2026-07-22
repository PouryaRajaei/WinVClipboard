using System.Windows;
using Application = System.Windows.Application;

namespace WinVClipboard;

public partial class App : Application
{
    private MainWindow? _window;
    private Mutex? _singleInstance;
    private System.Windows.Forms.NotifyIcon? _tray;
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        Localizer.Initialize();
        _singleInstance = new Mutex(true, "WinVClipboard.SingleInstance.4F1C58D2", out var isFirst);
        if (!isFirst) { Shutdown(); return; }
        _window = new MainWindow();
        InitializeTray();
        var startHidden = e.Args.Any(arg => arg.Equals("--startup", StringComparison.OrdinalIgnoreCase));
        _window.InitializeInBackground(showImmediately: !startHidden);
    }

    private void InitializeTray()
    {
        if (_window == null) return;
        var stream = GetResourceStream(new Uri("pack://application:,,,/Assets/WinVClipboard.ico"))?.Stream;
        _tray = new System.Windows.Forms.NotifyIcon
        {
            Text = "WinVClipboard",
            Icon = stream == null ? System.Drawing.SystemIcons.Application : new System.Drawing.Icon(stream),
            Visible = true
        };
        var menu = new System.Windows.Forms.ContextMenuStrip();
        menu.Items.Add(Localizer.T("Open"), null, (_, _) => _window.ShowFromTray());
        menu.Items.Add(Localizer.T("Settings"), null, (_, _) => _window.OpenSettingsFromTray());
        menu.Items.Add(new System.Windows.Forms.ToolStripSeparator());
        menu.Items.Add(Localizer.T("ExitFull"), null, (_, _) => _window.ExitFromTray());
        _tray.ContextMenuStrip = menu;
        _tray.DoubleClick += (_, _) => _window.ShowFromTray();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        if (_tray != null) { _tray.Visible = false; _tray.Dispose(); }
        _singleInstance?.Dispose(); base.OnExit(e);
    }
}
