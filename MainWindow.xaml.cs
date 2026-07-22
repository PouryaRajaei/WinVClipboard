using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.IO.Compression;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using MaterialDesignThemes.Wpf;
using FlowDirection = System.Windows.FlowDirection;
using KeyEventArgs = System.Windows.Input.KeyEventArgs;

namespace WinVClipboard;

public partial class MainWindow : Window
{
    private const int WM_CLIPBOARDUPDATE = 0x031D, WM_HOTKEY = 0x0312, WH_KEYBOARD_LL = 13, PANEL_HOTKEY_ID = 0x4356;
    private const int WM_KEYDOWN = 0x0100, WM_KEYUP = 0x0101, WM_SYSKEYDOWN = 0x0104, WM_SYSKEYUP = 0x0105;
    private const int VK_V = 0x56, VK_LWIN = 0x5B, VK_RWIN = 0x5C;
    private readonly ObservableCollection<ClipItem> _all = [], _filtered = [];
    private readonly List<CategoryDefinition> _categories = [];
    private readonly List<TextShortcut> _textShortcuts = [];
    private readonly object _shortcutLock = new();
    private readonly StringBuilder _typedBuffer = new();
    private readonly string _storePath, _categoriesPath, _textShortcutsPath;
    private const string UncategorizedFilter = "\u0001UNCATEGORIZED";
    private string? _selectedCategory;
    private HwndSource? _source;
    private IntPtr _keyboardHook, _pasteTarget;
    private LowLevelKeyboardProc? _keyboardProc;
    private Thread? _hookThread;
    private uint _hookThreadId;
    private CancellationTokenSource? _saveDebounce;
    private int _pendingWinKey;
    private int _blockedSuggestionKey;
    private IntPtr _typingTarget;
    private TextSuggestionWindow? _suggestion;
    private TextShortcut? _activeShortcut;
    private Action<Key, ModifierKeys>? _hotkeyCapture;
    private int _blockedRecordingKey;
    private bool _recordingWin;
    private bool _suppressCapture, _blockedV, _winVChord, _reallyExit, _showPinnedOnly;
    private bool _useWinHookHotkey = true;
    private string _lastWorkingHotkey = "Win+V";
    private static readonly UIntPtr InjectionMarker = new(0xC0D3);

