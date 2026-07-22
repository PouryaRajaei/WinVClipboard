using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using DataGrid = System.Windows.Controls.DataGrid;
using FlowDirection = System.Windows.FlowDirection;
using TextBox = System.Windows.Controls.TextBox;

namespace WinVClipboard;

public sealed class TextShortcut : INotifyPropertyChanged
{
    private string _trigger = "", _description = "";
    public string Trigger { get => _trigger; set { _trigger = value ?? ""; Changed(); } }
    public string Description { get => _description; set { _description = value ?? ""; Changed(); } }
    public event PropertyChangedEventHandler? PropertyChanged;
    private void Changed([CallerMemberName] string? name = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

public sealed class TextShortcutManagerDialog : Window
{
    private readonly ObservableCollection<TextShortcut> _items;
    private readonly TextBox _triggerBox, _descriptionBox;
    private readonly DataGrid _grid;
    public IReadOnlyList<TextShortcut> ResultItems => _items;

    public TextShortcutManagerDialog(IEnumerable<TextShortcut> source)
    {
        _items = new ObservableCollection<TextShortcut>(source.Select(x => new TextShortcut { Trigger = x.Trigger, Description = x.Description }));
        Title = Localizer.T("TextShortcuts"); Width = 620; Height = 480; WindowStartupLocation = WindowStartupLocation.CenterOwner;
        ResizeMode = ResizeMode.CanResizeWithGrip; ShowInTaskbar = false; Background = new SolidColorBrush(Color.FromRgb(30, 30, 30));
        Foreground = Brushes.White; FlowDirection = Localizer.Direction;

        var root = new Grid { Margin = new Thickness(18) };
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition());
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        var title = new TextBlock { Text = Localizer.T("TextShortcuts"), FontSize = 19, FontWeight = FontWeights.SemiBold };
        var hint = new TextBlock { Text = Localizer.T("TextShortcutHint"), Foreground = new SolidColorBrush(Color.FromRgb(170, 170, 170)), Margin = new Thickness(0, 5, 0, 12) };
        var heading = new StackPanel(); heading.Children.Add(title); heading.Children.Add(hint); Grid.SetRow(heading, 0); root.Children.Add(heading);

        var editor = new Grid { Margin = new Thickness(0, 0, 0, 12) };
        editor.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(155) }); editor.ColumnDefinitions.Add(new ColumnDefinition()); editor.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        _triggerBox = new TextBox { Padding = new Thickness(8), Margin = new Thickness(0, 0, 8, 0), FlowDirection = FlowDirection.LeftToRight, ToolTip = Localizer.T("TriggerTip") };
        _descriptionBox = new TextBox { Padding = new Thickness(8), Margin = new Thickness(0, 0, 8, 0), ToolTip = Localizer.T("DescriptionTip") };
        var add = new Button { Content = Localizer.T("Add"), Padding = new Thickness(13, 7, 13, 7), Background = new SolidColorBrush(Color.FromRgb(42, 112, 180)) }; add.Click += Add_Click;
        Grid.SetColumn(_triggerBox, 0); Grid.SetColumn(_descriptionBox, 1); Grid.SetColumn(add, 2); editor.Children.Add(_triggerBox); editor.Children.Add(_descriptionBox); editor.Children.Add(add);
        Grid.SetRow(editor, 1); root.Children.Add(editor);

        _grid = new DataGrid { ItemsSource = _items, AutoGenerateColumns = false, CanUserAddRows = false, HeadersVisibility = DataGridHeadersVisibility.Column, GridLinesVisibility = DataGridGridLinesVisibility.Horizontal, Background = new SolidColorBrush(Color.FromRgb(38, 38, 38)), Foreground = Brushes.White, RowBackground = new SolidColorBrush(Color.FromRgb(42, 42, 42)), AlternatingRowBackground = new SolidColorBrush(Color.FromRgb(47, 47, 47)) };
        _grid.Columns.Add(new DataGridTextColumn { Header = Localizer.T("Shortcut"), Binding = new System.Windows.Data.Binding(nameof(TextShortcut.Trigger)), Width = new DataGridLength(155) });
        _grid.Columns.Add(new DataGridTextColumn { Header = Localizer.T("Description"), Binding = new System.Windows.Data.Binding(nameof(TextShortcut.Description)), Width = new DataGridLength(1, DataGridLengthUnitType.Star) });
        Grid.SetRow(_grid, 2); root.Children.Add(_grid);

