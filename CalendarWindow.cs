using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace WinVClipboard;

public sealed class CalendarWindow : Window
{
    private static readonly PersianCalendar Persian = new();
    private static readonly string[] Months = ["فروردین", "اردیبهشت", "خرداد", "تیر", "مرداد", "شهریور", "مهر", "آبان", "آذر", "دی", "بهمن", "اسفند"];
    private static readonly string[] WeekDays = ["ش", "ی", "د", "س", "چ", "پ", "ج"];
    private static readonly Dictionary<(int, int), string> Holidays = new()
    {
        [(1, 1)] = "نوروز", [(1, 2)] = "نوروز", [(1, 3)] = "نوروز", [(1, 4)] = "نوروز",
        [(1, 12)] = "روز جمهوری اسلامی", [(1, 13)] = "روز طبیعت", [(3, 14)] = "رحلت امام خمینی",
        [(3, 15)] = "قیام ۱۵ خرداد", [(11, 22)] = "پیروزی انقلاب", [(12, 29)] = "ملی شدن صنعت نفت"
    };
    private readonly Grid _days = new();
    private readonly TextBlock _title = new(), _subtitle = new();
    private readonly ListBox _reminders = new();
    private int _year, _month;

    public CalendarWindow()
    {
        _year = Persian.GetYear(DateTime.Today); _month = Persian.GetMonth(DateTime.Today);
        Title = "تقویم و یادآورها"; Width = 700; Height = 760; MinWidth = 620; MinHeight = 650;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Background = (Brush)Application.Current.Resources["PanelBrush"];
        Foreground = (Brush)Application.Current.Resources["PrimaryTextBrush"];
        FlowDirection = FlowDirection.RightToLeft;
        Content = BuildLayout();
        RenderMonth();
    }