    public MainWindow()
    {
        InitializeComponent();
        ApplyPanelSize(Localizer.CurrentPanelSize);
        ClipsList.ItemsSource = _filtered;
        var folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "WinVClipboard");
        Directory.CreateDirectory(folder);
        _storePath = Path.Combine(folder, "history.json");
        _categoriesPath = Path.Combine(folder, "categories.json");
        _textShortcutsPath = Path.Combine(folder, "text-shortcuts.json");
        LoadHistory(); LoadCategories(); LoadTextShortcuts(); RefreshFilter();
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
        UpdateRegisteredHotkey(false);
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == WM_CLIPBOARDUPDATE && !_suppressCapture) Dispatcher.BeginInvoke(CaptureClipboard, DispatcherPriority.Background);
        else if (msg == WM_HOTKEY && wParam.ToInt32() == PANEL_HOTKEY_ID) { Dispatcher.BeginInvoke(ShowPanel); handled = true; }
        return IntPtr.Zero;
    }

    private void CaptureClipboard()
    {
        if (Localizer.PauseCapture || IsForegroundProcessExcluded()) return;
        try
        {
            ClipItem? clip = null;
            var imageNeedsEncoding = false;
            if (Clipboard.ContainsFileDropList()) clip = ClipItem.FromFiles(Clipboard.GetFileDropList().Cast<string>().ToArray());
            else if (Localizer.CaptureImages && Clipboard.ContainsImage() && Clipboard.GetImage() is { } image) { clip = ClipItem.FromImage(image); imageNeedsEncoding = true; }
            else if (Clipboard.ContainsText() && Clipboard.GetText() is { Length: > 0 } text) clip = ClipItem.FromText(text);
            if (clip == null) return;
            var duplicate = _all.FirstOrDefault(x => x.Fingerprint == clip.Fingerprint);
            if (duplicate != null) { _all.Remove(duplicate); duplicate.CopiedAt = DateTime.Now; _all.Insert(0, duplicate); }
            else _all.Insert(0, clip);
            CleanupExpiredItems();
            while (_all.Count > Localizer.MaxHistory && _all.LastOrDefault(x => !x.IsPinned) is { } old) _all.Remove(old);
            // Update the visible list before any image encoding or disk I/O.
            RefreshFilter();
            if (imageNeedsEncoding) _ = EncodeImageAndSaveAsync(clip);
            else SaveHistory();
        }
        catch (COMException) { Dispatcher.BeginInvoke(CaptureClipboard, DispatcherPriority.ApplicationIdle); }
        catch { }
    }

    private bool IsForegroundProcessExcluded()
    {
        try
        {
            var excluded = Localizer.ExcludedApps.Split([',', ';', '\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (excluded.Length == 0) return false;
            var hwnd = GetForegroundWindow(); GetWindowThreadProcessId(hwnd, out var processId);
            var name = System.Diagnostics.Process.GetProcessById((int)processId).ProcessName;
            return excluded.Any(x => string.Equals(Path.GetFileNameWithoutExtension(x), name, StringComparison.OrdinalIgnoreCase));
        }
        catch { return false; }
    }

    private void CleanupExpiredItems()
    {
        if (Localizer.AutoDeleteDays <= 0) return;
        var cutoff = DateTime.Now.AddDays(-Localizer.AutoDeleteDays);
        foreach (var item in _all.Where(x => !x.IsPinned && x.CopiedAt < cutoff).ToList()) _all.Remove(item);
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
        for (var i = 0; i < pinned.Count; i++) pinned[i].ShortcutLabel = i < 9 ? $"{Localizer.PinnedModifier}+{i + 1}" : "";
        foreach (var item in _all.Where(x => !x.IsPinned)) item.ShortcutLabel = "";
        _filtered.Clear();
        foreach (var item in _all.Where(x => MatchesCategory(x) && (!_showPinnedOnly || x.IsPinned) && (query.Length == 0 || x.SearchText.Contains(query, StringComparison.CurrentCultureIgnoreCase))).OrderBy(x => x.IsPinned).ThenByDescending(x => x.CopiedAt)) _filtered.Add(item);
        if (EmptyState != null) EmptyState.Visibility = _filtered.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        if (CountText != null) CountText.Text = $"{_all.Count:N0} / {Localizer.MaxHistory:N0}";
        if (PinnedOnlyButton != null) { PinnedOnlyButton.Foreground = _showPinnedOnly ? System.Windows.Media.Brushes.DeepSkyBlue : System.Windows.Media.Brushes.White; PinnedOnlyButton.Content = "📌"; PinnedOnlyButton.ToolTip = Localizer.T(_showPinnedOnly ? "ShowAll" : "PinnedOnly"); }
        RefreshCategoryChips();
    }

    private bool MatchesCategory(ClipItem item)
    {
        if (_selectedCategory == null) return true;
        if (!item.IsPinned) return false;
        if (_selectedCategory == UncategorizedFilter) return string.IsNullOrWhiteSpace(item.Category);
        return string.Equals(item.Category, _selectedCategory, StringComparison.CurrentCultureIgnoreCase);
    }

    private void RefreshCategoryChips()
    {
        if (CategoriesPanel == null) return;
        CategoriesPanel.Children.Clear();
        AddCategoryChip(Localizer.T("All"), null, "ViewGrid", false);
        AddCategoryChip(Localizer.T("Uncategorized"), UncategorizedFilter, "TagOff", false);
        foreach (var category in _categories.OrderBy(x => x.Name, StringComparer.CurrentCultureIgnoreCase)) AddCategoryChip(category.Name, category.Name, category.IconKind, true);
        var add = new System.Windows.Controls.Button { Content = MakeMaterialIcon("Plus", 18), ToolTip = Localizer.T("NewCategory"), Tag = "__ADD__", Margin = new Thickness(0, 0, 6, 0), Padding = new Thickness(9, 5, 9, 5) };
        add.Click += CategoryChip_Click; CategoriesPanel.Children.Add(add);
    }

    private void AddCategoryChip(string label, string? value, string iconKind, bool deletable)
    {
        var selected = value == null ? _selectedCategory == null : string.Equals(_selectedCategory, value, StringComparison.Ordinal);
        var chip = new System.Windows.Controls.Button
        {
            Content = MakeMaterialIcon(iconKind, 18), ToolTip = label, Tag = value ?? "__ALL__", Margin = new Thickness(0, 0, 6, 0), Padding = new Thickness(9, 5, 9, 5),
            Foreground = selected ? System.Windows.Media.Brushes.White : new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(185, 185, 185)),
            Background = selected ? new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(42, 112, 180)) : new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(48, 48, 48))
        };
        chip.Click += CategoryChip_Click;
        if (deletable)
        {
            var menu = new System.Windows.Controls.ContextMenu();
            var edit = new System.Windows.Controls.MenuItem { Header = Localizer.T("EditCategory"), Tag = value };
            edit.Click += EditCategory_Click; menu.Items.Add(edit);
            var remove = new System.Windows.Controls.MenuItem { Header = Localizer.T("DeleteCategory"), Tag = value };
            remove.Click += DeleteCategory_Click; menu.Items.Add(remove); chip.ContextMenu = menu;
        }
        CategoriesPanel.Children.Add(chip);
    }

    private static PackIcon MakeMaterialIcon(string kind, double size)
    {
        if (!Enum.TryParse<PackIconKind>(kind, true, out var parsed)) parsed = PackIconKind.Folder;
        return new PackIcon { Kind = parsed, Width = size, Height = size };
    }

    private void ShowPanel()
    {
        // Recover from a stale synthetic Ctrl state left by any previous run.
        ReleaseCtrlKeys();
        var foreground = GetForegroundWindow();
        var ownWindow = new WindowInteropHelper(this).Handle;
        if (foreground != IntPtr.Zero && foreground != ownWindow) _pasteTarget = foreground;
        Show();
        PositionPanelAtMouse();
        Activate(); SearchBox.Clear(); SearchBox.Focus();
    }

    private void PositionPanelAtMouse()
    {
        GetCursorPos(out var cursor);
        var monitor = MonitorFromPoint(cursor, 2);
        var info = new MONITORINFO { size = (uint)Marshal.SizeOf<MONITORINFO>() };
        GetMonitorInfo(monitor, ref info);
        var hwnd = new WindowInteropHelper(this).Handle;
        GetWindowRect(hwnd, out var windowRect);
        var x = info.workArea.right - (windowRect.right - windowRect.left) - 22;
        var y = info.workArea.bottom - (windowRect.bottom - windowRect.top) - 22;
        SetWindowPos(hwnd, new IntPtr(-1), x, y, 0, 0, 0x0001 | 0x0010);
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
        catch (Exception ex) { MessageBox.Show($"{Localizer.T("PasteError")}\n{ex.Message}", "Win+V Clipboard", MessageBoxButton.OK, MessageBoxImage.Warning); }
        finally { _suppressCapture = false; }
    }

    private void InstallKeyboardHook()
    {
        _keyboardProc = KeyboardHookCallback;
        _hookThread = new Thread(() =>
        {
            _hookThreadId = GetCurrentThreadId();
            _keyboardHook = SetWindowsHookEx(WH_KEYBOARD_LL, _keyboardProc, GetModuleHandle(null), 0);
            if (_keyboardHook == IntPtr.Zero) Dispatcher.BeginInvoke(() => MessageBox.Show(Localizer.T("HookError"), "Win+V Clipboard"));
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

            if (_hotkeyCapture != null)
            {
                var isModifier = key is 0x10 or 0x11 or 0x12 or 0xA0 or 0xA1 or 0xA2 or 0xA3 or 0xA4 or 0xA5;
                if (key is VK_LWIN or VK_RWIN) { _recordingWin = down || (!up && _recordingWin); return new IntPtr(1); }
                if (isModifier) return new IntPtr(1);
                if (up && key == _blockedRecordingKey) { _blockedRecordingKey = 0; return new IntPtr(1); }
                if (down)
                {
                    var modifiers = ModifierKeys.None;
                    if ((GetAsyncKeyState(0x11) & 0x8000) != 0) modifiers |= ModifierKeys.Control;
                    if ((GetAsyncKeyState(0x12) & 0x8000) != 0) modifiers |= ModifierKeys.Alt;
                    if ((GetAsyncKeyState(0x10) & 0x8000) != 0) modifiers |= ModifierKeys.Shift;
                    if (_recordingWin || (GetAsyncKeyState(VK_LWIN) & 0x8000) != 0 || (GetAsyncKeyState(VK_RWIN) & 0x8000) != 0) modifiers |= ModifierKeys.Windows;
                    _blockedRecordingKey = key;
                    var callback = _hotkeyCapture; Dispatcher.BeginInvoke(() => callback?.Invoke(KeyInterop.KeyFromVirtualKey(key), modifiers));
                    return new IntPtr(1);
                }
            }

            if (up && key == _blockedSuggestionKey) { _blockedSuggestionKey = 0; return new IntPtr(1); }
            if (down && _suggestion != null && (key == 0x09 || key == 0x1B))
            {
                _blockedSuggestionKey = key;
                if (key == 0x09) Dispatcher.BeginInvoke(AcceptTextSuggestion);
                else Dispatcher.BeginInvoke(DismissTextSuggestion);
                return new IntPtr(1);
            }
            if (down && _suggestion != null) Dispatcher.BeginInvoke(DismissTextSuggestion);

            // Win+V/Win+C need the low-level path so Windows' own panel stays suppressed.
            if (key == VK_LWIN || key == VK_RWIN)
            {
                if (!_useWinHookHotkey) return CallNextHookEx(_keyboardHook, code, wParam, lParam);
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
                var showKey = Localizer.ShowHotkey.Equals("Win+C", StringComparison.OrdinalIgnoreCase) ? 0x43 : VK_V;
                if (key == showKey)
                {
                    _blockedV = true; _winVChord = true; _pendingWinKey = 0;
                    Dispatcher.BeginInvoke(ShowPanel);
                    return new IntPtr(1);
                }
                var pending = (byte)_pendingWinKey; _pendingWinKey = 0;
                InjectKeyDown(pending); // Forward every other Win shortcut normally.
            }
            var activeShowKey = Localizer.ShowHotkey.Equals("Win+C", StringComparison.OrdinalIgnoreCase) ? 0x43 : VK_V;
            if (key == activeShowKey && down && _winVChord) return new IntPtr(1);
            if (key == activeShowKey && up && _blockedV) { _blockedV = false; return new IntPtr(1); }
            if (down) DetectTypedKey(key, Marshal.ReadInt32(lParam, 4));
        }
        return CallNextHookEx(_keyboardHook, code, wParam, lParam);
    }

    private void DetectTypedKey(int virtualKey, int scanCode)
    {
        var foreground = GetForegroundWindow();
        GetWindowThreadProcessId(foreground, out var processId);
        if (processId == (uint)Environment.ProcessId) return;
        if ((GetAsyncKeyState(0x11) & 0x8000) != 0 || (GetAsyncKeyState(0x12) & 0x8000) != 0 ||
            (GetAsyncKeyState(VK_LWIN) & 0x8000) != 0 || (GetAsyncKeyState(VK_RWIN) & 0x8000) != 0) return;

        if (virtualKey == 0x08) { if (_typedBuffer.Length > 0) _typedBuffer.Length--; return; }
        if (virtualKey is 0x0D or 0x1B or 0x25 or 0x26 or 0x27 or 0x28 or 0x2E) { _typedBuffer.Clear(); return; }
        var typed = TranslateKey(virtualKey, scanCode, foreground);
        if (typed.Length == 0) return;
        _typedBuffer.Append(typed);
        if (_typedBuffer.Length > 96) _typedBuffer.Remove(0, _typedBuffer.Length - 96);

        TextShortcut? match;
        lock (_shortcutLock)
            match = _textShortcuts.Where(x => x.Trigger.Length > 0 && _typedBuffer.ToString().EndsWith(x.Trigger, StringComparison.OrdinalIgnoreCase)).OrderByDescending(x => x.Trigger.Length).FirstOrDefault();
        if (match == null) return;
        _typingTarget = foreground;
        var snapshot = new TextShortcut { Trigger = match.Trigger, Description = match.Description };
        Dispatcher.BeginInvoke(() => ShowTextSuggestion(snapshot));
    }

    private static string TranslateKey(int virtualKey, int scanCode, IntPtr foreground)
    {
        var state = new byte[256];
        if (!GetKeyboardState(state)) return "";
        if ((GetAsyncKeyState(0x10) & 0x8000) != 0) state[0x10] = 0x80;
        var thread = GetWindowThreadProcessId(foreground, out _);
        var layout = GetKeyboardLayout(thread);
        var output = new StringBuilder(8);
        var count = ToUnicodeEx((uint)virtualKey, (uint)scanCode, state, output, output.Capacity, 0, layout);
        return count > 0 ? output.ToString(0, count) : "";
    }

    private void ShowTextSuggestion(TextShortcut shortcut)
    {
        DismissTextSuggestion();
        _activeShortcut = shortcut;
        var popup = new TextSuggestionWindow(shortcut);
        _suggestion = popup;
        popup.Accepted += AcceptTextSuggestion;
        popup.Closed += (_, _) => { if (ReferenceEquals(_suggestion, popup)) { _suggestion = null; _activeShortcut = null; } };

        var point = GetCaretScreenPoint(_typingTarget);
        popup.Show();
        var hwnd = new WindowInteropHelper(popup).Handle;
        GetWindowRect(hwnd, out var rect);
        var popupWidth = rect.right - rect.left; var popupHeight = rect.bottom - rect.top;
        var monitor = MonitorFromPoint(point, 2);
        var monitorInfo = new MONITORINFO { size = (uint)Marshal.SizeOf<MONITORINFO>() };
        GetMonitorInfo(monitor, ref monitorInfo);
        var work = monitorInfo.workArea;
        var x = point.x + 14;
        if (x + popupWidth > work.right - 4) x = point.x - popupWidth - 14;
        x = Math.Clamp(x, work.left + 4, Math.Max(work.left + 4, work.right - popupWidth - 4));
        var y = point.y - (popupHeight / 2);
        y = Math.Clamp(y, work.top + 4, Math.Max(work.top + 4, work.bottom - popupHeight - 4));
        SetWindowPos(hwnd, new IntPtr(-1), x, y, popupWidth, popupHeight, 0x0010);
    }

    private static POINT GetCaretScreenPoint(IntPtr target)
    {
        var thread = GetWindowThreadProcessId(target, out _);
        var info = new GUITHREADINFO { size = (uint)Marshal.SizeOf<GUITHREADINFO>() };
        if (GetGUIThreadInfo(thread, ref info) && info.caretWindow != IntPtr.Zero)
        {
            var point = new POINT { x = info.caretRect.left, y = info.caretRect.bottom };
            if (ClientToScreen(info.caretWindow, ref point)) return point;
        }
        GetCursorPos(out var fallback); return fallback;
    }

    private async void AcceptTextSuggestion()
    {
        var shortcut = _activeShortcut; var target = _typingTarget;
        if (shortcut == null) return;
        DismissTextSuggestion();
        RestoreWindow(target); await Task.Delay(45);
        for (var i = 0; i < shortcut.Trigger.Length; i++) { InjectKeyDown(0x08); InjectKeyUp(0x08); }
        SendUnicodeText(shortcut.Description);
        _typedBuffer.Clear();
    }

    private void DismissTextSuggestion()
    {
        var popup = _suggestion; _suggestion = null; _activeShortcut = null;
        if (popup?.IsVisible == true) popup.Close();
    }

    private static void SendUnicodeText(string text)
    {
        if (text.Length == 0) return;
        var inputs = new List<NATIVE_INPUT>(text.Length * 2);
        foreach (var c in text)
        {
            inputs.Add(NATIVE_INPUT.Unicode(c, false)); inputs.Add(NATIVE_INPUT.Unicode(c, true));
        }
        SendInput((uint)inputs.Count, inputs.ToArray(), Marshal.SizeOf<NATIVE_INPUT>());
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
        try { if (File.Exists(_storePath)) foreach (var item in (JsonSerializer.Deserialize<List<ClipItem>>(File.ReadAllText(_storePath)) ?? []).Take(Localizer.MaxHistory)) _all.Add(item); CleanupExpiredItems(); } catch { }
    }

    private void LoadCategories()
    {
        try
        {
            if (File.Exists(_categoriesPath))
            {
                var json = File.ReadAllText(_categoriesPath);
                try { _categories.AddRange(JsonSerializer.Deserialize<List<CategoryDefinition>>(json) ?? []); }
                catch { foreach (var name in JsonSerializer.Deserialize<List<string>>(json) ?? []) _categories.Add(new CategoryDefinition(name, "Folder")); }
            }
        }
        catch { }
        foreach (var category in _all.Select(x => x.Category).Where(x => !string.IsNullOrWhiteSpace(x)))
            if (!_categories.Any(x => x.Name.Equals(category, StringComparison.CurrentCultureIgnoreCase))) _categories.Add(new CategoryDefinition(category, "Folder"));
    }

    private void SaveCategories()
    {
        try { File.WriteAllText(_categoriesPath, JsonSerializer.Serialize(_categories.GroupBy(x => x.Name, StringComparer.CurrentCultureIgnoreCase).Select(x => x.First()).OrderBy(x => x.Name))); } catch { }
    }

    private void LoadTextShortcuts()
    {
        try { if (File.Exists(_textShortcutsPath)) lock (_shortcutLock) _textShortcuts.AddRange(JsonSerializer.Deserialize<List<TextShortcut>>(File.ReadAllText(_textShortcutsPath)) ?? []); } catch { }
    }

    private void SaveTextShortcuts()
    {
        try { lock (_shortcutLock) File.WriteAllText(_textShortcutsPath, JsonSerializer.Serialize(_textShortcuts)); } catch { }
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        if (!_reallyExit) { e.Cancel = true; Hide(); return; }
        ReleaseCtrlKeys();
        _pendingWinKey = 0; _winVChord = false;
        if (_source != null) RemoveClipboardFormatListener(_source.Handle);
        if (_source != null) UnregisterHotKey(_source.Handle, PANEL_HOTKEY_ID);
        if (_hookThreadId != 0) PostThreadMessage(_hookThreadId, 0x0012, IntPtr.Zero, IntPtr.Zero);
        DismissTextSuggestion(); SaveHistoryImmediate(); SaveCategories(); SaveTextShortcuts(); base.OnClosing(e);
    }

    private void SearchBox_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e) => RefreshFilter();
    private void Window_Deactivated(object? sender, EventArgs e) { if (OwnedWindows.Cast<Window>().Any(x => x.IsVisible)) return; if (IsVisible) Hide(); }
    private void HideButton_Click(object sender, RoutedEventArgs e) => Hide();
    private void Window_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.System && e.SystemKey == Key.F4)
        {
            e.Handled = true; _reallyExit = true; Close(); Application.Current.Shutdown();
        }
        else if (e.Key == Key.Escape) Hide();
        else if (((Localizer.PinnedModifier == "Alt" && (Keyboard.Modifiers & ModifierKeys.Alt) != 0) || (Localizer.PinnedModifier != "Alt" && (Keyboard.Modifiers & ModifierKeys.Control) != 0)) && GetShortcutNumber(e.Key) is var number && number > 0)
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
    private void LanguageButton_Click(object sender, RoutedEventArgs e)
    {
        Localizer.Toggle();
        FlowDirection = Localizer.Direction;
        foreach (var item in _all) item.RefreshLocalizedText();
        RefreshFilter();
    }
    private void SettingsButton_Click(object sender, RoutedEventArgs e) => new SettingsWindow(this).ShowDialog();

    private void ApplyPanelSize(PanelSize size)
    {
        (Width, Height) = size switch
        {
            PanelSize.Small => (360, 480),
            PanelSize.Large => (520, 720),
            _ => (430, 590)
        };
    }
    public void ApplySettings()
    {
        ApplyPanelSize(Localizer.CurrentPanelSize);
        FlowDirection = Localizer.Direction;
        foreach (var item in _all) item.RefreshLocalizedText();
        CleanupExpiredItems();
        while (_all.Count > Localizer.MaxHistory && _all.LastOrDefault(x => !x.IsPinned) is { } old) _all.Remove(old);
        RefreshFilter();
        UpdateRegisteredHotkey(true);
        Dispatcher.BeginInvoke(PositionPanelAtMouse, DispatcherPriority.Loaded);
    }

    private bool UpdateRegisteredHotkey(bool notifyFailure)
    {
        if (_source == null) return true;
        UnregisterHotKey(_source.Handle, PANEL_HOTKEY_ID);
        _useWinHookHotkey = Localizer.ShowHotkey.Equals("Win+V", StringComparison.OrdinalIgnoreCase) || Localizer.ShowHotkey.Equals("Win+C", StringComparison.OrdinalIgnoreCase);
        if (_useWinHookHotkey) { _lastWorkingHotkey = Localizer.ShowHotkey; return true; }
        if (!TryParseHotkey(Localizer.ShowHotkey, out var modifiers, out var key) || !RegisterHotKey(_source.Handle, PANEL_HOTKEY_ID, modifiers | 0x4000, key))
        {
            if (notifyFailure) MessageBox.Show(Localizer.IsPersian ? "این میانبر توسط برنامهٔ دیگری استفاده می‌شود یا معتبر نیست." : "This hotkey is invalid or already used by another application.", "WinVClipboard", MessageBoxButton.OK, MessageBoxImage.Warning);
            Localizer.ShowHotkey = _lastWorkingHotkey; Localizer.Save();
            return false;
        }
        _lastWorkingHotkey = Localizer.ShowHotkey;
        return true;
    }

    private static bool TryParseHotkey(string text, out uint modifiers, out uint virtualKey)
    {
        modifiers = 0; virtualKey = 0;
        var parts = text.Split('+', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length < 2) return false;
        foreach (var part in parts[..^1])
        {
            if (part.Equals("Alt", StringComparison.OrdinalIgnoreCase)) modifiers |= 0x0001;
            else if (part.Equals("Ctrl", StringComparison.OrdinalIgnoreCase) || part.Equals("Control", StringComparison.OrdinalIgnoreCase)) modifiers |= 0x0002;
            else if (part.Equals("Shift", StringComparison.OrdinalIgnoreCase)) modifiers |= 0x0004;
            else if (part.Equals("Win", StringComparison.OrdinalIgnoreCase) || part.Equals("Windows", StringComparison.OrdinalIgnoreCase)) modifiers |= 0x0008;
            else return false;
        }
        if (!Enum.TryParse<Key>(parts[^1], true, out var key)) return false;
        virtualKey = (uint)KeyInterop.VirtualKeyFromKey(key);
        return modifiers != 0 && virtualKey != 0;
    }

    public void ExportBackup()
    {
        var dialog = new Microsoft.Win32.SaveFileDialog { Filter = "WinVClipboard backup (*.wvcbackup)|*.wvcbackup", FileName = $"WinVClipboard-{DateTime.Now:yyyyMMdd}.wvcbackup" };
        if (dialog.ShowDialog(this) != true) return;
        SaveHistoryImmediate(); SaveCategories(); SaveTextShortcuts(); Localizer.Save();
        using var archive = ZipFile.Open(dialog.FileName, ZipArchiveMode.Create);
        foreach (var path in new[] { _storePath, _categoriesPath, _textShortcutsPath, Localizer.SettingsFilePath })
            if (File.Exists(path)) archive.CreateEntryFromFile(path, Path.GetFileName(path), CompressionLevel.Optimal);
        MessageBox.Show(Localizer.T("BackupDone"), "WinVClipboard");
    }

    public void ImportBackup()
    {
        var dialog = new Microsoft.Win32.OpenFileDialog { Filter = "WinVClipboard backup (*.wvcbackup)|*.wvcbackup" };
        if (dialog.ShowDialog(this) != true) return;
        try
        {
            using var archive = ZipFile.OpenRead(dialog.FileName);
            var targets = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                [Path.GetFileName(_storePath)] = _storePath, [Path.GetFileName(_categoriesPath)] = _categoriesPath,
                [Path.GetFileName(_textShortcutsPath)] = _textShortcutsPath, [Path.GetFileName(Localizer.SettingsFilePath)] = Localizer.SettingsFilePath
            };
            foreach (var entry in archive.Entries)
                if (targets.TryGetValue(entry.Name, out var target)) { Directory.CreateDirectory(Path.GetDirectoryName(target)!); entry.ExtractToFile(target, true); }
            MessageBox.Show($"{Localizer.T("RestoreDone")}\n{Localizer.T("RestartHint")}", "WinVClipboard");
        }
        catch (Exception ex) { MessageBox.Show(ex.Message, "WinVClipboard", MessageBoxButton.OK, MessageBoxImage.Warning); }
    }

    public void ShowFromTray() => Dispatcher.BeginInvoke(ShowPanel);
    public void OpenSettingsFromTray() => Dispatcher.BeginInvoke(() => { ShowPanel(); new SettingsWindow(this).ShowDialog(); });
    public void OpenTextShortcutsFromSettings() => TextShortcutsButton_Click(this, new RoutedEventArgs());
    public IReadOnlyList<TextShortcut> GetTextShortcuts() { lock (_shortcutLock) return _textShortcuts.Select(x => new TextShortcut { Trigger = x.Trigger, Description = x.Description }).ToList(); }
    public void SetTextShortcuts(IEnumerable<TextShortcut> items)
    {
        lock (_shortcutLock)
        {
            _textShortcuts.Clear();
            _textShortcuts.AddRange(items.Where(x => x.Trigger.Trim().Length > 1 && x.Description.Length > 0).Select(x => new TextShortcut { Trigger = x.Trigger.Trim(), Description = x.Description }));
        }
        SaveTextShortcuts(); _typedBuffer.Clear();
    }
    public void ClearUnpinnedFromSettings() => ClearButton_Click(this, new RoutedEventArgs());
    public void ExitFromTray() { _reallyExit = true; Close(); Application.Current.Shutdown(); }
    public void ExitForUpdate() { _reallyExit = true; Close(); Application.Current.Shutdown(); }
    public void BeginHotkeyRecording(Action<Key, ModifierKeys> callback) { _hotkeyCapture = callback; _recordingWin = false; _blockedRecordingKey = 0; }
    public void EndHotkeyRecording() { _hotkeyCapture = null; _recordingWin = false; _blockedRecordingKey = 0; }
    private void TextShortcutsButton_Click(object sender, RoutedEventArgs e)
    {
        List<TextShortcut> snapshot;
        lock (_shortcutLock) snapshot = _textShortcuts.Select(x => new TextShortcut { Trigger = x.Trigger, Description = x.Description }).ToList();
        var dialog = new TextShortcutManagerDialog(snapshot) { Owner = this };
        if (dialog.ShowDialog() != true) return;
        lock (_shortcutLock)
        {
            _textShortcuts.Clear();
            _textShortcuts.AddRange(dialog.ResultItems.Where(x => x.Trigger.Trim().Length > 1 && x.Description.Length > 0).Select(x => new TextShortcut { Trigger = x.Trigger.Trim(), Description = x.Description }));
        }
        SaveTextShortcuts(); _typedBuffer.Clear();
    }
    private void CategoryChip_Click(object sender, RoutedEventArgs e)
    {
        if (((FrameworkElement)sender).Tag as string == "__ADD__") { CreateCategory(); return; }
        var tag = ((FrameworkElement)sender).Tag as string;
        _selectedCategory = tag == "__ALL__" ? null : tag;
        _showPinnedOnly = false;
        RefreshFilter();
    }

    private CategoryDefinition? CreateCategory(ClipItem? assignTo = null)
    {
        var dialog = new CategoryEditorDialog { Owner = this };
        if (dialog.ShowDialog() != true) return null;
        var existing = _categories.FirstOrDefault(x => x.Name.Equals(dialog.CategoryName, StringComparison.CurrentCultureIgnoreCase));
        if (existing == null) { existing = new CategoryDefinition(dialog.CategoryName, dialog.IconKind); _categories.Add(existing); }
        else existing.IconKind = dialog.IconKind;
        SaveCategories();
        if (assignTo != null) { assignTo.Category = existing.Name; SaveHistory(); }
        RefreshFilter(); return existing;
    }

    private void CategoryButton_Click(object sender, RoutedEventArgs e)
    {
        if (((FrameworkElement)sender).Tag is not ClipItem item) return;
        var menu = new System.Windows.Controls.ContextMenu { PlacementTarget = (System.Windows.Controls.Button)sender };
        foreach (var category in _categories.OrderBy(x => x.Name, StringComparer.CurrentCultureIgnoreCase))
        {
            var header = new System.Windows.Controls.StackPanel { Orientation = System.Windows.Controls.Orientation.Horizontal };
            header.Children.Add(MakeMaterialIcon(category.IconKind, 17));
            header.Children.Add(new System.Windows.Controls.TextBlock { Text = (item.Category.Equals(category.Name, StringComparison.CurrentCultureIgnoreCase) ? "  ✓ " : "  ") + category.Name, VerticalAlignment = VerticalAlignment.Center });
            var choice = new System.Windows.Controls.MenuItem { Header = header };
            choice.Click += (_, _) => { item.Category = category.Name; SaveHistory(); RefreshFilter(); }; menu.Items.Add(choice);
        }
        if (_categories.Count > 0) menu.Items.Add(new System.Windows.Controls.Separator());
        var none = new System.Windows.Controls.MenuItem { Header = Localizer.T("Uncategorized") };
        none.Click += (_, _) => { item.Category = ""; SaveHistory(); RefreshFilter(); }; menu.Items.Add(none);
        var create = new System.Windows.Controls.MenuItem { Header = Localizer.T("NewCategoryMenu") };
        create.Click += (_, _) => CreateCategory(item); menu.Items.Add(create);
        menu.IsOpen = true;
    }

    private void DeleteCategory_Click(object sender, RoutedEventArgs e)
    {
        if (((FrameworkElement)sender).Tag is not string category) return;
        foreach (var item in _all.Where(x => x.Category.Equals(category, StringComparison.CurrentCultureIgnoreCase))) item.Category = "";
        _categories.RemoveAll(x => x.Name.Equals(category, StringComparison.CurrentCultureIgnoreCase));
        if (_selectedCategory?.Equals(category, StringComparison.CurrentCultureIgnoreCase) == true) _selectedCategory = null;
        SaveCategories(); SaveHistory(); RefreshFilter();
    }

    private void EditCategory_Click(object sender, RoutedEventArgs e)
    {
        if (((FrameworkElement)sender).Tag is not string oldName) return;
        var category = _categories.FirstOrDefault(x => x.Name.Equals(oldName, StringComparison.CurrentCultureIgnoreCase));
        if (category == null) return;
        var dialog = new CategoryEditorDialog(category.Name, category.IconKind) { Owner = this };
        if (dialog.ShowDialog() != true) return;
        foreach (var item in _all.Where(x => x.Category.Equals(oldName, StringComparison.CurrentCultureIgnoreCase))) item.Category = dialog.CategoryName;
        category.Name = dialog.CategoryName; category.IconKind = dialog.IconKind;
        if (_selectedCategory?.Equals(oldName, StringComparison.CurrentCultureIgnoreCase) == true) _selectedCategory = category.Name;
        SaveCategories(); SaveHistory(); RefreshFilter();
    }
    private void DeleteButton_Click(object sender, RoutedEventArgs e) { if (((FrameworkElement)sender).Tag is ClipItem item) { _all.Remove(item); SaveHistory(); RefreshFilter(); } }
    private void ClearButton_Click(object sender, RoutedEventArgs e) { foreach (var item in _all.Where(x => !x.IsPinned).ToList()) _all.Remove(item); SaveHistory(); RefreshFilter(); }
    private void ClearAllButton_Click(object sender, RoutedEventArgs e)
    {
        if (MessageBox.Show(Localizer.T("ClearAllConfirm"), "WinVClipboard", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes) return;
        _all.Clear(); SaveHistory(); RefreshFilter();
    }
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

    private void RestorePasteTarget() => RestoreWindow(_pasteTarget);

    private static void RestoreWindow(IntPtr target)
    {
        if (target == IntPtr.Zero || !IsWindow(target)) return;
        // SW_RESTORE also unmaximizes a maximized window, so use it only when
        // the target is actually minimized.
        if (IsIconic(target)) ShowWindowAsync(target, 9);
        var targetThread = GetWindowThreadProcessId(target, out _);
        var currentThread = GetCurrentThreadId();
        var attached = targetThread != 0 && targetThread != currentThread && AttachThreadInput(currentThread, targetThread, true);
        BringWindowToTop(target);
        SetForegroundWindow(target);
        if (attached) AttachThreadInput(currentThread, targetThread, false);
    }

    private static void InjectKeyDown(byte key) => keybd_event(key, 0, 0, InjectionMarker);
    private static void InjectKeyUp(byte key) => keybd_event(key, 0, 2, InjectionMarker);

    private delegate IntPtr LowLevelKeyboardProc(int nCode, IntPtr wParam, IntPtr lParam);
    [DllImport("user32.dll")] private static extern bool AddClipboardFormatListener(IntPtr hwnd);
    [DllImport("user32.dll")] private static extern bool RegisterHotKey(IntPtr hwnd, int id, uint modifiers, uint virtualKey);
    [DllImport("user32.dll")] private static extern bool UnregisterHotKey(IntPtr hwnd, int id);
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
    [DllImport("user32.dll")] private static extern bool GetKeyboardState(byte[] state);
    [DllImport("user32.dll")] private static extern IntPtr GetKeyboardLayout(uint threadId);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern int ToUnicodeEx(uint virtualKey, uint scanCode, byte[] state, StringBuilder output, int capacity, uint flags, IntPtr layout);
    [DllImport("user32.dll")] private static extern bool GetGUIThreadInfo(uint threadId, ref GUITHREADINFO info);
    [DllImport("user32.dll")] private static extern bool ClientToScreen(IntPtr hwnd, ref POINT point);
    [DllImport("user32.dll")] private static extern uint SendInput(uint count, NATIVE_INPUT[] inputs, int size);
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
    [StructLayout(LayoutKind.Sequential)] private struct GUITHREADINFO { public uint size, flags; public IntPtr activeWindow, focusWindow, captureWindow, menuOwner, moveSizeWindow, caretWindow; public RECT caretRect; }
    [StructLayout(LayoutKind.Sequential)] private struct NATIVE_INPUT
    {
        public uint type; public NATIVE_INPUT_UNION data;
        public static NATIVE_INPUT Unicode(char value, bool keyUp) => new() { type = 1, data = new NATIVE_INPUT_UNION { keyboard = new NATIVE_KEYBOARD_INPUT { scanCode = value, flags = 0x0004u | (keyUp ? 0x0002u : 0u), extraInfo = InjectionMarker } } };
    }
    [StructLayout(LayoutKind.Explicit)] private struct NATIVE_INPUT_UNION { [FieldOffset(0)] public NATIVE_MOUSE_INPUT mouse; [FieldOffset(0)] public NATIVE_KEYBOARD_INPUT keyboard; [FieldOffset(0)] public NATIVE_HARDWARE_INPUT hardware; }
    [StructLayout(LayoutKind.Sequential)] private struct NATIVE_MOUSE_INPUT { public int dx, dy; public uint mouseData, flags, time; public UIntPtr extraInfo; }
    [StructLayout(LayoutKind.Sequential)] private struct NATIVE_KEYBOARD_INPUT { public ushort virtualKey, scanCode; public uint flags, time; public UIntPtr extraInfo; }
    [StructLayout(LayoutKind.Sequential)] private struct NATIVE_HARDWARE_INPUT { public uint message; public ushort low, high; }
}

