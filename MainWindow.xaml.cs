using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace WinVClipboard;

public partial class MainWindow : Window
{
    private const int MaxItems = 2000, WM_CLIPBOARDUPDATE = 0x031D, WH_KEYBOARD_LL = 13;
    private const int WM_KEYDOWN = 0x0100, WM_KEYUP = 0x0101, WM_SYSKEYDOWN = 0x0104, WM_SYSKEYUP = 0x0105;
    private const int VK_V = 0x56, VK_LWIN = 0x5B, VK_RWIN = 0x5C;
    private readonly ObservableCollection<ClipItem> _all = [], _filtered = [];
    private readonly string _storePath;
    private HwndSource? _source;
    private IntPtr _keyboardHook, _pasteTarget;
    private LowLevelKeyboardProc? _keyboardProc;
    private Thread? _hookThread;
    private uint _hookThreadId;
    private CancellationTokenSource? _saveDebounce;
    private int _pendingWinKey;
    private bool _suppressCapture, _blockedV, _winVChord, _reallyExit, _showPinnedOnly;
    private static readonly UIntPtr InjectionMarker = new(0xC0D3);

    public MainWindow()
    {
        InitializeComponent();
        ClipsList.ItemsSource = _filtered;
        var folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "WinVClipboard");
        Directory.CreateDirectory(folder);
        _storePath = Path.Combine(folder, "history.json");
        LoadHistory(); RefreshFilter();
    }

    public void InitializeInBackground(bool showImmediately = false)
    {
        ReleaseCtrlKeys();
        Left = -10000; Top = -10000; Show(); Hide();
        InstallKeyboardHook();
        if (showImmediately) Dispatcher.BeginInvoke(ShowPanel, DispatcherPriority.ApplicationIdle);
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        _source = (HwndSource)PresentationSource.FromVisual(this);
        _source.AddHook(WndProc);
        AddClipboardFormatListener(_source.Handle);
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == WM_CLIPBOARDUPDATE && !_suppressCapture) Dispatcher.BeginInvoke(CaptureClipboard, DispatcherPriority.Background);
        return IntPtr.Zero;
    }

    private void CaptureClipboard()
    {
        try
        {
            ClipItem? clip = null;
            var imageNeedsEncoding = false;
            if (Clipboard.ContainsFileDropList()) clip = ClipItem.FromFiles(Clipboard.GetFileDropList().Cast<string>().ToArray());
            else if (Clipboard.ContainsImage() && Clipboard.GetImage() is { } image) { clip = ClipItem.FromImage(image); imageNeedsEncoding = true; }
            else if (Clipboard.ContainsText() && Clipboard.GetText() is { Length: > 0 } text) clip = ClipItem.FromText(text);
            if (clip == null) return;
            var duplicate = _all.FirstOrDefault(x => x.Fingerprint == clip.Fingerprint);
            if (duplicate != null) { _all.Remove(duplicate); duplicate.CopiedAt = DateTime.Now; _all.Insert(0, duplicate); }
            else _all.Insert(0, clip);
            while (_all.Count > MaxItems && _all.LastOrDefault(x => !x.IsPinned) is { } old) _all.Remove(old);
            // Update the visible list before any image encoding or disk I/O.
            RefreshFilter();
            if (imageNeedsEncoding) _ = EncodeImageAndSaveAsync(clip);
            else SaveHistory();
        }
        catch (COMException) { Dispatcher.BeginInvoke(CaptureClipboard, DispatcherPriority.ApplicationIdle); }
        catch { }
    }

    private async Task EncodeImageAndSaveAsync(ClipItem item)
    {
        var source = item.LiveImage;
        if (source == null) return;
        try
        {
            item.ImageBase64 = await Task.Run(() =>
            {
                var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(source));
                using var stream = new MemoryStream(); encoder.Save(stream); return Convert.ToBase64String(stream.ToArray());
            });
            SaveHistory();
        }
        catch { }
    }

    private void RefreshFilter()
    {
        var query = SearchBox?.Text?.Trim() ?? "";
        var pinned = _all.Where(x => x.IsPinned).OrderByDescending(x => x.CopiedAt).ToList();
        for (var i = 0; i < pinned.Count; i++) pinned[i].ShortcutLabel = i < 9 ? $"Ctrl+{i + 1}" : "";
        foreach (var item in _all.Where(x => !x.IsPinned)) item.ShortcutLabel = "";
        _filtered.Clear();
        foreach (var item in _all.Where(x => (!_showPinnedOnly || x.IsPinned) && (query.Length == 0 || x.SearchText.Contains(query, StringComparison.CurrentCultureIgnoreCase))).OrderBy(x => x.IsPinned).ThenByDescending(x => x.CopiedAt)) _filtered.Add(item);
        if (EmptyState != null) EmptyState.Visibility = _filtered.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        if (CountText != null) CountText.Text = $"{_all.Count:N0} / {MaxItems:N0}";
        if (PinnedOnlyButton != null) { PinnedOnlyButton.Foreground = _showPinnedOnly ? System.Windows.Media.Brushes.DeepSkyBlue : System.Windows.Media.Brushes.White; PinnedOnlyButton.Content = _showPinnedOnly ? "📌 همه موارد" : "📌 فقط پین‌ها"; }
    }

    private void ShowPanel()
    {
        // Recover from a stale synthetic Ctrl state left by any previous run.
        ReleaseCtrlKeys();
        var foreground = GetForegroundWindow();
        var ownWindow = new WindowInteropHelper(this).Handle;
        if (foreground != IntPtr.Zero && foreground != ownWindow) _pasteTarget = foreground;
        GetCursorPos(out var cursor);
        var monitor = MonitorFromPoint(cursor, 2);
        var info = new MONITORINFO { size = (uint)Marshal.SizeOf<MONITORINFO>() };
        GetMonitorInfo(monitor, ref info);
        Show();
        var hwnd = new WindowInteropHelper(this).Handle;
        GetWindowRect(hwnd, out var windowRect);
        var x = info.workArea.right - (windowRect.right - windowRect.left) - 22;
        var y = info.workArea.bottom - (windowRect.bottom - windowRect.top) - 22;
        SetWindowPos(hwnd, new IntPtr(-1), x, y, 0, 0, 0x0001 | 0x0010);
        Activate(); SearchBox.Clear(); SearchBox.Focus();
    }

    private async void Paste(ClipItem item)
    {
        try
        {
            _suppressCapture = true;
            if (item.Kind == ClipKind.Text) Clipboard.SetText(item.Text ?? "");
            else if (item.Kind == ClipKind.Files && item.Files is { Length: > 0 })
            {
                var list = new System.Collections.Specialized.StringCollection(); list.AddRange(item.Files); Clipboard.SetFileDropList(list);
            }
            else if (item.Kind == ClipKind.Image && item.LiveImage is { } liveImage) Clipboard.SetImage(liveImage);
            else if (item.Kind == ClipKind.Image && !string.IsNullOrEmpty(item.ImageBase64))
            {
                using var stream = new MemoryStream(Convert.FromBase64String(item.ImageBase64));
                var bitmap = new BitmapImage(); bitmap.BeginInit(); bitmap.CacheOption = BitmapCacheOption.OnLoad; bitmap.StreamSource = stream; bitmap.EndInit(); bitmap.Freeze(); Clipboard.SetImage(bitmap);
            }
            Hide();
            await Task.Delay(70);
            RestorePasteTarget();
            await Task.Delay(80);
            SendCtrlV();
            await Task.Delay(250);
        }
        catch (Exception ex) { MessageBox.Show($"امکان چسباندن این مورد نبود.\n{ex.Message}", "Win+V Clipboard", MessageBoxButton.OK, MessageBoxImage.Warning); }
        finally { _suppressCapture = false; }
    }

    private void InstallKeyboardHook()
    {
        _keyboardProc = KeyboardHookCallback;
        _hookThread = new Thread(() =>
        {
            _hookThreadId = GetCurrentThreadId();
            _keyboardHook = SetWindowsHookEx(WH_KEYBOARD_LL, _keyboardProc, GetModuleHandle(null), 0);
            if (_keyboardHook == IntPtr.Zero) Dispatcher.BeginInvoke(() => MessageBox.Show("رهگیری Win+V فعال نشد. برنامه را با دسترسی Administrator اجرا کنید.", "Win+V Clipboard"));
            while (GetMessage(out var message, IntPtr.Zero, 0, 0) > 0) { }
            if (_keyboardHook != IntPtr.Zero) UnhookWindowsHookEx(_keyboardHook);
        }) { IsBackground = true, Name = "WinV Hotkey Hook" };
        _hookThread.Start();
    }

    private IntPtr KeyboardHookCallback(int code, IntPtr wParam, IntPtr lParam)
    {
        if (code >= 0)
        {
            var key = Marshal.ReadInt32(lParam); var flags = Marshal.ReadInt32(lParam, 8); var message = wParam.ToInt32();
            var extraInfo = Marshal.ReadIntPtr(lParam, 16);
            if ((flags & 0x10) != 0 && extraInfo.ToInt64() == (long)InjectionMarker.ToUInt64())
                return CallNextHookEx(_keyboardHook, code, wParam, lParam);
            var down = message is WM_KEYDOWN or WM_SYSKEYDOWN; var up = message is WM_KEYUP or WM_SYSKEYUP;

            // Hold Win back until the next key reveals whether this is Win+V.
            if (key == VK_LWIN || key == VK_RWIN)
            {
                if (down)
                {
                    if (_pendingWinKey == 0 && !_winVChord) _pendingWinKey = key;
                    return new IntPtr(1);
                }
                if (up && _winVChord)
                {
                    _winVChord = false; _pendingWinKey = 0;
                    return new IntPtr(1);
                }
                if (up && _pendingWinKey != 0)
                {
                    var pending = (byte)_pendingWinKey; _pendingWinKey = 0;
                    InjectKeyDown(pending); InjectKeyUp(pending); // Preserve lone Win = Start.
                    return new IntPtr(1);
                }
                return CallNextHookEx(_keyboardHook, code, wParam, lParam);
            }

            if (down && _pendingWinKey != 0)
            {
                if (key == VK_V)
                {
                    _blockedV = true; _winVChord = true; _pendingWinKey = 0;
                    Dispatcher.BeginInvoke(ShowPanel);
                    return new IntPtr(1);
                }
                var pending = (byte)_pendingWinKey; _pendingWinKey = 0;
                InjectKeyDown(pending); // Forward every other Win shortcut normally.
            }
            if (key == VK_V && down && _winVChord) return new IntPtr(1);
            if (key == VK_V && up && _blockedV) { _blockedV = false; return new IntPtr(1); }
        }
        return CallNextHookEx(_keyboardHook, code, wParam, lParam);
    }

    private void SaveHistory()
    {
        _saveDebounce?.Cancel();
        _saveDebounce = new CancellationTokenSource();
        var token = _saveDebounce.Token;
        var snapshot = _all.ToArray();
        _ = Task.Run(async () =>
        {
            string? temp = null;
            try
            {
                await Task.Delay(180, token);
                var json = JsonSerializer.Serialize(snapshot);
                token.ThrowIfCancellationRequested();
                temp = _storePath + "." + Guid.NewGuid().ToString("N") + ".tmp";
                await File.WriteAllTextAsync(temp, json, token);
                File.Move(temp, _storePath, true); temp = null;
            }
            catch (OperationCanceledException) { }
            catch { }
            finally { if (temp != null) try { File.Delete(temp); } catch { } }
        });
    }

    private void SaveHistoryImmediate()
    {
        try { _saveDebounce?.Cancel(); File.WriteAllText(_storePath, JsonSerializer.Serialize(_all)); } catch { }
    }
    private void LoadHistory()
    {
        try { if (File.Exists(_storePath)) foreach (var item in (JsonSerializer.Deserialize<List<ClipItem>>(File.ReadAllText(_storePath)) ?? []).Take(MaxItems)) _all.Add(item); } catch { }
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        if (!_reallyExit) { e.Cancel = true; Hide(); return; }
        ReleaseCtrlKeys();
        _pendingWinKey = 0; _winVChord = false;
        if (_source != null) RemoveClipboardFormatListener(_source.Handle);
        if (_hookThreadId != 0) PostThreadMessage(_hookThreadId, 0x0012, IntPtr.Zero, IntPtr.Zero);
        SaveHistoryImmediate(); base.OnClosing(e);
    }

    private void SearchBox_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e) => RefreshFilter();
    private void Window_Deactivated(object? sender, EventArgs e) { if (IsVisible) Hide(); }
    private void HideButton_Click(object sender, RoutedEventArgs e) => Hide();
    private void Window_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.System && e.SystemKey == Key.F4)
        {
            e.Handled = true; _reallyExit = true; Close(); Application.Current.Shutdown();
        }
        else if (e.Key == Key.Escape) Hide();
        else if ((Keyboard.Modifiers & ModifierKeys.Control) != 0 && GetShortcutNumber(e.Key) is var number && number > 0)
        {
            var pinned = _all.Where(x => x.IsPinned).OrderByDescending(x => x.CopiedAt).ToList();
            if (number <= pinned.Count) { e.Handled = true; Paste(pinned[number - 1]); }
        }
        else if (e.Key == Key.Enter && ClipsList.SelectedItem is ClipItem item) Paste(item);
        else if (e.Key == Key.Down && ClipsList.Items.Count > 0)
        {
            e.Handled = true;
            ClipsList.SelectedIndex = Math.Min(ClipsList.Items.Count - 1, ClipsList.SelectedIndex + 1);
            ClipsList.ScrollIntoView(ClipsList.SelectedItem);
        }
        else if (e.Key == Key.Up && ClipsList.Items.Count > 0)
        {
            e.Handled = true;
            ClipsList.SelectedIndex = ClipsList.SelectedIndex <= 0 ? 0 : ClipsList.SelectedIndex - 1;
            ClipsList.ScrollIntoView(ClipsList.SelectedItem);
        }
    }
    private void ClipsList_MouseDoubleClick(object sender, MouseButtonEventArgs e) { if (ClipsList.SelectedItem is ClipItem item) Paste(item); }
    private void PinButton_Click(object sender, RoutedEventArgs e) { if (((FrameworkElement)sender).Tag is ClipItem item) { item.IsPinned = !item.IsPinned; SaveHistory(); RefreshFilter(); } }
    private void PinnedOnlyButton_Click(object sender, RoutedEventArgs e) { _showPinnedOnly = !_showPinnedOnly; RefreshFilter(); }
    private void DeleteButton_Click(object sender, RoutedEventArgs e) { if (((FrameworkElement)sender).Tag is ClipItem item) { _all.Remove(item); SaveHistory(); RefreshFilter(); } }
    private void ClearButton_Click(object sender, RoutedEventArgs e) { foreach (var item in _all.Where(x => !x.IsPinned).ToList()) _all.Remove(item); SaveHistory(); RefreshFilter(); }
    private void ExitButton_Click(object sender, RoutedEventArgs e) { _reallyExit = true; Close(); Application.Current.Shutdown(); }

    private static int GetShortcutNumber(Key key) => key switch
    {
        Key.D1 or Key.NumPad1 => 1, Key.D2 or Key.NumPad2 => 2, Key.D3 or Key.NumPad3 => 3,
        Key.D4 or Key.NumPad4 => 4, Key.D5 or Key.NumPad5 => 5, Key.D6 or Key.NumPad6 => 6,
        Key.D7 or Key.NumPad7 => 7, Key.D8 or Key.NumPad8 => 8, Key.D9 or Key.NumPad9 => 9, _ => 0
    };

    private static void SendCtrlV()
    {
        ReleaseCtrlKeys();
        try
        {
            InjectKeyDown(0xA2); // Left Ctrl
            InjectKeyDown(0x56); // V
            InjectKeyUp(0x56);
        }
        finally
        {
            // Send key-up separately and redundantly. A partial SendInput call can
            // never leave Ctrl held after this block.
            ReleaseCtrlKeys();
        }
    }

    private static void ReleaseCtrlKeys()
    {
        InjectKeyUp(0x11); InjectKeyUp(0xA2); InjectKeyUp(0xA3);
    }

    private void RestorePasteTarget()
    {
        if (_pasteTarget == IntPtr.Zero || !IsWindow(_pasteTarget)) return;
        // SW_RESTORE also unmaximizes a maximized window, so use it only when
        // the target is actually minimized.
        if (IsIconic(_pasteTarget)) ShowWindowAsync(_pasteTarget, 9);
        var targetThread = GetWindowThreadProcessId(_pasteTarget, out _);
        var currentThread = GetCurrentThreadId();
        var attached = targetThread != 0 && targetThread != currentThread && AttachThreadInput(currentThread, targetThread, true);
        BringWindowToTop(_pasteTarget);
        SetForegroundWindow(_pasteTarget);
        if (attached) AttachThreadInput(currentThread, targetThread, false);
    }

    private static void InjectKeyDown(byte key) => keybd_event(key, 0, 0, InjectionMarker);
    private static void InjectKeyUp(byte key) => keybd_event(key, 0, 2, InjectionMarker);

    private delegate IntPtr LowLevelKeyboardProc(int nCode, IntPtr wParam, IntPtr lParam);
    [DllImport("user32.dll")] private static extern bool AddClipboardFormatListener(IntPtr hwnd);
    [DllImport("user32.dll")] private static extern bool RemoveClipboardFormatListener(IntPtr hwnd);
    [DllImport("user32.dll")] private static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] private static extern bool SetForegroundWindow(IntPtr hwnd);
    [DllImport("user32.dll")] private static extern bool BringWindowToTop(IntPtr hwnd);
    [DllImport("user32.dll")] private static extern bool IsWindow(IntPtr hwnd);
    [DllImport("user32.dll")] private static extern bool IsIconic(IntPtr hwnd);
    [DllImport("user32.dll")] private static extern bool ShowWindowAsync(IntPtr hwnd, int command);
    [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(IntPtr hwnd, out uint processId);
    [DllImport("user32.dll")] private static extern bool AttachThreadInput(uint attach, uint attachTo, bool value);
    [DllImport("user32.dll")] private static extern short GetAsyncKeyState(int vKey);
    [DllImport("user32.dll", SetLastError = true)] private static extern IntPtr SetWindowsHookEx(int idHook, LowLevelKeyboardProc callback, IntPtr module, uint threadId);
    [DllImport("user32.dll")] private static extern bool UnhookWindowsHookEx(IntPtr hook);
    [DllImport("user32.dll")] private static extern IntPtr CallNextHookEx(IntPtr hook, int code, IntPtr wParam, IntPtr lParam);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)] private static extern IntPtr GetModuleHandle(string? name);
    [DllImport("user32.dll")] private static extern void keybd_event(byte key, byte scanCode, uint flags, UIntPtr extraInfo);
    [DllImport("user32.dll")] private static extern bool GetCursorPos(out POINT point);
    [DllImport("user32.dll")] private static extern IntPtr MonitorFromPoint(POINT point, uint flags);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern bool GetMonitorInfo(IntPtr monitor, ref MONITORINFO info);
    [DllImport("user32.dll")] private static extern bool GetWindowRect(IntPtr hwnd, out RECT rect);
    [DllImport("user32.dll")] private static extern bool SetWindowPos(IntPtr hwnd, IntPtr insertAfter, int x, int y, int width, int height, uint flags);
    [DllImport("user32.dll")] private static extern int GetMessage(out MSG message, IntPtr hwnd, uint min, uint max);
    [DllImport("user32.dll")] private static extern bool PostThreadMessage(uint threadId, uint message, IntPtr wParam, IntPtr lParam);
    [DllImport("kernel32.dll")] private static extern uint GetCurrentThreadId();
    [StructLayout(LayoutKind.Sequential)] private struct POINT { public int x, y; }
    [StructLayout(LayoutKind.Sequential)] private struct RECT { public int left, top, right, bottom; }
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)] private struct MONITORINFO { public uint size; public RECT monitorArea; public RECT workArea; public uint flags; }
    [StructLayout(LayoutKind.Sequential)] private struct MSG { public IntPtr hwnd; public uint message; public UIntPtr wParam; public IntPtr lParam; public uint time; public POINT point; public uint privateData; }
}