    private UIElement BuildLayout()
    {
        var root = new Grid { Margin = new Thickness(20) };
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition());
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(175) });
        var header = new Grid { Margin = new Thickness(0, 0, 0, 14) };
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        header.ColumnDefinitions.Add(new ColumnDefinition());
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        var next = NavButton("ماه بعد ←", 1); var previous = NavButton("→ ماه قبل", -1);
        Grid.SetColumn(next, 0); Grid.SetColumn(previous, 2);
        var titles = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center };
        _title.FontSize = 22; _title.FontWeight = FontWeights.SemiBold; _title.Foreground = Accent();
        _title.HorizontalAlignment = _subtitle.HorizontalAlignment = HorizontalAlignment.Center;
        _subtitle.Foreground = (Brush)Application.Current.Resources["MutedBrush"];
        titles.Children.Add(_title); titles.Children.Add(_subtitle); Grid.SetColumn(titles, 1);
        header.Children.Add(next); header.Children.Add(titles); header.Children.Add(previous); root.Children.Add(header);

        var week = new Grid { Background = Accent(), Margin = new Thickness(0, 0, 0, 8) };
        for (var i = 0; i < 7; i++)
        {
            week.ColumnDefinitions.Add(new ColumnDefinition());
            var text = new TextBlock { Text = WeekDays[i], Foreground = Brushes.White, FontWeight = FontWeights.Bold, FontSize = 16, Margin = new Thickness(4, 9, 4, 9), HorizontalAlignment = HorizontalAlignment.Center };
            Grid.SetColumn(text, i); week.Children.Add(text);
        }
        Grid.SetRow(week, 1); root.Children.Add(week);
        for (var i = 0; i < 7; i++) _days.ColumnDefinitions.Add(new ColumnDefinition());
        for (var i = 0; i < 6; i++) _days.RowDefinitions.Add(new RowDefinition());
        Grid.SetRow(_days, 2); root.Children.Add(_days);

        var reminderPanel = new Grid { Margin = new Thickness(0, 14, 0, 0) };
        reminderPanel.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); reminderPanel.RowDefinitions.Add(new RowDefinition());
        reminderPanel.Children.Add(new TextBlock { Text = "یادآورهای این ماه", FontSize = 16, FontWeight = FontWeights.SemiBold, Margin = new Thickness(4, 0, 4, 7) });
        _reminders.Background = Brushes.Transparent; _reminders.BorderThickness = new Thickness(0);
        Grid.SetRow(_reminders, 1); reminderPanel.Children.Add(_reminders); Grid.SetRow(reminderPanel, 3); root.Children.Add(reminderPanel);
        return root;
    }

    private static SolidColorBrush Accent() => new(Color.FromRgb(224, 167, 41));
    private Button NavButton(string text, int delta)
    {
        var button = new Button { Content = text, Foreground = Accent(), Padding = new Thickness(12, 8, 12, 8) };
        button.Click += (_, _) => { _month += delta; if (_month == 13) { _month = 1; _year++; } if (_month == 0) { _month = 12; _year--; } RenderMonth(); };
        return button;
    }

    private void RenderMonth()
    {
        _days.Children.Clear();
        var first = Persian.ToDateTime(_year, _month, 1, 0, 0, 0, 0);
        var last = Persian.ToDateTime(_year, _month, Persian.GetDaysInMonth(_year, _month), 0, 0, 0, 0);
        _title.Text = $"{Months[_month - 1]} {Digits(_year.ToString())}";
        _subtitle.Text = first.Year == last.Year ? $"{first:MMMM} – {last:MMMM yyyy}" : $"{first:MMMM yyyy} – {last:MMMM yyyy}";
        var offset = ((int)first.DayOfWeek + 1) % 7;
        for (var day = 1; day <= Persian.GetDaysInMonth(_year, _month); day++)
        {
            var date = Persian.ToDateTime(_year, _month, day, 0, 0, 0, 0);
            var holiday = Holidays.TryGetValue((_month, day), out var name) ? name : date.DayOfWeek == DayOfWeek.Friday ? "تعطیل" : null;
            var button = DayButton(date, day, holiday); var cell = offset + day - 1;
            Grid.SetColumn(button, cell % 7); Grid.SetRow(button, cell / 7); _days.Children.Add(button);
        }
        var list = ReminderStore.Load().Where(x => x.StartsAt.Date >= first && x.StartsAt.Date <= last).OrderBy(x => x.StartsAt)
            .Select(x => $"{Digits(Persian.GetDayOfMonth(x.StartsAt).ToString())} {Months[Persian.GetMonth(x.StartsAt) - 1]}  •  {x.StartsAt:HH:mm}  •  {x.Title}  ({RepeatText(x.Repeat)})").ToList();
        _reminders.ItemsSource = list.Count > 0 ? list : ["یادآوری برای این ماه ثبت نشده است."];
    }

    private Button DayButton(DateTime date, int day, string? holiday)
    {
        var stack = new StackPanel { Margin = new Thickness(1) };
        stack.Children.Add(new TextBlock { Text = Digits(day.ToString()), FontSize = 19, FontWeight = date == DateTime.Today ? FontWeights.Bold : FontWeights.Normal, Foreground = holiday != null ? Brushes.IndianRed : (Brush)Application.Current.Resources["PrimaryTextBrush"], HorizontalAlignment = HorizontalAlignment.Center });
        stack.Children.Add(new TextBlock { Text = date.ToString("dd", CultureInfo.InvariantCulture), FontSize = 10, Foreground = (Brush)Application.Current.Resources["MutedBrush"], HorizontalAlignment = HorizontalAlignment.Center });
        if (holiday != null) stack.Children.Add(new TextBlock { Text = holiday, FontSize = 8, Foreground = Brushes.IndianRed, TextTrimming = TextTrimming.CharacterEllipsis, HorizontalAlignment = HorizontalAlignment.Center });
        var button = new Button { Content = stack, Margin = new Thickness(3), ToolTip = holiday ?? "افزودن یادآور", Background = holiday != null ? new SolidColorBrush(Color.FromArgb(28, 244, 67, 54)) : Brushes.Transparent };
        button.Click += (_, _) => { var dialog = new ReminderDialog(date) { Owner = this }; if (dialog.ShowDialog() == true && dialog.Reminder != null) { var items = ReminderStore.Load(); items.Add(dialog.Reminder); ReminderStore.Save(items); RenderMonth(); } };
        return button;
    }

    private static string RepeatText(ReminderRepeat value) => value switch { ReminderRepeat.Daily => "روزانه", ReminderRepeat.Monthly => "ماهانه", ReminderRepeat.Yearly => "سالانه", _ => "بدون تکرار" };
    private static string Digits(string value) => value.Replace('0', '۰').Replace('1', '۱').Replace('2', '۲').Replace('3', '۳').Replace('4', '۴').Replace('5', '۵').Replace('6', '۶').Replace('7', '۷').Replace('8', '۸').Replace('9', '۹');
}