public enum ClipKind { Text, Image, Files }

public sealed class CategoryDefinition
{
    public CategoryDefinition() { }
    public CategoryDefinition(string name, string iconKind) { Name = name; IconKind = iconKind; }
    public string Name { get; set; } = "";
    public string IconKind { get; set; } = "Folder";
}

public sealed class CategoryEditorDialog : Window
{
    private static readonly string[] MaterialIcons =
    [
        "Folder", "Briefcase", "Home", "CodeBraces", "Link", "Star", "Account", "Cart", "School", "Heart",
        "Bookmark", "Palette", "Music", "Image", "MessageText", "Lightbulb", "FormatListChecks", "Lock", "Cloud", "Tag",
        "Coffee", "GamepadVariant", "RocketLaunch", "Calendar", "Camera", "BookOpenPageVariant", "Finance", "MedicalBag", "Earth", "Tools"
    ];

    private readonly System.Windows.Controls.TextBox _nameBox;
    private readonly List<System.Windows.Controls.Button> _iconButtons = [];
    private string _selectedIcon;
    public string CategoryName => _nameBox.Text.Trim();
    public string IconKind => _selectedIcon;

    public CategoryEditorDialog(string name = "", string iconKind = "Folder")
    {
        _selectedIcon = iconKind;
        Title = Localizer.T("CategoryDialog"); Width = 430; Height = 430; WindowStartupLocation = WindowStartupLocation.CenterOwner;
        ResizeMode = ResizeMode.NoResize; ShowInTaskbar = false; Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(30, 30, 30));
        Foreground = System.Windows.Media.Brushes.White; FlowDirection = Localizer.Direction;

