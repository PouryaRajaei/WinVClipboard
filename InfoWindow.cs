using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace WinVClipboard;

public sealed class InfoWindow : Window
{
    public InfoWindow()
    {
        Title = "اطلاعات سازنده";
        Width = 430;
        Height = 390;
        ResizeMode = ResizeMode.NoResize;
        WindowStyle = WindowStyle.None;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Background = Brushes.Transparent;
        AllowsTransparency = true;
        FlowDirection = FlowDirection.RightToLeft;
        Content = BuildContent();
    }

    private UIElement BuildContent()
    {
        var border = new Border
        {
            Background = (Brush)Application.Current.Resources["PanelBrush"],
            BorderBrush = new SolidColorBrush(Color.FromArgb(65, 255, 255, 255)),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(12)
        };
        var root = new Grid { Margin = new Thickness(22, 12, 22, 24) };
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition());

        var header = new Grid { Margin = new Thickness(0, 0, 0, 14) };
        header.ColumnDefinitions.Add(new ColumnDefinition());
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        header.Children.Add(new TextBlock
        {
            Text = "اطلاعات سازنده",
            Foreground = (Brush)Application.Current.Resources["PrimaryTextBrush"],
            FontSize = 14,
            FontWeight = FontWeights.SemiBold,
            VerticalAlignment = VerticalAlignment.Center
        });
        var close = new Button
        {
            Content = "✕",
            Foreground = (Brush)Application.Current.Resources["MutedBrush"],
            Padding = new Thickness(10, 5, 10, 5)
        };
        close.Click += (_, _) => Close();
        Grid.SetColumn(close, 1);
        header.Children.Add(close);
        header.MouseLeftButtonDown += (_, e) => { if (e.ButtonState == MouseButtonState.Pressed) DragMove(); };
        root.Children.Add(header);

        var content = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        content.Children.Add(new TextBlock
        {
            Text = "WinVClipboard",
            FontSize = 25,
            FontWeight = FontWeights.Bold,
            Foreground = new SolidColorBrush(Color.FromRgb(95, 174, 255)),
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(0, 0, 0, 7)
        });
        content.Children.Add(new TextBlock
        {
            Text = "ساخته‌شده توسط",
            Foreground = (Brush)Application.Current.Resources["MutedBrush"],
            HorizontalAlignment = HorizontalAlignment.Center
        });
        content.Children.Add(new TextBlock
        {
            Text = "Pourya Rajaei",
            FontSize = 20,
            FontWeight = FontWeights.SemiBold,
            Foreground = (Brush)Application.Current.Resources["PrimaryTextBrush"],
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(0, 5, 0, 20)
        });
        content.Children.Add(ContactRow("ایمیل", "Pourya.Rajaei@gmail.com", "mailto:Pourya.Rajaei@gmail.com"));
        content.Children.Add(ContactRow("تلفن", "0989309483323", "tel:0989309483323"));
        content.Children.Add(ContactRow("تلگرام", "t.me/PouryaRajaei", "https://t.me/PouryaRajaei"));
        Grid.SetRow(content, 1);
        root.Children.Add(content);
        border.Child = root;
        return border;
    }

    private static UIElement ContactRow(string label, string value, string target)
    {
        var row = new Grid { Margin = new Thickness(18, 5, 18, 5) };
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(75) });
        row.ColumnDefinitions.Add(new ColumnDefinition());
        row.Children.Add(new TextBlock
        {
            Text = label,
            Foreground = (Brush)Application.Current.Resources["MutedBrush"],
            VerticalAlignment = VerticalAlignment.Center
        });
        var link = new Button
        {
            Content = value,
            Tag = target,
            HorizontalContentAlignment = HorizontalAlignment.Left,
            FlowDirection = FlowDirection.LeftToRight,
            Foreground = new SolidColorBrush(Color.FromRgb(95, 174, 255)),
            Padding = new Thickness(8, 6, 8, 6),
            ToolTip = "باز کردن"
        };
        link.Click += (_, _) =>
        {
            try { Process.Start(new ProcessStartInfo(target) { UseShellExecute = true }); }
            catch { Clipboard.SetText(value); }
        };
        Grid.SetColumn(link, 1);
        row.Children.Add(link);
        return row;
    }
}
