using System.Windows;
using Microsoft.Windows.AppNotifications;
using Microsoft.Windows.AppNotifications.Builder;
using Application = System.Windows.Application;

namespace WinVClipboard;

public partial class App : Application
{
    private MainWindow? _window;
    private Mutex? _singleInstance;
    private System.Windows.Forms.NotifyIcon? _tray;
    private ReminderService? _reminders;
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        Localizer.Initialize();
        _singleInstance = new Mutex(true, "WinVClipboard.SingleInstance.4F1C58D2", out var isFirst);
        if (!isFirst) { Shutdown(); return; }
        _window = new MainWindow();
        InitializeTray();
        TryRegisterNotifications();
        _reminders = new ReminderService(ShowReminderNotification);
        var startHidden = e.Args.Any(arg => arg.Equals("--startup", StringComparison.OrdinalIgnoreCase));
        _window.InitializeInBackground(showImmediately: !startHidden);
        _ = UpdateService.CheckAtStartupAsync(_window);
    }

    private void ShowReminderNotification(ReminderItem reminder, bool wasMissed)
    {
        var heading = wasMissed ? "یادآور عقب‌افتاده" : "یادآور WinVClipboard";
        try
        {
            var notification = new AppNotificationBuilder()
                .AddText(heading)
                .AddText(reminder.Title)
                .BuildNotification();
            AppNotificationManager.Default.Show(notification);
            return;
        }
        catch { }
        if (_tray != null)
        {
            _tray.BalloonTipTitle = heading;
            _tray.BalloonTipText = reminder.Title;
            _tray.BalloonTipIcon = System.Windows.Forms.ToolTipIcon.Info;
            _tray.ShowBalloonTip(10000);
        }
    }

    private static void TryRegisterNotifications()
    {
        try { AppNotificationManager.Default.Register(); }
        catch { }
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
        _reminders?.Dispose();
        try { AppNotificationManager.Default.Unregister(); } catch { }
        if (_tray != null) { _tray.Visible = false; _tray.Dispose(); }
        _singleInstance?.Dispose(); base.OnExit(e);
    }
}