        var root = new System.Windows.Controls.Grid { Margin = new Thickness(18) };
        root.RowDefinitions.Add(new System.Windows.Controls.RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new System.Windows.Controls.RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new System.Windows.Controls.RowDefinition());
        root.RowDefinitions.Add(new System.Windows.Controls.RowDefinition { Height = GridLength.Auto });

        var title = new System.Windows.Controls.TextBlock { Text = Localizer.T("CategoryNameIcon"), FontSize = 18, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 0, 0, 12) };
        System.Windows.Controls.Grid.SetRow(title, 0); root.Children.Add(title);
        _nameBox = new System.Windows.Controls.TextBox { Text = name, FontSize = 14, Padding = new Thickness(10, 8, 10, 8), Margin = new Thickness(0, 0, 0, 14), FlowDirection = Localizer.Direction };
        System.Windows.Controls.Grid.SetRow(_nameBox, 1); root.Children.Add(_nameBox);

        var icons = new System.Windows.Controls.WrapPanel { FlowDirection = FlowDirection.LeftToRight };
        foreach (var iconName in MaterialIcons)
        {
            if (!Enum.TryParse<PackIconKind>(iconName, true, out var kind)) continue;
            var button = new System.Windows.Controls.Button { Content = new PackIcon { Kind = kind, Width = 23, Height = 23 }, Tag = iconName, ToolTip = iconName, Width = 44, Height = 42, Margin = new Thickness(4), Padding = new Thickness(8) };
            button.Click += Icon_Click; _iconButtons.Add(button); icons.Children.Add(button);
        }
        var scroll = new System.Windows.Controls.ScrollViewer { Content = icons, VerticalScrollBarVisibility = System.Windows.Controls.ScrollBarVisibility.Auto };
        System.Windows.Controls.Grid.SetRow(scroll, 2); root.Children.Add(scroll);

        var actions = new System.Windows.Controls.StackPanel { Orientation = System.Windows.Controls.Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Left, Margin = new Thickness(0, 12, 0, 0) };
        var save = new System.Windows.Controls.Button { Content = Localizer.T("Save"), Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(42, 112, 180)), Padding = new Thickness(18, 7, 18, 7), Margin = new Thickness(6, 0, 0, 0) };
        save.Click += (_, _) => Save();
        var cancel = new System.Windows.Controls.Button { Content = Localizer.T("Cancel"), Padding = new Thickness(18, 7, 18, 7) }; cancel.Click += (_, _) => { DialogResult = false; Close(); };
        actions.Children.Add(save); actions.Children.Add(cancel); System.Windows.Controls.Grid.SetRow(actions, 3); root.Children.Add(actions);
        Content = root;
        Loaded += (_, _) => { UpdateIconSelection(); _nameBox.Focus(); _nameBox.SelectAll(); };
        PreviewKeyDown += (_, e) => { if (e.Key == Key.Escape) { DialogResult = false; Close(); } else if (e.Key == Key.Enter && Keyboard.Modifiers == ModifierKeys.None) Save(); };
    }

    private void Icon_Click(object sender, RoutedEventArgs e) { _selectedIcon = (string)((FrameworkElement)sender).Tag; UpdateIconSelection(); }
    private void UpdateIconSelection()
    {
        foreach (var button in _iconButtons)
        {
            var selected = string.Equals((string)button.Tag, _selectedIcon, StringComparison.OrdinalIgnoreCase);
            button.Background = selected ? new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(42, 112, 180)) : System.Windows.Media.Brushes.Transparent;
            button.Opacity = selected ? 1 : .72;
        }
    }
    private void Save()
    {
        if (CategoryName.Length == 0) { _nameBox.Focus(); return; }
        DialogResult = true; Close();
    }
}

