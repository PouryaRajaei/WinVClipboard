using Microsoft.Win32;
using System.Diagnostics;
using System.Net.Http;
using System.Reflection;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Input;
using System.Collections.ObjectModel;
using Application = System.Windows.Application;
using Button = System.Windows.Controls.Button;
using CheckBox = System.Windows.Controls.CheckBox;
using ComboBox = System.Windows.Controls.ComboBox;
using DataGrid = System.Windows.Controls.DataGrid;
using TextBox = System.Windows.Controls.TextBox;

namespace WinVClipboard;

public sealed class SettingsWindow : Window
{
    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string RunValue = "WinVClipboard";
    private readonly MainWindow _main;
    private readonly CheckBox _startup, _pause, _images;
    private readonly TextBox _maxHistory, _days, _excluded, _hotkey;
    private readonly ComboBox _language, _size, _theme, _thumbnail, _pinnedModifier;
    private readonly ObservableCollection<TextShortcut> _shortcutItems;
    private readonly TextBox _shortcutTrigger, _shortcutDescription;
    private readonly DataGrid _shortcutGrid;
    private string _capturedHotkey;

    public SettingsWindow(MainWindow main)
    {
        _main = main;
        _shortcutItems = new ObservableCollection<TextShortcut>(_main.GetTextShortcuts());
        _capturedHotkey = Localizer.ShowHotkey;
        Title = Localizer.T("Settings"); Width = 720; Height = 620; MinWidth = 650; MinHeight = 540; WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Owner = main; ShowInTaskbar = false; Background = (Brush)Application.Current.Resources["PanelBrush"];
        Foreground = (Brush)Application.Current.Resources["PrimaryTextBrush"]; FlowDirection = Localizer.Direction;

        var root = new DockPanel { Margin = new Thickness(22) };
        var heading = new StackPanel { Margin = new Thickness(4, 0, 4, 18) };
        heading.Children.Add(new TextBlock { Text = "⚙  " + Localizer.T("Settings"), FontSize = 25, FontWeight = FontWeights.SemiBold, Foreground = Primary() });
        heading.Children.Add(new TextBlock { Text = "WinVClipboard", FontSize = 12, Foreground = Muted(), Margin = new Thickness(2, 4, 0, 0) });
        DockPanel.SetDock(heading, Dock.Top); root.Children.Add(heading);
        var actions = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 14, 0, 0) };
        var save = Button(Localizer.T("Save")); save.Background = new SolidColorBrush(Color.FromRgb(42, 112, 180)); save.Click += Save_Click;
        var cancel = Button(Localizer.T("Cancel")); cancel.Click += (_, _) => Close(); actions.Children.Add(cancel); actions.Children.Add(save);
        DockPanel.SetDock(actions, Dock.Bottom); root.Children.Add(actions);

        var tabs = new TabControl { Background = Brushes.Transparent, BorderThickness = new Thickness(0), Foreground = Primary(), ItemContainerStyle = (Style)Application.Current.Resources["SettingsTabStyle"] };
        var general = Panel();
        general.Children.Add(Label(Localizer.T("Language"))); _language = Combo(("فارسی", "fa"), ("English", "en")); Select(_language, Localizer.IsPersian ? "fa" : "en"); general.Children.Add(_language);
        _startup = Check(Localizer.T("StartWithWindows"), IsStartupEnabled());
        _pause = Check(Localizer.T("PauseCapture"), Localizer.PauseCapture);
        general.Children.Add(_startup); general.Children.Add(_pause);
        general.Children.Add(Label(Localizer.T("PanelSize"))); _size = Combo(("Small", PanelSize.Small.ToString()), ("Medium", PanelSize.Medium.ToString()), ("Large", PanelSize.Large.ToString())); Select(_size, Localizer.CurrentPanelSize.ToString()); general.Children.Add(_size);
        general.Children.Add(Label(Localizer.T("MaxHistory"))); _maxHistory = Input(Localizer.MaxHistory.ToString()); general.Children.Add(_maxHistory);
        general.Children.Add(Label(Localizer.T("ShowHotkey")));
        _hotkey = Input(Localizer.ShowHotkey.Replace("+", " + ")); _hotkey.IsReadOnly = true; _hotkey.FontSize = 17; _hotkey.FontWeight = FontWeights.SemiBold; _hotkey.Cursor = Cursors.Hand;
        _hotkey.ToolTip = Localizer.T("HotkeyHint");
        _hotkey.GotKeyboardFocus += (_, _) => { _hotkey.Text = Localizer.T("HotkeyRecording"); _main.BeginHotkeyRecording(HotkeyRecorded); };
        _hotkey.LostKeyboardFocus += (_, _) => _main.EndHotkeyRecording();
        general.Children.Add(_hotkey); general.Children.Add(new TextBlock { Text = Localizer.T("HotkeyHint"), Foreground = Muted(), FontSize = 11, Margin = new Thickness(2, 5, 0, 8) });
        general.Children.Add(Label(Localizer.T("PinnedModifier"))); _pinnedModifier = Combo(("Ctrl + 1…9", "Ctrl"), ("Alt + 1…9", "Alt")); Select(_pinnedModifier, Localizer.PinnedModifier); general.Children.Add(_pinnedModifier);
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

        var shortcutsPanel = Panel();
        shortcutsPanel.Children.Add(new TextBlock { Text = Localizer.T("TextShortcutHint"), Foreground = Muted(), TextWrapping = TextWrapping.Wrap, Margin = new Thickness(2, 0, 0, 14) });
        var shortcutEditor = new Grid { Margin = new Thickness(0, 0, 0, 12) };
        shortcutEditor.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(155) }); shortcutEditor.ColumnDefinitions.Add(new ColumnDefinition()); shortcutEditor.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        _shortcutTrigger = Input(""); _shortcutTrigger.ToolTip = Localizer.T("TriggerTip"); _shortcutTrigger.FlowDirection = FlowDirection.LeftToRight;
        _shortcutDescription = Input(""); _shortcutDescription.ToolTip = Localizer.T("DescriptionTip"); _shortcutDescription.Margin = new Thickness(8, 0, 8, 0);
        var addShortcut = Button(Localizer.T("Add")); addShortcut.Click += AddShortcut_Click;
        Grid.SetColumn(_shortcutTrigger, 0); Grid.SetColumn(_shortcutDescription, 1); Grid.SetColumn(addShortcut, 2); shortcutEditor.Children.Add(_shortcutTrigger); shortcutEditor.Children.Add(_shortcutDescription); shortcutEditor.Children.Add(addShortcut); shortcutsPanel.Children.Add(shortcutEditor);
        _shortcutGrid = new DataGrid { ItemsSource = _shortcutItems, AutoGenerateColumns = false, CanUserAddRows = false, Height = 235, Background = Card(), Foreground = Primary(), RowBackground = Card(), AlternatingRowBackground = (Brush)Application.Current.Resources["HoverBrush"], GridLinesVisibility = DataGridGridLinesVisibility.Horizontal, BorderBrush = new SolidColorBrush(Color.FromArgb(80, 95, 174, 255)) };
        _shortcutGrid.Columns.Add(new DataGridTextColumn { Header = Localizer.T("Shortcut"), Binding = new System.Windows.Data.Binding(nameof(TextShortcut.Trigger)), Width = new DataGridLength(155) });
        _shortcutGrid.Columns.Add(new DataGridTextColumn { Header = Localizer.T("Description"), Binding = new System.Windows.Data.Binding(nameof(TextShortcut.Description)), Width = new DataGridLength(1, DataGridLengthUnitType.Star) });
        shortcutsPanel.Children.Add(_shortcutGrid);
        var removeShortcut = Button(Localizer.T("RemoveSelected")); removeShortcut.HorizontalAlignment = HorizontalAlignment.Left; removeShortcut.Click += (_, _) => { if (_shortcutGrid.SelectedItem is TextShortcut item) _shortcutItems.Remove(item); }; shortcutsPanel.Children.Add(removeShortcut);
        tabs.Items.Add(Tab(Localizer.T("TextShortcuts"), shortcutsPanel));

        var backup = Panel();
        backup.Children.Add(new TextBlock { Text = "☁", FontSize = 52, Foreground = new SolidColorBrush(Color.FromRgb(85, 175, 255)), HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 15, 0, 8) });
        backup.Children.Add(new TextBlock { Text = Localizer.T("BackupDescription"), Foreground = Primary(), TextWrapping = TextWrapping.Wrap, TextAlignment = TextAlignment.Center, FontSize = 15, MaxWidth = 460, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 0, 0, 20) });
        var export = Button(Localizer.T("ExportBackup")); export.Click += (_, _) => _main.ExportBackup();
        var import = Button(Localizer.T("ImportBackup")); import.Click += (_, _) => _main.ImportBackup();
        var backupActions = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center }; backupActions.Children.Add(export); backupActions.Children.Add(import); backup.Children.Add(backupActions);
        tabs.Items.Add(Tab(Localizer.T("Backup"), backup));

        var about = Panel();
        var version = Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "";
        about.Children.Add(new TextBlock { Text = $"WinVClipboard  {version}", FontSize = 24, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 0, 0, 14) });
        about.Children.Add(new TextBlock { Text =
            "Pourya Rajaei\n" +
            "https://t.me/PouryaRajaei\n" +
            "Tel: +989309483323\n" +
            "Pourya.Rajaei@gmail.com\n" +
            "https://github.com/PouryaRajaei/WinVClipboard", TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 18) });
        var update = Button(Localizer.T("CheckUpdates")); update.Click += CheckUpdates_Click; about.Children.Add(update);
        tabs.Items.Add(Tab(Localizer.T("About"), about));
        root.Children.Add(tabs); Content = root;
        Closed += (_, _) => _main.EndHotkeyRecording();
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
        Localizer.ShowHotkey = _capturedHotkey; Localizer.PinnedModifier = Value(_pinnedModifier);
        if (Enum.TryParse<PanelSize>(Value(_size), out var panelSize)) Localizer.SetPanelSize(panelSize);
        _shortcutGrid.CommitEdit(DataGridEditingUnit.Cell, true); _shortcutGrid.CommitEdit(DataGridEditingUnit.Row, true);
        _main.SetTextShortcuts(_shortcutItems);
        Localizer.Save(); SetStartup(_startup.IsChecked == true); _main.ApplySettings(); Close();
    }

    private void AddShortcut_Click(object sender, RoutedEventArgs e)
    {
        var trigger = _shortcutTrigger.Text.Trim(); var description = _shortcutDescription.Text;
        if (trigger.Length < 2 || description.Length == 0) { _shortcutTrigger.Focus(); return; }
        var existing = _shortcutItems.FirstOrDefault(x => x.Trigger.Equals(trigger, StringComparison.OrdinalIgnoreCase));
        if (existing != null) existing.Description = description; else _shortcutItems.Add(new TextShortcut { Trigger = trigger, Description = description });
        _shortcutTrigger.Clear(); _shortcutDescription.Clear(); _shortcutTrigger.Focus();
    }

    private void HotkeyRecorded(Key key, ModifierKeys modifiers)
    {
        if (key == Key.Escape) { _hotkey.Text = _capturedHotkey.Replace("+", " + "); Keyboard.ClearFocus(); return; }
        if (key == Key.Back) { _capturedHotkey = "Win+V"; _hotkey.Text = "Win + V"; Keyboard.ClearFocus(); return; }
        var parts = new List<string>();
        if ((modifiers & ModifierKeys.Control) != 0) parts.Add("Ctrl");
        if ((modifiers & ModifierKeys.Alt) != 0) parts.Add("Alt");
        if ((modifiers & ModifierKeys.Shift) != 0) parts.Add("Shift");
        if ((modifiers & ModifierKeys.Windows) != 0) parts.Add("Win");
        if (parts.Count == 0) { _hotkey.Text = Localizer.T("HotkeyHint"); return; }
        parts.Add(key.ToString()); _capturedHotkey = string.Join("+", parts); _hotkey.Text = string.Join(" + ", parts); Keyboard.ClearFocus();
    }

    private async void CheckUpdates_Click(object sender, RoutedEventArgs e)
    {
        ((Button)sender).IsEnabled = false;
        try { await UpdateService.CheckAsync(_main, true); }
        finally { ((Button)sender).IsEnabled = true; }
    }

    private static bool IsStartupEnabled() { try { using var key = Registry.CurrentUser.OpenSubKey(RunKey); return key?.GetValue(RunValue) != null; } catch { return false; } }
    private static void SetStartup(bool enabled)
    {
        try { using var key = Registry.CurrentUser.CreateSubKey(RunKey); if (enabled) key.SetValue(RunValue, $"\"{Environment.ProcessPath}\" --startup"); else key.DeleteValue(RunValue, false); } catch { }
    }
    private static StackPanel Panel() => new() { Margin = new Thickness(20) };
    private static TabItem Tab(string title, UIElement content)
    {
        var card = new Border { Background = Card(), BorderBrush = new SolidColorBrush(Color.FromArgb(45, 100, 170, 235)), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(12), Margin = new Thickness(2, 8, 2, 2), Child = new ScrollViewer { Content = content, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Background = Brushes.Transparent } };
        return new TabItem { Header = title, Content = card, Foreground = Primary(), Background = Brushes.Transparent, Padding = new Thickness(14, 8, 14, 8) };
    }
    private static TextBlock Label(string text) => new() { Text = text, Margin = new Thickness(2, 12, 0, 6), Foreground = Muted(), FontWeight = FontWeights.Medium };
    private static TextBox Input(string text) => new() { Text = text, Padding = new Thickness(11, 9, 11, 9), MaxWidth = 540, HorizontalAlignment = HorizontalAlignment.Stretch, Background = Card(), Foreground = Primary(), CaretBrush = Primary(), BorderBrush = new SolidColorBrush(Color.FromArgb(90, 95, 174, 255)), BorderThickness = new Thickness(1) };
    private static CheckBox Check(string text, bool value) => new() { Content = text, IsChecked = value, Margin = new Thickness(2, 9, 0, 9), Foreground = Primary() };
    private static Button Button(string text) => new() { Content = text, Padding = new Thickness(16, 9, 16, 9), Margin = new Thickness(5), Foreground = Primary(), Background = new SolidColorBrush(Color.FromArgb(60, 75, 145, 215)), BorderBrush = new SolidColorBrush(Color.FromArgb(100, 85, 165, 245)), BorderThickness = new Thickness(1) };
    private static ComboBox Combo(params (string Label, string Value)[] values) { var box = new ComboBox { Style = (Style)Application.Current.Resources["SettingsComboBoxStyle"] }; foreach (var value in values) box.Items.Add(new ComboBoxItem { Content = value.Label, Tag = value.Value }); return box; }
    private static Brush Primary() => (Brush)Application.Current.Resources["PrimaryTextBrush"];
    private static Brush Muted() => (Brush)Application.Current.Resources["MutedBrush"];
    private static Brush Card() => (Brush)Application.Current.Resources["CardBrush"];
    private static void Select(ComboBox box, string value) { box.SelectedItem = box.Items.Cast<ComboBoxItem>().FirstOrDefault(x => string.Equals((string)x.Tag, value, StringComparison.OrdinalIgnoreCase)) ?? box.Items[0]; }
    private static string Value(ComboBox box) => (string)((ComboBoxItem)box.SelectedItem).Tag;
}
