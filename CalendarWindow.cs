using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
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
    private static readonly Dictionary<(int, int), string> Holidays1405 = new()
    {
        [(1, 1)] = "عید فطر و نوروز", [(1, 2)] = "عید فطر و نوروز",
        [(1, 3)] = "عید نوروز", [(1, 4)] = "عید نوروز",
        [(1, 12)] = "روز جمهوری اسلامی", [(1, 13)] = "روز طبیعت",
        [(1, 25)] = "شهادت امام جعفر صادق (ع)", [(3, 6)] = "عید قربان",
        [(3, 14)] = "عید غدیر و رحلت امام خمینی", [(3, 15)] = "قیام ۱۵ خرداد",
        [(4, 3)] = "تاسوعای حسینی", [(4, 4)] = "عاشورای حسینی",
        [(5, 13)] = "اربعین حسینی", [(5, 21)] = "رحلت پیامبر (ص) و شهادت امام حسن (ع)",
        [(5, 22)] = "شهادت امام رضا (ع)", [(5, 30)] = "شهادت امام حسن عسکری (ع)",
        [(6, 8)] = "میلاد پیامبر (ص) و امام صادق (ع)", [(8, 22)] = "شهادت حضرت فاطمه (س)",
        [(10, 2)] = "میلاد امام علی (ع)", [(10, 16)] = "مبعث پیامبر (ص)",
        [(11, 4)] = "نیمه شعبان", [(11, 22)] = "پیروزی انقلاب اسلامی",
        [(12, 9)] = "شهادت امام علی (ع)", [(12, 19)] = "عید فطر",
        [(12, 20)] = "تعطیل عید فطر", [(12, 29)] = "ملی شدن صنعت نفت"
    };
    private readonly Grid _days = new();
    private readonly Button _monthButton = new(), _yearButton = new();
    private readonly TextBlock _subtitle = new();
    private readonly TextBlock _reminderTitle = new();
    private readonly TextBlock _eventTitle = new();
    private readonly ListBox _reminders = new();
    private readonly ListBox _events = new();
    private int _year, _month;
    private DateTime? _selectedDate;
    private bool _showMonthReminders = true;

    public CalendarWindow()
    {
        var today = DateTime.Today;
        _year = Persian.GetYear(today); _month = Persian.GetMonth(today);
        Title = "تقویم و یادآورها"; Width = 700; Height = 760; MinWidth = 620; MinHeight = 650;
        WindowStyle = WindowStyle.None;
        ResizeMode = ResizeMode.NoResize;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Background = (Brush)Application.Current.Resources["PanelBrush"];
        Foreground = (Brush)Application.Current.Resources["PrimaryTextBrush"];
        FlowDirection = FlowDirection.RightToLeft;
        Content = BuildLayout();
        RenderMonth();
    }

    private UIElement BuildLayout()
    {
        var shell = new Border
        {
            Background = (Brush)Application.Current.Resources["PanelBrush"],
            BorderBrush = new SolidColorBrush(Color.FromArgb(80, 65, 65, 65)),
            BorderThickness = new Thickness(1)
        };
        var root = new Grid { Margin = new Thickness(20, 10, 20, 20) };
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition());
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(175) });

        var windowHeader = new Grid { Margin = new Thickness(0, 0, 0, 8), Background = Brushes.Transparent };
        windowHeader.ColumnDefinitions.Add(new ColumnDefinition());
        windowHeader.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        windowHeader.Children.Add(new TextBlock { Text = "تقویم و یادآورها", Foreground = (Brush)Application.Current.Resources["MutedBrush"], VerticalAlignment = VerticalAlignment.Center, FontSize = 12 });
        var close = new Button { Content = "✕", FontSize = 12, Padding = new Thickness(10, 5, 10, 5), Foreground = (Brush)Application.Current.Resources["MutedBrush"] };
        close.Click += (_, _) => Close(); Grid.SetColumn(close, 1); windowHeader.Children.Add(close);
        windowHeader.MouseLeftButtonDown += (_, e) => { if (e.ButtonState == MouseButtonState.Pressed) DragMove(); };
        root.Children.Add(windowHeader);

        var header = new Grid { Margin = new Thickness(0, 0, 0, 14) };
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        header.ColumnDefinitions.Add(new ColumnDefinition());
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        var next = NavButton("ماه بعد ←", 1); var previous = NavButton("→ ماه قبل", -1);
        Grid.SetColumn(previous, 0); Grid.SetColumn(next, 2);
        var titles = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center };
        var titleSelectors = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            FlowDirection = FlowDirection.RightToLeft,
            HorizontalAlignment = HorizontalAlignment.Center
        };
        ConfigureTitleSelector(_monthButton);
        ConfigureTitleSelector(_yearButton);
        _monthButton.Click += (_, _) => ShowMonthSelector();
        _yearButton.Click += (_, _) => ShowYearSelector();
        titleSelectors.Children.Add(_monthButton);
        titleSelectors.Children.Add(_yearButton);
        _subtitle.HorizontalAlignment = HorizontalAlignment.Center;
        _subtitle.Foreground = (Brush)Application.Current.Resources["MutedBrush"];
        var todayButton = new Button { Content = "برو به امروز", Foreground = (Brush)Application.Current.Resources["MutedBrush"], FontSize = 11, Padding = new Thickness(8, 3, 8, 3), Margin = new Thickness(0, 3, 0, 0) };
        todayButton.Click += (_, _) => GoToToday();
        titles.Children.Add(titleSelectors); titles.Children.Add(_subtitle); titles.Children.Add(todayButton); Grid.SetColumn(titles, 1);
        header.Children.Add(next); header.Children.Add(titles); header.Children.Add(previous);
        Grid.SetRow(header, 1); root.Children.Add(header);

        var week = new Grid { Background = new SolidColorBrush(Color.FromArgb(22, 255, 255, 255)), Margin = new Thickness(0, 0, 0, 8) };
        for (var i = 0; i < 7; i++)
        {
            week.ColumnDefinitions.Add(new ColumnDefinition());
            var text = new TextBlock { Text = WeekDays[i], Foreground = (Brush)Application.Current.Resources["MutedBrush"], FontWeight = FontWeights.SemiBold, FontSize = 15, Margin = new Thickness(4, 9, 4, 9), HorizontalAlignment = HorizontalAlignment.Center };
            Grid.SetColumn(text, i); week.Children.Add(text);
        }
        Grid.SetRow(week, 2); root.Children.Add(week);
        for (var i = 0; i < 7; i++) _days.ColumnDefinitions.Add(new ColumnDefinition());
        for (var i = 0; i < 6; i++) _days.RowDefinitions.Add(new RowDefinition());
        Grid.SetRow(_days, 3); root.Children.Add(_days);

        var bottomPanel = new Grid { Margin = new Thickness(0, 14, 0, 0) };
        bottomPanel.ColumnDefinitions.Add(new ColumnDefinition());
        bottomPanel.ColumnDefinitions.Add(new ColumnDefinition());

        var eventPanel = new Grid { Margin = new Thickness(0, 0, 10, 0) };
        eventPanel.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); eventPanel.RowDefinitions.Add(new RowDefinition());
        _eventTitle.FontSize = 16; _eventTitle.FontWeight = FontWeights.SemiBold; _eventTitle.Margin = new Thickness(4, 0, 4, 7);
        _eventTitle.Foreground = (Brush)Application.Current.Resources["PrimaryTextBrush"];
        eventPanel.Children.Add(_eventTitle);
        _events.Background = Brushes.Transparent; _events.BorderThickness = new Thickness(0);
        _events.Foreground = (Brush)Application.Current.Resources["PrimaryTextBrush"];
        _events.ItemContainerStyle = ReminderItemStyle();
        Grid.SetRow(_events, 1); eventPanel.Children.Add(_events);
        Grid.SetColumn(eventPanel, 0); bottomPanel.Children.Add(eventPanel);

        var reminderPanel = new Grid { Margin = new Thickness(10, 0, 0, 0) };
        reminderPanel.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); reminderPanel.RowDefinitions.Add(new RowDefinition());
        _reminderTitle.FontSize = 16; _reminderTitle.FontWeight = FontWeights.SemiBold; _reminderTitle.Margin = new Thickness(4, 0, 4, 7);
        _reminderTitle.Foreground = (Brush)Application.Current.Resources["PrimaryTextBrush"];
        reminderPanel.Children.Add(_reminderTitle);
        _reminders.Background = Brushes.Transparent; _reminders.BorderThickness = new Thickness(0);
        _reminders.Foreground = (Brush)Application.Current.Resources["PrimaryTextBrush"];
        _reminders.ItemContainerStyle = ReminderItemStyle();
        Grid.SetRow(_reminders, 1); reminderPanel.Children.Add(_reminders);
        Grid.SetColumn(reminderPanel, 1); bottomPanel.Children.Add(reminderPanel);
        Grid.SetRow(bottomPanel, 4); root.Children.Add(bottomPanel);
        shell.Child = root;
        return shell;
    }

    private static Style ReminderItemStyle()
    {
        var style = new Style(typeof(ListBoxItem));
        style.Setters.Add(new Setter(ForegroundProperty, Application.Current.Resources["PrimaryTextBrush"]));
        style.Setters.Add(new Setter(BackgroundProperty, Brushes.Transparent));
        style.Setters.Add(new Setter(PaddingProperty, new Thickness(8, 5, 8, 5)));
        return style;
    }

    private static SolidColorBrush Accent() => new(Color.FromRgb(224, 167, 41));
    private static void ConfigureTitleSelector(Button button)
    {
        button.FontSize = 21;
        button.FontWeight = FontWeights.SemiBold;
        button.Foreground = Accent();
        button.Padding = new Thickness(5, 2, 5, 2);
        button.ToolTip = "برای انتخاب کلیک کنید";
    }

    private void ShowMonthSelector()
    {
        var menu = SelectorMenu(_monthButton);
        for (var month = 1; month <= 12; month++)
        {
            var value = month;
            var item = SelectorItem(Months[month - 1], month == _month);
            item.Click += (_, _) => { _month = value; SelectionChangedFromTitle(); };
            menu.Items.Add(item);
        }
        menu.IsOpen = true;
    }

    private void ShowYearSelector()
    {
        var menu = SelectorMenu(_yearButton);
        menu.MaxHeight = 420;
        for (var year = Math.Max(1200, _year - 20); year <= Math.Min(1600, _year + 20); year++)
        {
            var value = year;
            var item = SelectorItem(Digits(year.ToString()), year == _year);
            item.Click += (_, _) => { _year = value; SelectionChangedFromTitle(); };
            menu.Items.Add(item);
        }
        menu.IsOpen = true;
    }

    private static ContextMenu SelectorMenu(FrameworkElement target) => new()
    {
        PlacementTarget = target,
        Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom,
        FlowDirection = FlowDirection.RightToLeft,
        Background = (Brush)Application.Current.Resources["CardBrush"],
        Foreground = (Brush)Application.Current.Resources["PrimaryTextBrush"]
    };

    private static MenuItem SelectorItem(string text, bool selected) => new()
    {
        Header = selected ? $"✓  {text}" : $"    {text}",
        FontWeight = selected ? FontWeights.SemiBold : FontWeights.Normal,
        Foreground = selected ? Accent() : (Brush)Application.Current.Resources["PrimaryTextBrush"],
        Padding = new Thickness(13, 6, 13, 6)
    };

    private void SelectionChangedFromTitle()
    {
        _selectedDate = null;
        _showMonthReminders = true;
        RenderMonth();
    }

    private void GoToToday()
    {
        var today = DateTime.Today;
        _year = Persian.GetYear(today);
        _month = Persian.GetMonth(today);
        _selectedDate = null;
        _showMonthReminders = true;
        RenderMonth();
    }

    private Button NavButton(string text, int delta)
    {
        var button = new Button { Content = text, Foreground = Accent(), Padding = new Thickness(12, 8, 12, 8) };
        button.Click += (_, _) =>
        {
            _month += delta;
            if (_month == 13) { _month = 1; _year++; }
            if (_month == 0) { _month = 12; _year--; }
            _selectedDate = null;
            _showMonthReminders = true;
            RenderMonth();
        };
        return button;
    }

    private void RenderMonth()
    {
        _days.Children.Clear();
        var first = Persian.ToDateTime(_year, _month, 1, 0, 0, 0, 0);
        var last = Persian.ToDateTime(_year, _month, Persian.GetDaysInMonth(_year, _month), 0, 0, 0, 0);
        _monthButton.Content = Months[_month - 1];
        _yearButton.Content = Digits(_year.ToString());
        _subtitle.Text = first.Year == last.Year ? $"{first:MMMM} – {last:MMMM yyyy}" : $"{first:MMMM yyyy} – {last:MMMM yyyy}";
        var offset = ((int)first.DayOfWeek + 1) % 7;
        for (var day = 1; day <= Persian.GetDaysInMonth(_year, _month); day++)
        {
            var date = Persian.ToDateTime(_year, _month, day, 0, 0, 0, 0);
            var holiday = GetHoliday(_year, _month, day, date);
            var button = DayButton(date, day, holiday); var cell = offset + day - 1;
            Grid.SetColumn(button, cell % 7); Grid.SetRow(button, cell / 7); _days.Children.Add(button);
        }
        RefreshReminderList();
    }

    private static string? GetHoliday(int year, int month, int day, DateTime date)
    {
        if (year == 1405 && Holidays1405.TryGetValue((month, day), out var officialName)) return officialName;
        if (Holidays.TryGetValue((month, day), out var fixedName)) return fixedName;
        return date.DayOfWeek == DayOfWeek.Friday ? "تعطیل" : null;
    }

    private Button DayButton(DateTime date, int day, string? holiday)
    {
        var stack = new StackPanel { Margin = new Thickness(1) };
        stack.Children.Add(new TextBlock { Text = Digits(day.ToString()), FontSize = 19, FontWeight = date == DateTime.Today ? FontWeights.Bold : FontWeights.Normal, Foreground = holiday != null ? Brushes.IndianRed : (Brush)Application.Current.Resources["PrimaryTextBrush"], HorizontalAlignment = HorizontalAlignment.Center });
        stack.Children.Add(new TextBlock { Text = date.ToString("dd", CultureInfo.InvariantCulture), FontSize = 10, Foreground = (Brush)Application.Current.Resources["MutedBrush"], HorizontalAlignment = HorizontalAlignment.Center });
        var selected = _selectedDate is { } selectedDate && date.Date == selectedDate.Date;
        var button = new Button
        {
            Content = stack, Tag = date, Margin = new Thickness(3),
            ToolTip = $"{(holiday == null ? "" : holiday + "\n")}تک‌کلیک: نمایش یادآورها\nدابل‌کلیک یا راست‌کلیک: افزودن یادآور",
            Background = selected ? new SolidColorBrush(Color.FromArgb(52, 74, 143, 205))
                : holiday != null ? new SolidColorBrush(Color.FromArgb(24, 220, 80, 80)) : Brushes.Transparent,
            BorderBrush = selected ? new SolidColorBrush(Color.FromRgb(90, 160, 220))
                : date.Date == DateTime.Today ? Accent() : Brushes.Transparent,
            BorderThickness = date.Date == DateTime.Today ? new Thickness(2) : new Thickness(1)
        };
        button.Click += (_, _) => SelectDate(date);
        button.MouseDoubleClick += (_, e) => { e.Handled = true; OpenReminderDialog(date); };
        button.ContextMenu = DateContextMenu(date);
        return button;
    }

    private ContextMenu DateContextMenu(DateTime date)
    {
        var menu = new ContextMenu
        {
            FlowDirection = FlowDirection.RightToLeft,
            Background = (Brush)Application.Current.Resources["CardBrush"],
            Foreground = (Brush)Application.Current.Resources["PrimaryTextBrush"],
            ItemContainerStyle = DarkMenuItemStyle(),
            Padding = new Thickness(4),
            Template = DarkContextMenuTemplate()
        };
        var add = new MenuItem { Header = "افزودن یادآور", Foreground = (Brush)Application.Current.Resources["PrimaryTextBrush"] };
        add.Click += (_, _) => { _selectedDate = date; _showMonthReminders = false; OpenReminderDialog(date); };
        var copyPersian = new MenuItem { Header = "کپی تاریخ شمسی", Foreground = (Brush)Application.Current.Resources["PrimaryTextBrush"] };
        copyPersian.Click += (_, _) => CopyDate(PersianDateText(date));
        var copyHijri = new MenuItem { Header = "کپی تاریخ قمری", Foreground = (Brush)Application.Current.Resources["PrimaryTextBrush"] };
        copyHijri.Click += (_, _) => CopyDate(HijriDateText(date));
        var copyGregorian = new MenuItem { Header = "کپی تاریخ میلادی", Foreground = (Brush)Application.Current.Resources["PrimaryTextBrush"] };
        copyGregorian.Click += (_, _) => CopyDate(date.ToString("yyyy/MM/dd", CultureInfo.InvariantCulture));
        menu.Items.Add(add);
        menu.Items.Add(copyPersian);
        menu.Items.Add(copyHijri);
        menu.Items.Add(copyGregorian);
        menu.Opened += (_, _) => SelectDate(date);
        return menu;
    }

    private static ControlTemplate DarkContextMenuTemplate()
    {
        var border = new FrameworkElementFactory(typeof(Border));
        border.SetValue(Border.BackgroundProperty, new TemplateBindingExtension(BackgroundProperty));
        border.SetValue(Border.BorderBrushProperty, new SolidColorBrush(Color.FromRgb(72, 72, 72)));
        border.SetValue(Border.BorderThicknessProperty, new Thickness(1));
        border.SetValue(Border.CornerRadiusProperty, new CornerRadius(7));
        border.SetValue(Border.PaddingProperty, new TemplateBindingExtension(PaddingProperty));
        var presenter = new FrameworkElementFactory(typeof(ItemsPresenter));
        presenter.SetValue(KeyboardNavigation.DirectionalNavigationProperty, KeyboardNavigationMode.Cycle);
        border.AppendChild(presenter);
        return new ControlTemplate(typeof(ContextMenu)) { VisualTree = border };
    }

    private static Style DarkMenuItemStyle()
    {
        var style = new Style(typeof(MenuItem));
        style.Setters.Add(new Setter(ForegroundProperty, Application.Current.Resources["PrimaryTextBrush"]));
        style.Setters.Add(new Setter(BackgroundProperty, Brushes.Transparent));
        style.Setters.Add(new Setter(PaddingProperty, new Thickness(14, 8, 14, 8)));
        style.Setters.Add(new Setter(BorderThicknessProperty, new Thickness(0)));

        var border = new FrameworkElementFactory(typeof(Border));
        border.Name = "ItemBorder";
        border.SetValue(Border.BackgroundProperty, new TemplateBindingExtension(BackgroundProperty));
        border.SetValue(Border.CornerRadiusProperty, new CornerRadius(5));
        border.SetValue(Border.PaddingProperty, new TemplateBindingExtension(PaddingProperty));
        var content = new FrameworkElementFactory(typeof(ContentPresenter));
        content.SetValue(ContentPresenter.ContentSourceProperty, "Header");
        content.SetValue(HorizontalAlignmentProperty, HorizontalAlignment.Stretch);
        content.SetValue(VerticalAlignmentProperty, VerticalAlignment.Center);
        border.AppendChild(content);

        var template = new ControlTemplate(typeof(MenuItem)) { VisualTree = border };
        var highlighted = new Trigger { Property = MenuItem.IsHighlightedProperty, Value = true };
        highlighted.Setters.Add(new Setter(BackgroundProperty, new SolidColorBrush(Color.FromRgb(45, 112, 170))));
        highlighted.Setters.Add(new Setter(ForegroundProperty, Brushes.White));
        template.Triggers.Add(highlighted);
        style.Setters.Add(new Setter(TemplateProperty, template));
        return style;
    }

    private static string PersianDateText(DateTime date) =>
        Digits($"{Persian.GetYear(date):0000}/{Persian.GetMonth(date):00}/{Persian.GetDayOfMonth(date):00}");

    private static string HijriDateText(DateTime date)
    {
        var hijri = new HijriCalendar();
        return Digits($"{hijri.GetYear(date):0000}/{hijri.GetMonth(date):00}/{hijri.GetDayOfMonth(date):00}");
    }

    private static void CopyDate(string value)
    {
        try { Clipboard.SetText(value); }
        catch { MessageBox.Show("کپی تاریخ در کلیپ‌بورد انجام نشد.", "تقویم", MessageBoxButton.OK, MessageBoxImage.Warning); }
    }

    private void SelectDate(DateTime date)
    {
        _selectedDate = date.Date;
        _showMonthReminders = false;
        foreach (var dayButton in _days.Children.OfType<Button>())
        {
            if (dayButton.Tag is not DateTime buttonDate) continue;
            var isSelected = buttonDate.Date == _selectedDate.Value.Date;
            var isHoliday = GetHoliday(Persian.GetYear(buttonDate), Persian.GetMonth(buttonDate), Persian.GetDayOfMonth(buttonDate), buttonDate) != null;
            dayButton.Background = isSelected ? new SolidColorBrush(Color.FromArgb(52, 74, 143, 205))
                : isHoliday ? new SolidColorBrush(Color.FromArgb(24, 220, 80, 80)) : Brushes.Transparent;
            dayButton.BorderBrush = isSelected ? new SolidColorBrush(Color.FromRgb(90, 160, 220))
                : buttonDate.Date == DateTime.Today ? Accent() : Brushes.Transparent;
            dayButton.BorderThickness = buttonDate.Date == DateTime.Today ? new Thickness(2) : new Thickness(1);
        }
        RefreshReminderList();
    }

    private void OpenReminderDialog(DateTime date)
    {
        var dialog = new ReminderDialog(date) { Owner = this };
        if (dialog.ShowDialog() != true || dialog.Reminder == null) return;
        var items = ReminderStore.Load(); items.Add(dialog.Reminder); ReminderStore.Save(items);
        _selectedDate = date; _showMonthReminders = false; RenderMonth();
    }

    private void RefreshReminderList()
    {
        if (_selectedDate is { } selected)
        {
            LoadRemindersForDate(selected);
            LoadEventsForDate(selected);
            return;
        }
        if (_showMonthReminders)
        {
            LoadRemindersForMonth();
            LoadEventsForMonth();
            return;
        }
        LoadAllReminders();
        LoadEventsForMonth();
    }

    private void LoadEventsForDate(DateTime date)
    {
        _eventTitle.Text = "رویدادهای روز";
        var namedEvent = GetNamedEvent(Persian.GetYear(date), Persian.GetMonth(date), Persian.GetDayOfMonth(date));
        PopulateEvents(namedEvent == null ? [] : [(namedEvent, true)]);
    }

    private void LoadEventsForMonth()
    {
        _eventTitle.Text = $"رویدادهای {Months[_month - 1]}";
        var values = new List<(string Text, bool IsHoliday)>();
        var days = Persian.GetDaysInMonth(_year, _month);
        for (var day = 1; day <= days; day++)
        {
            var namedEvent = GetNamedEvent(_year, _month, day);
            if (namedEvent != null) values.Add(($"{Digits(day.ToString())} {Months[_month - 1]}  •  {namedEvent}", true));
        }
        PopulateEvents(values);
    }

    private static string? GetNamedEvent(int year, int month, int day)
    {
        if (year == 1405 && Holidays1405.TryGetValue((month, day), out var officialName)) return officialName;
        return Holidays.TryGetValue((month, day), out var fixedName) ? fixedName : null;
    }

    private void PopulateEvents(IEnumerable<(string Text, bool IsHoliday)> source)
    {
        _events.ItemsSource = null;
        _events.Items.Clear();
        var values = source.ToList();
        if (values.Count == 0)
        {
            _events.Items.Add(new TextBlock
            {
                Text = "رویدادی ثبت نشده است.",
                Foreground = (Brush)Application.Current.Resources["MutedBrush"],
                Padding = new Thickness(8, 5, 8, 5)
            });
            return;
        }
        foreach (var value in values)
            _events.Items.Add(new Border
            {
                Background = value.IsHoliday ? new SolidColorBrush(Color.FromArgb(35, 220, 70, 70)) : Brushes.Transparent,
                BorderBrush = value.IsHoliday ? new SolidColorBrush(Color.FromArgb(95, 230, 90, 90)) : Brushes.Transparent,
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(5),
                Margin = new Thickness(0, 2, 0, 2),
                Child = new TextBlock
                {
                    Text = value.Text,
                    Foreground = value.IsHoliday ? new SolidColorBrush(Color.FromRgb(245, 110, 110)) : (Brush)Application.Current.Resources["PrimaryTextBrush"],
                    TextWrapping = TextWrapping.Wrap,
                    Padding = new Thickness(7, 5, 7, 5)
                }
            });
    }

    private void LoadAllReminders()
    {
        _reminderTitle.Text = "همهٔ یادآورها";
        PopulateReminderList(ReminderStore.Load().OrderBy(x => x.StartsAt).Select(x => (x, x.StartsAt.Date)), true);
    }

    private void LoadRemindersForMonth()
    {
        _reminderTitle.Text = $"یادآورهای {Months[_month - 1]} {Digits(_year.ToString())}";
        var first = Persian.ToDateTime(_year, _month, 1, 0, 0, 0, 0);
        var last = Persian.ToDateTime(_year, _month, Persian.GetDaysInMonth(_year, _month), 23, 59, 59, 999);
        var reminders = ReminderStore.Load();
        var occurrences = new List<(ReminderItem Reminder, DateTime Date)>();
        for (var date = first.Date; date <= last.Date; date = date.AddDays(1))
            foreach (var reminder in reminders.Where(x => OccursOn(x, date)))
                occurrences.Add((reminder, date));
        PopulateReminderList(occurrences.OrderBy(x => x.Date).ThenBy(x => x.Reminder.StartsAt.TimeOfDay), true);
    }

    private void LoadRemindersForDate(DateTime date)
    {
        var pc = new PersianCalendar();
        _reminderTitle.Text = $"یادآورهای {Digits(pc.GetYear(date).ToString())}/{Digits(pc.GetMonth(date).ToString("00"))}/{Digits(pc.GetDayOfMonth(date).ToString("00"))}";
        var items = ReminderStore.Load().Where(x => OccursOn(x, date)).OrderBy(x => x.StartsAt.TimeOfDay)
            .Select(x => (x, date.Date));
        PopulateReminderList(items, false);
    }

    private void PopulateReminderList(IEnumerable<(ReminderItem Reminder, DateTime Date)> source, bool showDate)
    {
        _reminders.ItemsSource = null;
        _reminders.Items.Clear();
        var items = source.ToList();
        if (items.Count == 0)
        {
            _reminders.Items.Add(new TextBlock
            {
                Text = "یادآوری برای این بازه ثبت نشده است.",
                Foreground = (Brush)Application.Current.Resources["MutedBrush"],
                Padding = new Thickness(8, 5, 8, 5)
            });
            return;
        }
        foreach (var item in items) _reminders.Items.Add(ReminderRow(item.Reminder, item.Date, showDate));
    }

    private static bool OccursInRange(ReminderItem item, DateTime first, DateTime last)
    {
        if (item.StartsAt > last) return false;
        if (item.Repeat == ReminderRepeat.None) return item.StartsAt >= first && item.StartsAt <= last;
        if (item.Repeat == ReminderRepeat.Daily) return true;
        for (var date = first.Date; date <= last.Date; date = date.AddDays(1))
            if (OccursOn(item, date)) return true;
        return false;
    }

    private UIElement ReminderRow(ReminderItem reminder, DateTime occurrenceDate, bool showDate)
    {
        var row = new Grid { Margin = new Thickness(0, 2, 0, 2) };
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        row.ColumnDefinitions.Add(new ColumnDefinition());
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        var occurrenceKey = occurrenceDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        reminder.CompletedOccurrences ??= [];
        var completed = reminder.CompletedOccurrences.Contains(occurrenceKey);
        var check = new CheckBox { IsChecked = completed, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(5, 0, 5, 0), ToolTip = "انجام شده" };
        check.Checked += (_, _) => SetCompleted(reminder, occurrenceKey, true);
        check.Unchecked += (_, _) => SetCompleted(reminder, occurrenceKey, false);
        row.Children.Add(check);
        var datePrefix = showDate
            ? $"{Digits(Persian.GetMonth(occurrenceDate).ToString("00"))}/{Digits(Persian.GetDayOfMonth(occurrenceDate).ToString("00"))}  •  "
            : "";
        var text = new TextBlock
        {
            Text = $"{datePrefix}{reminder.StartsAt:HH:mm}  •  {reminder.Title}  ({RepeatText(reminder.Repeat)}{RepeatLimitText(reminder)})",
            Foreground = (Brush)Application.Current.Resources["PrimaryTextBrush"],
            VerticalAlignment = VerticalAlignment.Center,
            TextTrimming = TextTrimming.CharacterEllipsis,
            Opacity = completed ? 0.45 : 1,
            TextDecorations = completed ? TextDecorations.Strikethrough : null
        };
        Grid.SetColumn(text, 1); row.Children.Add(text);
        var actions = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            FlowDirection = FlowDirection.LeftToRight,
            HorizontalAlignment = HorizontalAlignment.Left
        };
        var edit = new Button
        {
            Content = "\uE70F", FontFamily = new FontFamily("Segoe MDL2 Assets"), FontSize = 15,
            Foreground = new SolidColorBrush(Color.FromRgb(115, 185, 235)),
            Padding = new Thickness(7, 5, 7, 5), ToolTip = "ویرایش یادآور"
        };
        edit.Click += (_, _) => EditReminder(reminder);
        var delete = new Button
        {
            Content = "\uE74D", FontFamily = new FontFamily("Segoe MDL2 Assets"), FontSize = 15,
            Foreground = Brushes.IndianRed,
            Padding = new Thickness(7, 5, 7, 5), ToolTip = "حذف یادآور"
        };
        delete.Click += (_, _) => DeleteReminder(reminder);
        actions.Children.Add(edit); actions.Children.Add(delete);
        Grid.SetColumn(actions, 2); row.Children.Add(actions);
        return row;
    }

    private void SetCompleted(ReminderItem reminder, string occurrenceKey, bool completed)
    {
        var items = ReminderStore.Load();
        var stored = items.FirstOrDefault(x => x.Id == reminder.Id);
        if (stored == null) return;
        stored.CompletedOccurrences ??= [];
        if (completed)
        {
            if (!stored.CompletedOccurrences.Contains(occurrenceKey)) stored.CompletedOccurrences.Add(occurrenceKey);
        }
        else stored.CompletedOccurrences.Remove(occurrenceKey);
        ReminderStore.Save(items);
        RefreshReminderList();
    }

    private void EditReminder(ReminderItem reminder)
    {
        var dialog = new ReminderDialog(reminder.StartsAt.Date, reminder) { Owner = this };
        if (dialog.ShowDialog() != true || dialog.Reminder == null) return;
        var items = ReminderStore.Load();
        var index = items.FindIndex(x => x.Id == reminder.Id);
        if (index < 0) return;
        items[index] = dialog.Reminder;
        ReminderStore.Save(items);
        RefreshReminderList();
    }

    private void DeleteReminder(ReminderItem reminder)
    {
        var answer = MessageBox.Show($"یادآور «{reminder.Title}» حذف شود؟", "حذف یادآور",
            MessageBoxButton.YesNo, MessageBoxImage.Question);
        if (answer != MessageBoxResult.Yes) return;
        var items = ReminderStore.Load();
        items.RemoveAll(x => x.Id == reminder.Id);
        ReminderStore.Save(items);
        RefreshReminderList();
    }

    private static bool OccursOn(ReminderItem item, DateTime date)
    {
        if (date.Date < item.StartsAt.Date) return false;
        if (item.Repeat == ReminderRepeat.None) return date.Date == item.StartsAt.Date;
        if (!ReminderService.IsOccurrenceAllowed(item, date)) return false;
        if (item.Repeat == ReminderRepeat.Daily) return true;
        if (item.Repeat == ReminderRepeat.Weekly)
            return (date.Date - item.StartsAt.Date).Days % 7 == 0;
        if (item.Calendar == ReminderCalendar.Gregorian)
            return item.Repeat == ReminderRepeat.Monthly ? date.Day == Math.Min(item.StartsAt.Day, DateTime.DaysInMonth(date.Year, date.Month))
                : date.Month == item.StartsAt.Month && date.Day == Math.Min(item.StartsAt.Day, DateTime.DaysInMonth(date.Year, item.StartsAt.Month));
        var sourceMonth = Persian.GetMonth(item.StartsAt);
        var sourceDay = Persian.GetDayOfMonth(item.StartsAt);
        var dateMonth = Persian.GetMonth(date);
        var expectedDay = Math.Min(sourceDay, Persian.GetDaysInMonth(Persian.GetYear(date), dateMonth));
        return Persian.GetDayOfMonth(date) == expectedDay &&
            (item.Repeat == ReminderRepeat.Monthly || dateMonth == sourceMonth);
    }

    private static string RepeatText(ReminderRepeat value) => value switch { ReminderRepeat.Daily => "روزانه", ReminderRepeat.Weekly => "هفتگی", ReminderRepeat.Monthly => "ماهانه", ReminderRepeat.Yearly => "سالانه", _ => "بدون تکرار" };
    private static string RepeatLimitText(ReminderItem item) =>
        item.Repeat != ReminderRepeat.None && item.MaxOccurrences is > 0 ? $"، {Digits(item.MaxOccurrences.Value.ToString())} بار" : "";
    private static string Digits(string value) => value.Replace('0', '۰').Replace('1', '۱').Replace('2', '۲').Replace('3', '۳').Replace('4', '۴').Replace('5', '۵').Replace('6', '۶').Replace('7', '۷').Replace('8', '۸').Replace('9', '۹');
}

