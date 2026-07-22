using System.IO;
using System.Text.Json;
using System.Windows;
using FlowDirection = System.Windows.FlowDirection;

namespace WinVClipboard;

public static class Localizer
{
    private static readonly Dictionary<string, (string Fa, string En)> Strings = new()
    {
        ["TextShortcuts"] = ("میانبرهای نوشتاری", "Text shortcuts"),
        ["Settings"] = ("تنظیمات", "Settings"),
        ["Open"] = ("بازکردن WinVClipboard", "Open WinVClipboard"),
        ["Language"] = ("زبان: فارسی", "Language: English"),
        ["SwitchLanguage"] = ("تغییر به English", "Switch to فارسی"),
        ["PanelSize"] = ("اندازهٔ پنل", "Panel size"),
        ["Small"] = ("کوچک", "Small"), ["Medium"] = ("متوسط", "Medium"), ["Large"] = ("بزرگ", "Large"),
        ["General"] = ("عمومی", "General"), ["Appearance"] = ("ظاهر", "Appearance"),
        ["Privacy"] = ("حریم خصوصی", "Privacy"), ["Backup"] = ("پشتیبان‌گیری", "Backup"),
        ["About"] = ("درباره", "About"), ["StartWithWindows"] = ("اجرا همراه ویندوز", "Start with Windows"),
        ["PauseCapture"] = ("توقف موقت ثبت کلیپ‌بورد", "Pause clipboard capture"),
        ["CaptureImages"] = ("ذخیرهٔ تصاویر", "Save images"),
        ["MaxHistory"] = ("حداکثر تعداد آیتم‌ها", "Maximum history items"),
        ["AutoDeleteDays"] = ("حذف خودکار پس از چند روز (۰ = خاموش)", "Auto-delete after days (0 = off)"),
        ["ExcludedApps"] = ("برنامه‌های مستثنا؛ نام پردازش‌ها با کاما", "Excluded apps; comma-separated process names"),
        ["Theme"] = ("پوسته", "Theme"), ["Dark"] = ("تیره", "Dark"), ["Light"] = ("روشن", "Light"), ["System"] = ("سیستم", "System"),
        ["ThumbnailSize"] = ("اندازهٔ تصویر بندانگشتی", "Thumbnail size"),
        ["ShowHotkey"] = ("میانبر نمایش پنل", "Panel hotkey"), ["PinnedModifier"] = ("کلید پین‌های ۱ تا ۹", "Pinned items 1–9 modifier"),
        ["ExportBackup"] = ("خروجی پشتیبان", "Export backup"), ["ImportBackup"] = ("بازیابی پشتیبان", "Import backup"),
        ["CheckUpdates"] = ("بررسی نسخهٔ جدید", "Check for updates"), ["UpToDate"] = ("آخرین نسخه نصب است.", "You are up to date."),
        ["UpdateAvailable"] = ("نسخهٔ جدید موجود است", "A new version is available"),
        ["BackupDone"] = ("فایل پشتیبان ذخیره شد.", "Backup saved."), ["RestoreDone"] = ("اطلاعات بازیابی شد.", "Backup restored."),
        ["RestartHint"] = ("برای اعمال کامل، برنامه را دوباره اجرا کنید.", "Restart the app to apply everything."),
        ["PinnedOnly"] = ("نمایش فقط موارد پین‌شده", "Show pinned items only"),
        ["ShowAll"] = ("نمایش همهٔ موارد", "Show all items"),
        ["Clear"] = ("پاک‌کردن", "Clear"),
        ["ClearUnpinned"] = ("حذف موارد پین‌نشده", "Delete unpinned items"),
        ["Exit"] = ("⏻ خروج", "⏻ Exit"),
        ["ExitFull"] = ("خروج از برنامه", "Exit application"),
        ["ExitTip"] = ("بستن کامل برنامه", "Exit the application completely"),
        ["Hide"] = ("مخفی‌کردن پنل", "Hide panel"),
        ["Search"] = ("جست‌وجو", "Search"),
        ["SelectCategory"] = ("انتخاب دسته", "Select category"),
        ["Pin"] = ("پین", "Pin"),
        ["Delete"] = ("حذف", "Delete"),
        ["Empty"] = ("هنوز چیزی کپی نشده است", "Nothing has been copied yet"),
        ["Footer"] = ("Enter: چسباندن   •   Esc: بستن   •   Win+V: نمایش", "Enter: Paste   •   Esc: Close   •   Win+V: Show"),
        ["All"] = ("همه", "All"), ["Uncategorized"] = ("بدون دسته", "Uncategorized"),
        ["NewCategory"] = ("دستهٔ جدید", "New category"),
        ["EditCategory"] = ("ویرایش نام و آیکن", "Edit name and icon"),
        ["DeleteCategory"] = ("حذف دسته", "Delete category"),
        ["NewCategoryMenu"] = ("＋ دستهٔ جدید...", "＋ New category..."),
        ["Category"] = ("دسته", "Category"),
        ["CategoryDialog"] = ("دسته‌بندی", "Category"),
        ["CategoryNameIcon"] = ("نام و آیکن دسته", "Category name and icon"),
        ["Save"] = ("ذخیره", "Save"), ["Cancel"] = ("انصراف", "Cancel"),
        ["Add"] = ("＋ افزودن", "＋ Add"), ["RemoveSelected"] = ("حذف انتخاب‌شده", "Remove selected"),
        ["Shortcut"] = ("میانبر", "Shortcut"), ["Description"] = ("متن جایگزین", "Description"),
        ["TextShortcutHint"] = ("مثال:  /addr1  ←  تهران، خیابان ایکس", "Example:  /addr1  →  Tehran, Example Street"),
        ["TriggerTip"] = ("Shortcut مثل /addr1", "Shortcut such as /addr1"),
        ["DescriptionTip"] = ("متن جایگزین", "Replacement text"),
        ["ConvertTab"] = ("تبدیل  Tab", "Replace  Tab"), ["CancelEsc"] = ("Esc: لغو", "Esc: Cancel"),
        ["PasteError"] = ("امکان چسباندن این مورد نبود.", "This item could not be pasted."),
        ["HookError"] = ("رهگیری Win+V فعال نشد. برنامه را با دسترسی Administrator اجرا کنید.", "Win+V could not be captured. Run the application as Administrator."),
        ["SavedImage"] = ("تصویر ذخیره‌شده", "Saved image"),
        ["Pinned"] = ("پین‌شده", "Pinned"), ["Text"] = ("متن", "Text"), ["Image"] = ("تصویر", "Image"),
        ["File"] = ("فایل", "file"), ["Files"] = ("فایل", "files"),
        ["LanguageTip"] = ("تغییر زبان به English", "Switch language to فارسی")
    };