public sealed class ClipItem : INotifyPropertyChanged
{
    private bool _isPinned;
    private string _category = "";
    private string _shortcutLabel = "";
    private BitmapSource? _thumbnail;
    public Guid Id { get; set; } = Guid.NewGuid(); public ClipKind Kind { get; set; } public string? Text { get; set; } public string? ImageBase64 { get; set; } public string[]? Files { get; set; } public DateTime CopiedAt { get; set; } = DateTime.Now;
    public bool IsPinned { get => _isPinned; set { _isPinned = value; Changed(); Changed(nameof(PinGlyph)); Changed(nameof(Details)); Changed(nameof(CategoryVisibility)); } }
    public string Category { get => _category; set { _category = value ?? ""; Changed(); Changed(nameof(CategoryLabel)); Changed(nameof(Details)); } }
    [JsonIgnore] public string CategoryLabel => string.IsNullOrWhiteSpace(Category) ? Localizer.T("Category") : Category;
    [JsonIgnore] public Visibility CategoryVisibility => IsPinned ? Visibility.Visible : Visibility.Collapsed;
    [JsonIgnore] public string ShortcutLabel { get => _shortcutLabel; set { if (_shortcutLabel == value) return; _shortcutLabel = value; Changed(); Changed(nameof(Details)); } }
    [JsonIgnore] public BitmapSource? Thumbnail
    {
        get
        {
            if (_thumbnail != null || Kind != ClipKind.Image || string.IsNullOrEmpty(ImageBase64)) return _thumbnail;
            try { using var stream = new MemoryStream(Convert.FromBase64String(ImageBase64)); var image = new BitmapImage(); image.BeginInit(); image.CacheOption = BitmapCacheOption.OnLoad; image.DecodePixelWidth = Math.Max(120, Localizer.ThumbnailSize); image.StreamSource = stream; image.EndInit(); image.Freeze(); _thumbnail = image; } catch { }
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
    public string Preview => Kind switch { ClipKind.Text => (Text ?? "").Replace("\r", " ").Replace("\n", " "), ClipKind.Image => Localizer.T("SavedImage"), ClipKind.Files => string.Join(Localizer.IsPersian ? "، " : ", ", (Files ?? []).Select(Path.GetFileName)), _ => "" };
    public string Details => $"{(IsPinned ? $"{Localizer.T("Pinned")}{(string.IsNullOrWhiteSpace(Category) ? "" : $" • {Category}")}{(ShortcutLabel.Length > 0 ? $" ({ShortcutLabel})" : "")} • " : "")}{Kind switch { ClipKind.Text => Localizer.T("Text"), ClipKind.Image => Localizer.T("Image"), _ => $"{Files?.Length ?? 0} {Localizer.T((Files?.Length ?? 0) == 1 ? "File" : "Files")}" }} • {CopiedAt:yyyy/MM/dd HH:mm}";
    public string SearchText => Kind == ClipKind.Text ? Text ?? "" : string.Join(" ", Files ?? []);
    public string Fingerprint => Kind switch { ClipKind.Text => "T:" + Text, ClipKind.Image => "I:" + (ImageBase64?.GetHashCode() ?? 0), _ => "F:" + string.Join("|", Files ?? []) };
    public static ClipItem FromText(string text) => new() { Kind = ClipKind.Text, Text = text };
    public static ClipItem FromFiles(string[] files) => new() { Kind = ClipKind.Files, Files = files };
    public static ClipItem FromImage(BitmapSource image)
    {
        var frozen = image.Clone(); frozen.Freeze();
        return new ClipItem { Kind = ClipKind.Image, LiveImage = frozen, _thumbnail = frozen };
    }
    public void RefreshLocalizedText() { Changed(nameof(CategoryLabel)); Changed(nameof(Preview)); Changed(nameof(Details)); Changed(nameof(TextFlowDirection)); }
    public event PropertyChangedEventHandler? PropertyChanged; private void Changed([CallerMemberName] string? name = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