public sealed class ReminderDialog : Window
{
    private readonly DateTime _date;
    private readonly TextBox _title = new(), _time = new() { Text = "09:00" };
    private readonly ComboBox _repeat = new(), _calendar = new();
    public ReminderItem? Reminder { get; private set; }

    public ReminderDialog(DateTime date)
    {
        _date = date; Title = "یادآور جدید"; Width = 410; Height = 420; ResizeMode = ResizeMode.NoResize;
        WindowStartupLocation = WindowStartupLocation.CenterOwner; Background = (Brush)Application.Current.Resources["PanelBrush"];
        Foreground = (Brush)Application.Current.Resources["PrimaryTextBrush"]; FlowDirection = FlowDirection.RightToLeft;
        var pc = new PersianCalendar(); var panel = new StackPanel { Margin = new Thickness(24) };
        panel.Children.Add(new TextBlock { Text = $"{pc.GetYear(date)}/{pc.GetMonth(date):00}/{pc.GetDayOfMonth(date):00}  •  {date:yyyy/MM/dd}", FontSize = 16, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 0, 0, 14) });
        AddField(panel, "عنوان", _title); AddField(panel, "زمان (HH:mm)", _time);
        _repeat.ItemsSource = new[] { "بدون تکرار", "روزانه", "ماهانه", "سالانه" }; _repeat.SelectedIndex = 0; AddField(panel, "تکرار", _repeat);
        _calendar.ItemsSource = new[] { "بر مبنای تقویم شمسی", "بر مبنای تقویم میلادی" }; _calendar.SelectedIndex = 0; AddField(panel, "مبنای تکرار", _calendar);
        var save = new Button { Content = "ذخیره یادآور", Background = new SolidColorBrush(Color.FromRgb(224, 167, 41)), Foreground = Brushes.White, Padding = new Thickness(18, 10, 18, 10), Margin = new Thickness(0, 12, 0, 0) };
        save.Click += Save; panel.Children.Add(save); Content = panel;
    }

    private static void AddField(Panel panel, string label, Control control)
    {
        panel.Children.Add(new TextBlock { Text = label, Margin = new Thickness(0, 5, 0, 4), Foreground = (Brush)Application.Current.Resources["MutedBrush"] });
        control.Padding = new Thickness(8); control.Background = (Brush)Application.Current.Resources["CardBrush"]; control.Foreground = (Brush)Application.Current.Resources["PrimaryTextBrush"]; panel.Children.Add(control);
    }

    private void Save(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(_title.Text) || !TimeSpan.TryParseExact(_time.Text.Trim(), @"hh\:mm", CultureInfo.InvariantCulture, out var time))
        { MessageBox.Show("عنوان و زمان معتبر وارد کنید.", "یادآور", MessageBoxButton.OK, MessageBoxImage.Information); return; }
        Reminder = new ReminderItem { Title = _title.Text.Trim(), StartsAt = _date.Add(time), Repeat = (ReminderRepeat)_repeat.SelectedIndex, Calendar = (ReminderCalendar)_calendar.SelectedIndex };
        DialogResult = true;
    }
}