    public static readonly string SettingsFilePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "WinVClipboard", "settings.json");

    public static bool IsPersian { get; private set; } = true;
    public static PanelSize CurrentPanelSize { get; private set; } = PanelSize.Medium;
    public static bool PauseCapture { get; set; }
    public static bool CaptureImages { get; set; } = true;
    public static int MaxHistory { get; set; } = 2000;
    public static int AutoDeleteDays { get; set; }
    public static string ExcludedApps { get; set; } = "";
    public static string Theme { get; set; } = "Dark";
    public static int ThumbnailSize { get; set; } = 245;
    public static string ShowHotkey { get; set; } = "Win+V";
    public static string PinnedModifier { get; set; } = "Ctrl";
    public static FlowDirection Direction => IsPersian ? FlowDirection.RightToLeft : FlowDirection.LeftToRight;
    public static string T(string key) => Strings.TryGetValue(key, out var value) ? (IsPersian ? value.Fa : value.En) : key;

    public static void Initialize()
    {
        try
        {
            if (File.Exists(SettingsFilePath))
            {
                var settings = JsonSerializer.Deserialize<LanguageSettings>(File.ReadAllText(SettingsFilePath));
                IsPersian = !string.Equals(settings?.Language, "en", StringComparison.OrdinalIgnoreCase);
                if (Enum.TryParse<PanelSize>(settings?.PanelSize, true, out var size)) CurrentPanelSize = size;
                PauseCapture = settings?.PauseCapture ?? false;
                CaptureImages = settings?.CaptureImages ?? true;
                MaxHistory = Math.Clamp(settings?.MaxHistory ?? 2000, 50, 10000);
                AutoDeleteDays = Math.Clamp(settings?.AutoDeleteDays ?? 0, 0, 3650);
                ExcludedApps = settings?.ExcludedApps ?? "";
                Theme = settings?.Theme ?? "Dark";
                ThumbnailSize = Math.Clamp(settings?.ThumbnailSize ?? 245, 100, 500);
                ShowHotkey = settings?.ShowHotkey is "Win+C" ? "Win+C" : "Win+V";
                PinnedModifier = settings?.PinnedModifier is "Alt" ? "Alt" : "Ctrl";
            }
            else IsPersian = true;
        }
        catch { IsPersian = true; }
        ApplyResources();
    }

    public static void Toggle()
    {
        IsPersian = !IsPersian;
        ApplyResources();
        SaveSettings();
    }

    public static void SetPanelSize(PanelSize size) { CurrentPanelSize = size; SaveSettings(); }
    public static void Save() { SaveSettings(); ApplyTheme(); ApplyComputedResources(); }

    private static void ApplyResources()
    {
        if (Application.Current == null) return;
        foreach (var pair in Strings) Application.Current.Resources[pair.Key] = IsPersian ? pair.Value.Fa : pair.Value.En;
        Application.Current.Resources["AppFlowDirection"] = Direction;
        Application.Current.Resources["LanguageLabel"] = IsPersian ? "EN" : "فا";
        ApplyComputedResources();
        ApplyTheme();
    }

    private static void ApplyComputedResources()
    {
        if (Application.Current == null) return;
        Application.Current.Resources["Footer"] = IsPersian
            ? $"Enter: چسباندن   •   Esc: بستن   •   {ShowHotkey}: نمایش"
            : $"Enter: Paste   •   Esc: Close   •   {ShowHotkey}: Show";
    }

    public static void ApplyTheme()
    {
        if (Application.Current == null) return;
        var glass = SystemParameters.WindowGlassColor;
        var systemIsLight = (glass.R * .299 + glass.G * .587 + glass.B * .114) > 150;
        var light = Theme == "Light" || (Theme == "System" && systemIsLight);
        Application.Current.Resources["PanelBrush"] = new System.Windows.Media.SolidColorBrush(light ? System.Windows.Media.Color.FromArgb(248, 245, 245, 245) : System.Windows.Media.Color.FromArgb(242, 30, 30, 30));
        Application.Current.Resources["CardBrush"] = new System.Windows.Media.SolidColorBrush(light ? System.Windows.Media.Color.FromRgb(255, 255, 255) : System.Windows.Media.Color.FromRgb(43, 43, 43));
        Application.Current.Resources["HoverBrush"] = new System.Windows.Media.SolidColorBrush(light ? System.Windows.Media.Color.FromRgb(232, 238, 245) : System.Windows.Media.Color.FromRgb(56, 56, 56));
        Application.Current.Resources["MutedBrush"] = new System.Windows.Media.SolidColorBrush(light ? System.Windows.Media.Color.FromRgb(85, 85, 85) : System.Windows.Media.Color.FromRgb(184, 184, 184));
        Application.Current.Resources["PrimaryTextBrush"] = new System.Windows.Media.SolidColorBrush(light ? System.Windows.Media.Colors.Black : System.Windows.Media.Colors.White);
        Application.Current.Resources["SearchBrush"] = new System.Windows.Media.SolidColorBrush(light ? System.Windows.Media.Color.FromRgb(238, 238, 238) : System.Windows.Media.Color.FromRgb(41, 41, 41));
        Application.Current.Resources["ThumbnailMaxWidth"] = (double)ThumbnailSize;
    }

    private static void SaveSettings()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(SettingsFilePath)!);
            File.WriteAllText(SettingsFilePath, JsonSerializer.Serialize(new LanguageSettings
            {
                Language = IsPersian ? "fa" : "en", PanelSize = CurrentPanelSize.ToString(), PauseCapture = PauseCapture,
                CaptureImages = CaptureImages, MaxHistory = MaxHistory, AutoDeleteDays = AutoDeleteDays, ExcludedApps = ExcludedApps,
                Theme = Theme, ThumbnailSize = ThumbnailSize, ShowHotkey = ShowHotkey, PinnedModifier = PinnedModifier
            }));
        }
        catch { }
    }

    private sealed class LanguageSettings
    {
        public string Language { get; set; } = "fa"; public string PanelSize { get; set; } = "Medium";
        public bool PauseCapture { get; set; } public bool CaptureImages { get; set; } = true;
        public int MaxHistory { get; set; } = 2000; public int AutoDeleteDays { get; set; }
        public string ExcludedApps { get; set; } = ""; public string Theme { get; set; } = "Dark";
        public int ThumbnailSize { get; set; } = 245; public string ShowHotkey { get; set; } = "Win+V"; public string PinnedModifier { get; set; } = "Ctrl";
    }
}

public enum PanelSize { Small, Medium, Large }