public enum ClipKind { Text, Image, Files }
public sealed class ClipItem : INotifyPropertyChanged
{
    private bool _isPinned;
    private string _shortcutLabel = "";
    private BitmapSource? _thumbnail;
    public Guid Id { get; set; } = Guid.NewGuid(); public ClipKind Kind { get; set; } public string? Text { get; set; } public string? ImageBase64 { get; set; } public string[]? Files { get; set; } public DateTime CopiedAt { get; set; } = DateTime.Now;
    public bool IsPinned { get => _isPinned; set { _isPinned = value; Changed(); Changed(nameof(PinGlyph)); Changed(nameof(Details)); } }
    [JsonIgnore] public string ShortcutLabel { get => _shortcutLabel; set { if (_shortcutLabel == value) return; _shortcutLabel = value; Changed(); Changed(nameof(Details)); } }
    [JsonIgnore] public BitmapSource? Thumbnail
    {
        get
        {
            if (_thumbnail != null || Kind != ClipKind.Image || string.IsNullOrEmpty(ImageBase64)) return _thumbnail;
            try { using var stream = new MemoryStream(Convert.FromBase64String(ImageBase64)); var image = new BitmapImage(); image.BeginInit(); image.CacheOption = BitmapCacheOption.OnLoad; image.DecodePixelWidth = 320; image.StreamSource = stream; image.EndInit(); image.Freeze(); _thumbnail = image; } catch { }
            return _thumbnail;
        }
    }
    [JsonIgnore] public BitmapSource? LiveImage { get; set; }
    public string Icon => Kind switch { ClipKind.Image => "▧", ClipKind.Files => "▤", _ => "T" }; public string PinGlyph => IsPinned ? "📌" : "📍";
    [JsonIgnore] public FlowDirection TextFlowDirection
    {
        get
        {
            foreach (var c in Preview)
            {
                if ((c >= '\u0600' && c <= '\u06FF') || (c >= '\u0750' && c <= '\u077F') || (c >= '\u08A0' && c <= '\u08FF')) return FlowDirection.RightToLeft;
                if ((c >= 'A' && c <= 'Z') || (c >= 'a' && c <= 'z')) return FlowDirection.LeftToRight;
            }
            return FlowDirection.LeftToRight;
        }
    }
    public string Preview => Kind switch { ClipKind.Text => (Text ?? "").Replace("\r", " ").Replace("\n", " "), ClipKind.Image => "تصویر ذخیره‌شده", ClipKind.Files => string.Join("، ", (Files ?? []).Select(Path.GetFileName)), _ => "" };
    public string Details => $"{(IsPinned ? $"پین‌شده{(ShortcutLabel.Length > 0 ? $" ({ShortcutLabel})" : "")} • " : "")}{Kind switch { ClipKind.Text => "متن", ClipKind.Image => "تصویر", _ => $"{Files?.Length ?? 0} فایل" }} • {CopiedAt:yyyy/MM/dd HH:mm}";
    public string SearchText => Kind == ClipKind.Text ? Text ?? "" : string.Join(" ", Files ?? []);
    public string Fingerprint => Kind switch { ClipKind.Text => "T:" + Text, ClipKind.Image => "I:" + (ImageBase64?.GetHashCode() ?? 0), _ => "F:" + string.Join("|", Files ?? []) };
    public static ClipItem FromText(string text) => new() { Kind = ClipKind.Text, Text = text };
    public static ClipItem FromFiles(string[] files) => new() { Kind = ClipKind.Files, Files = files };
    public static ClipItem FromImage(BitmapSource image)
    {
        var frozen = image.Clone(); frozen.Freeze();
        return new ClipItem { Kind = ClipKind.Image, LiveImage = frozen, _thumbnail = frozen };
    }
    public event PropertyChangedEventHandler? PropertyChanged; private void Changed([CallerMemberName] string? name = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
