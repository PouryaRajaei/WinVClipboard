using Microsoft.Win32;
using System.Diagnostics;
using System.Net.Http;
using System.Reflection;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Application = System.Windows.Application;
using Button = System.Windows.Controls.Button;
using CheckBox = System.Windows.Controls.CheckBox;
using ComboBox = System.Windows.Controls.ComboBox;
using TextBox = System.Windows.Controls.TextBox;

namespace WinVClipboard;

public sealed class SettingsWindow : Window
{
    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string RunValue = "WinVClipboard";
    private readonly MainWindow _main;
    private readonly CheckBox _startup, _pause, _images;
    private readonly TextBox _maxHistory, _days, _excluded;
    private readonly ComboBox _language, _size, _theme, _thumbnail, _hotkey, _pinnedModifier;

    public SettingsWindow(MainWindow main)
    {
        _main = main;
        Title = Localizer.T("Settings"); Width = 650; Height = 570; WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Owner = main; ShowInTaskbar = false; Background = (Brush)Application.Current.Resources["PanelBrush"];
        Foreground = (Brush)Application.Current.Resources["PrimaryTextBrush"]; FlowDirection = Localizer.Direction;

        var root = new DockPanel { Margin = new Thickness(18) };
        var actions = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 14, 0, 0) };
        var save = Button(Localizer.T("Save")); save.Background = new SolidColorBrush(Color.FromRgb(42, 112, 180)); save.Click += Save_Click;
        var cancel = Button(Localizer.T("Cancel")); cancel.Click += (_, _) => Close(); actions.Children.Add(cancel); actions.Children.Add(save);
        DockPanel.SetDock(actions, Dock.Bottom); root.Children.Add(actions);

        var tabs = new TabControl();
        var general = Panel();
        general.Children.Add(Label(Localizer.T("Language"))); _language = Combo(("فارسی", "fa"), ("English", "en")); Select(_language, Localizer.IsPersian ? "fa" : "en"); general.Children.Add(_language);
        _startup = Check(Localizer.T("StartWithWindows"), IsStartupEnabled());
        _pause = Check(Localizer.T("PauseCapture"), Localizer.PauseCapture);
        general.Children.Add(_startup); general.Children.Add(_pause);
        general.Children.Add(Label(Localizer.T("PanelSize"))); _size = Combo(("Small", PanelSize.Small.ToString()), ("Medium", PanelSize.Medium.ToString()), ("Large", PanelSize.Large.ToString())); Select(_size, Localizer.CurrentPanelSize.ToString()); general.Children.Add(_size);
        general.Children.Add(Label(Localizer.T("MaxHistory"))); _maxHistory = Input(Localizer.MaxHistory.ToString()); general.Children.Add(_maxHistory);
        general.Children.Add(Label(Localizer.T("ShowHotkey"))); _hotkey = Combo(("Win + V", "Win+V"), ("Win + C", "Win+C")); Select(_hotkey, Localizer.ShowHotkey); general.Children.Add(_hotkey);
        general.Children.Add(Label(Localizer.T("PinnedModifier"))); _pinnedModifier = Combo(("Ctrl + 1…9", "Ctrl"), ("Alt + 1…9", "Alt")); Select(_pinnedModifier, Localizer.PinnedModifier); general.Children.Add(_pinnedModifier);
        var shortcuts = Button(Localizer.T("TextShortcuts")); shortcuts.Click += (_, _) => _main.OpenTextShortcutsFromSettings(); general.Children.Add(shortcuts);
        var clear = Button(Localizer.T("ClearUnpinned")); clear.Click += (_, _) => _main.ClearUnpinnedFromSettings(); general.Children.Add(clear);
        tabs.Items.Add(Tab(Localizer.T("General"), general));

        var appearance = Panel();
        appearance.Children.Add(Label(Localizer.T("Theme"))); _theme = Combo(("Dark / " + Localizer.T("Dark"), "Dark"), ("Light / " + Localizer.T("Light"), "Light"), (Localizer.T("System"), "System")); Select(_theme, Localizer.Theme); appearance.Children.Add(_theme);
        appearance.Children.Add(Label(Localizer.T("ThumbnailSize"))); _thumbnail = Combo(("Small · 180 px", "180"), ("Medium · 245 px", "245"), ("Large · 360 px", "360")); Select(_thumbnail, Localizer.ThumbnailSize.ToString()); appearance.Children.Add(_thumbnail);
        tabs.Items.Add(Tab(Localizer.T("Appearance"), appearance));

        var privacy = Panel();
        _images = Check(Localizer.T("CaptureImages"), Localizer.CaptureImages); privacy.Children.Add(_images);
        privacy.Children.Add(Label(Localizer.T("AutoDeleteDays"))); _days = Input(Localizer.AutoDeleteDays.ToString()); privacy.Children.Add(_days);
        privacy.Children.Add(Label(Localizer.T("ExcludedApps"))); _excluded = Input(Localizer.ExcludedApps); _excluded.AcceptsReturn = true; _excluded.Height = 90; privacy.Children.Add(_excluded);
        tabs.Items.Add(Tab(Localizer.T("Privacy"), privacy));

        var backup = Panel();
        var export = Button(Localizer.T("ExportBackup")); export.Click += (_, _) => _main.ExportBackup();
        var import = Button(Localizer.T("ImportBackup")); import.Click += (_, _) => _main.ImportBackup();
        backup.Children.Add(export); backup.Children.Add(import);
        tabs.Items.Add(Tab(Localizer.T("Backup"), backup));

        var about = Panel();
        var version = Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "";
        about.Children.Add(new TextBlock { Text = $"WinVClipboard  {version}", FontSize = 24, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 0, 0, 14) });
        about.Children.Add(new TextBlock { Text = "Pourya Rajaei\nhttps://github.com/PouryaRajaei/WinVClipboard", TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 18) });
        var update = Button(Localizer.T("CheckUpdates")); update.Click += CheckUpdates_Click; about.Children.Add(update);
        tabs.Items.Add(Tab(Localizer.T("About"), about));
        root.Children.Add(tabs); Content = root;
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        var wantsPersian = Value(_language) == "fa";
        if (wantsPersian != Localizer.IsPersian) Localizer.Toggle();
        Localizer.PauseCapture = _pause.IsChecked == true; Localizer.CaptureImages = _images.IsChecked == true;
        if (int.TryParse(_maxHistory.Text, out var max)) Localizer.MaxHistory = Math.Clamp(max, 50, 10000);
        if (int.TryParse(_days.Text, out var days)) Localizer.AutoDeleteDays = Math.Clamp(days, 0, 3650);
        Localizer.ExcludedApps = _excluded.Text.Trim(); Localizer.Theme = Value(_theme);
        if (int.TryParse(Value(_thumbnail), out var thumbnail)) Localizer.ThumbnailSize = thumbnail;
        Localizer.ShowHotkey = Value(_hotkey); Localizer.PinnedModifier = Value(_pinnedModifier);
        if (Enum.TryParse<PanelSize>(Value(_size), out var panelSize)) Localizer.SetPanelSize(panelSize);
        Localizer.Save(); SetStartup(_startup.IsChecked == true); _main.ApplySettings(); Close();
    }

    private async void CheckUpdates_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            ((Button)sender).IsEnabled = false;
            using var client = new HttpClient(); client.DefaultRequestHeaders.UserAgent.ParseAdd("WinVClipboard/2.0");
            var json = await client.GetStringAsync("https://api.github.com/repos/PouryaRajaei/WinVClipboard/releases/latest");
            using var document = JsonDocument.Parse(json); var tag = document.RootElement.GetProperty("tag_name").GetString()?.TrimStart('v') ?? "0.0.0";
            var current = Assembly.GetExecutingAssembly().GetName().Version ?? new Version();
            if (Version.TryParse(tag, out var latest) && latest > current)
            {
                if (MessageBox.Show($"{Localizer.T("UpdateAvailable")}: {tag}\nGitHub Releases?", "WinVClipboard", MessageBoxButton.YesNo, MessageBoxImage.Information) == MessageBoxResult.Yes)
                    Process.Start(new ProcessStartInfo("https://github.com/PouryaRajaei/WinVClipboard/releases/latest") { UseShellExecute = true });
            }
            else MessageBox.Show(Localizer.T("UpToDate"), "WinVClipboard");
        }
        catch (Exception ex) { MessageBox.Show(ex.Message, "WinVClipboard", MessageBoxButton.OK, MessageBoxImage.Warning); }
        finally { ((Button)sender).IsEnabled = true; }
    }

    private static bool IsStartupEnabled() { try { using var key = Registry.CurrentUser.OpenSubKey(RunKey); return key?.GetValue(RunValue) != null; } catch { return false; } }
    private static void SetStartup(bool enabled)
    {
        try { using var key = Registry.CurrentUser.CreateSubKey(RunKey); if (enabled) key.SetValue(RunValue, $"\"{Environment.ProcessPath}\" --startup"); else key.DeleteValue(RunValue, false); } catch { }
    }
    private static StackPanel Panel() => new() { Margin = new Thickness(18) };
    private static TabItem Tab(string title, UIElement content) => new() { Header = title, Content = new ScrollViewer { Content = content, VerticalScrollBarVisibility = ScrollBarVisibility.Auto } };
    private static TextBlock Label(string text) => new() { Text = text, Margin = new Thickness(0, 10, 0, 5), Foreground = (Brush)Application.Current.Resources["MutedBrush"] };
    private static TextBox Input(string text) => new() { Text = text, Padding = new Thickness(9), MaxWidth = 520, HorizontalAlignment = HorizontalAlignment.Stretch };
    private static CheckBox Check(string text, bool value) => new() { Content = text, IsChecked = value, Margin = new Thickness(0, 8, 0, 8) };
    private static Button Button(string text) => new() { Content = text, Padding = new Thickness(14, 8, 14, 8), Margin = new Thickness(5) };
    private static ComboBox Combo(params (string Label, string Value)[] values) { var box = new ComboBox { Padding = new Thickness(8) }; foreach (var value in values) box.Items.Add(new ComboBoxItem { Content = value.Label, Tag = value.Value }); return box; }
    private static void Select(ComboBox box, string value) { box.SelectedItem = box.Items.Cast<ComboBoxItem>().FirstOrDefault(x => string.Equals((string)x.Tag, value, StringComparison.OrdinalIgnoreCase)) ?? box.Items[0]; }
    private static string Value(ComboBox box) => (string)((ComboBoxItem)box.SelectedItem).Tag;
}