        var actions = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Left, Margin = new Thickness(0, 12, 0, 0) };
        var save = new Button { Content = Localizer.T("Save"), Padding = new Thickness(18, 7, 18, 7), Background = new SolidColorBrush(Color.FromRgb(42, 112, 180)), Margin = new Thickness(6, 0, 0, 0) };
        save.Click += (_, _) => { _grid.CommitEdit(); DialogResult = true; Close(); };
        var remove = new Button { Content = Localizer.T("RemoveSelected"), Padding = new Thickness(13, 7, 13, 7), Margin = new Thickness(6, 0, 0, 0) }; remove.Click += (_, _) => { if (_grid.SelectedItem is TextShortcut item) _items.Remove(item); };
        var cancel = new Button { Content = Localizer.T("Cancel"), Padding = new Thickness(13, 7, 13, 7) }; cancel.Click += (_, _) => { DialogResult = false; Close(); };
        actions.Children.Add(save); actions.Children.Add(remove); actions.Children.Add(cancel); Grid.SetRow(actions, 3); root.Children.Add(actions);
        Content = root;
        PreviewKeyDown += (_, e) => { if (e.Key == System.Windows.Input.Key.Escape) { DialogResult = false; Close(); } };
        Loaded += (_, _) => _triggerBox.Focus();
    }

    private void Add_Click(object sender, RoutedEventArgs e)
    {
        var trigger = _triggerBox.Text.Trim(); var description = _descriptionBox.Text;
        if (trigger.Length < 2 || description.Length == 0) { _triggerBox.Focus(); return; }
        var existing = _items.FirstOrDefault(x => x.Trigger.Equals(trigger, StringComparison.OrdinalIgnoreCase));
        if (existing != null) existing.Description = description;
        else _items.Add(new TextShortcut { Trigger = trigger, Description = description });
        _triggerBox.Clear(); _descriptionBox.Clear(); _triggerBox.Focus();
    }
}

public sealed class TextSuggestionWindow : Window
{
    public event Action? Accepted;
    public TextSuggestionWindow(TextShortcut shortcut)
    {
        Width = 360; SizeToContent = SizeToContent.Height; WindowStyle = WindowStyle.None; AllowsTransparency = true;
        Background = Brushes.Transparent; Topmost = true; ShowInTaskbar = false; ShowActivated = false;
        var border = new Border { Background = new SolidColorBrush(Color.FromArgb(248, 35, 35, 35)), BorderBrush = new SolidColorBrush(Color.FromRgb(65, 140, 215)), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(10), Padding = new Thickness(12) };
        var root = new Grid(); root.ColumnDefinitions.Add(new ColumnDefinition()); root.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        var text = new StackPanel();
        text.Children.Add(new TextBlock { Text = shortcut.Trigger, Foreground = new SolidColorBrush(Color.FromRgb(105, 180, 255)), FontSize = 12, FlowDirection = FlowDirection.LeftToRight, HorizontalAlignment = HorizontalAlignment.Left });
        text.Children.Add(new TextBlock { Text = shortcut.Description, Foreground = Brushes.White, FontSize = 13, TextWrapping = TextWrapping.Wrap, MaxHeight = 56, Margin = new Thickness(0, 4, 0, 0), FlowDirection = DetectDirection(shortcut.Description) });
        var actions = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(10, 0, 0, 0) };
        var accept = new Button { Content = Localizer.T("ConvertTab"), Padding = new Thickness(10, 5, 10, 5), Background = new SolidColorBrush(Color.FromRgb(42, 112, 180)) }; accept.Click += (_, _) => Accepted?.Invoke();
        actions.Children.Add(accept); actions.Children.Add(new TextBlock { Text = Localizer.T("CancelEsc"), Foreground = new SolidColorBrush(Color.FromRgb(150, 150, 150)), FontSize = 10, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 4, 0, 0) });
        Grid.SetColumn(text, 0); Grid.SetColumn(actions, 1); root.Children.Add(text); root.Children.Add(actions); border.Child = root; Content = border;
        var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(8) }; timer.Tick += (_, _) => { timer.Stop(); Close(); }; Closed += (_, _) => timer.Stop(); timer.Start();
    }
    private static FlowDirection DetectDirection(string text) => text.Any(c => c >= '\u0600' && c <= '\u08FF') ? FlowDirection.RightToLeft : FlowDirection.LeftToRight;
}
