using System.IO;
using System.Text.Json;
using System.Windows;

namespace WinVClipboard;

public static class Localizer
{
    private static readonly Dictionary<string, (string Fa, string En)> Strings = new()
    {
        ["TextShortcuts"] = ("میانبرهای نوشتاری", "Text shortcuts"),
        ["Settings"] = ("تنظیمات", "Settings"),
        ["Language"] = ("زبان: فارسی", "Language: English"),
        ["SwitchLanguage"] = ("تغییر به English", "Switch to فارسی"),
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

    private static readonly string SettingsPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "WinVClipboard", "settings.json");

    public static bool IsPersian { get; private set; } = true;
    public static FlowDirection Direction => IsPersian ? FlowDirection.RightToLeft : FlowDirection.LeftToRight;
    public static string T(string key) => Strings.TryGetValue(key, out var value) ? (IsPersian ? value.Fa : value.En) : key;

    public static void Initialize()
    {
        try
        {
            if (File.Exists(SettingsPath))
            {
                var settings = JsonSerializer.Deserialize<LanguageSettings>(File.ReadAllText(SettingsPath));
                IsPersian = !string.Equals(settings?.Language, "en", StringComparison.OrdinalIgnoreCase);
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
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(SettingsPath)!);
            File.WriteAllText(SettingsPath, JsonSerializer.Serialize(new LanguageSettings { Language = IsPersian ? "fa" : "en" }));
        }
        catch { }
    }

    private static void ApplyResources()
    {
        if (Application.Current == null) return;
        foreach (var pair in Strings) Application.Current.Resources[pair.Key] = IsPersian ? pair.Value.Fa : pair.Value.En;
        Application.Current.Resources["AppFlowDirection"] = Direction;
        Application.Current.Resources["LanguageLabel"] = IsPersian ? "EN" : "فا";
    }

    private sealed class LanguageSettings { public string Language { get; set; } = "fa"; }
}