public sealed class ReminderDialog : Window
{
    private readonly DateTime _date;
    private readonly ReminderItem? _existing;
    private readonly TextBox _title = new(), _time = new() { Text = "09:00" };
    private readonly TextBox _repeatCount = new();
    private readonly ComboBox _repeat = new(), _calendar = new();
    private static readonly ReminderRepeat[] RepeatOptions =
        [ReminderRepeat.None, ReminderRepeat.Daily, ReminderRepeat.Weekly, ReminderRepeat.Monthly, ReminderRepeat.Yearly];
    public ReminderItem? Reminder { get; private set; }

    public ReminderDialog(DateTime date, ReminderItem? existing = null)
    {
        _existing = existing;
        _date = existing?.StartsAt.Date ?? date;
        Title = existing == null ? "یادآور جدید" : "ویرایش یادآور"; Width = 410; Height = 500; ResizeMode = ResizeMode.NoResize;
        WindowStartupLocation = WindowStartupLocation.CenterOwner; Background = (Brush)Application.Current.Resources["PanelBrush"];
        Foreground = (Brush)Application.Current.Resources["PrimaryTextBrush"]; FlowDirection = FlowDirection.RightToLeft;
        var pc = new PersianCalendar(); var panel = new StackPanel { Margin = new Thickness(24) };
        panel.Children.Add(new TextBlock { Text = $"{pc.GetYear(date)}/{pc.GetMonth(date):00}/{pc.GetDayOfMonth(date):00}  •  {date:yyyy/MM/dd}", FontSize = 16, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 0, 0, 14) });
        AddField(panel, "عنوان", _title); AddField(panel, "زمان (HH:mm)", _time);
        _repeat.ItemsSource = new[] { "بدون تکرار", "روزانه", "هفتگی", "ماهانه", "سالانه" }; _repeat.SelectedIndex = 0; AddField(panel, "تکرار", _repeat);
        _repeatCount.Text = "";
        AddField(panel, "تعداد دفعات (خالی = نامحدود)", _repeatCount);
        _calendar.ItemsSource = new[] { "بر مبنای تقویم شمسی", "بر مبنای تقویم میلادی" }; _calendar.SelectedIndex = 0; AddField(panel, "مبنای تکرار", _calendar);
        if (existing != null)
        {
            _title.Text = existing.Title;
            _time.Text = existing.StartsAt.ToString("HH:mm", CultureInfo.InvariantCulture);
            _repeat.SelectedIndex = Math.Max(0, Array.IndexOf(RepeatOptions, existing.Repeat));
            _repeatCount.Text = existing.MaxOccurrences?.ToString(CultureInfo.InvariantCulture) ?? "";
            _calendar.SelectedIndex = (int)existing.Calendar;
        }
        var save = new Button { Content = existing == null ? "ذخیره یادآور" : "ذخیره تغییرات", Background = new SolidColorBrush(Color.FromRgb(224, 167, 41)), Foreground = Brushes.White, Padding = new Thickness(18, 10, 18, 10), Margin = new Thickness(0, 12, 0, 0) };
        save.Click += Save; panel.Children.Add(save); Content = panel;
    }

    private static void AddField(Panel panel, string label, Control control)
    {
        panel.Children.Add(new TextBlock { Text = label, Margin = new Thickness(0, 5, 0, 4), Foreground = (Brush)Application.Current.Resources["MutedBrush"] });
        control.Padding = new Thickness(8);
        if (control is ComboBox combo)
        {
            combo.Background = new SolidColorBrush(Color.FromRgb(242, 242, 242));
            combo.Foreground = new SolidColorBrush(Color.FromRgb(25, 25, 25));
            var itemStyle = new Style(typeof(ComboBoxItem));
            itemStyle.Setters.Add(new Setter(ForegroundProperty, new SolidColorBrush(Color.FromRgb(25, 25, 25))));
            itemStyle.Setters.Add(new Setter(BackgroundProperty, Brushes.White));
            itemStyle.Setters.Add(new Setter(PaddingProperty, new Thickness(9, 7, 9, 7)));
            combo.ItemContainerStyle = itemStyle;
        }
        else
        {
            control.Background = (Brush)Application.Current.Resources["CardBrush"];
            control.Foreground = (Brush)Application.Current.Resources["PrimaryTextBrush"];
        }
        panel.Children.Add(control);
    }

    private void Save(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(_title.Text) || !TimeSpan.TryParseExact(_time.Text.Trim(), @"hh\:mm", CultureInfo.InvariantCulture, out var time) ||
            (!string.IsNullOrWhiteSpace(_repeatCount.Text) && (!int.TryParse(_repeatCount.Text.Trim(), out var parsedCount) || parsedCount < 1)))
        { MessageBox.Show("عنوان، زمان و تعداد دفعات را معتبر وارد کنید.", "یادآور", MessageBoxButton.OK, MessageBoxImage.Information); return; }
        int? maxOccurrences = string.IsNullOrWhiteSpace(_repeatCount.Text) ? null : int.Parse(_repeatCount.Text.Trim(), CultureInfo.InvariantCulture);
        Reminder = new ReminderItem
        {
            Id = _existing?.Id ?? Guid.NewGuid(),
            Title = _title.Text.Trim(),
            StartsAt = _date.Add(time),
            Repeat = RepeatOptions[_repeat.SelectedIndex],
            Calendar = (ReminderCalendar)_calendar.SelectedIndex,
            MaxOccurrences = maxOccurrences,
            LastNotifiedOccurrence = _existing?.LastNotifiedOccurrence,
            CompletedOccurrences = _existing?.CompletedOccurrences ?? []
        };
        DialogResult = true;
    }
}
