using System.Globalization;
using System.IO;
using System.Text.Json;
using System.Windows.Threading;

namespace WinVClipboard;

public enum ReminderRepeat { None, Daily, Monthly, Yearly }
public enum ReminderCalendar { Persian, Gregorian }

public sealed class ReminderItem
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Title { get; set; } = "";
    public DateTime StartsAt { get; set; }
    public ReminderRepeat Repeat { get; set; }
    public ReminderCalendar Calendar { get; set; } = ReminderCalendar.Persian;
    public DateTime? LastNotifiedOccurrence { get; set; }
}

public static class ReminderStore
{
    private static readonly string Folder = Path.Combine(Environment.GetFolderPath(
        Environment.SpecialFolder.LocalApplicationData), "WinVClipboard");
    public static readonly string FilePath = Path.Combine(Folder, "reminders.json");

    public static List<ReminderItem> Load()
    {
        try { return File.Exists(FilePath) ? JsonSerializer.Deserialize<List<ReminderItem>>(File.ReadAllText(FilePath)) ?? [] : []; }
        catch { return []; }
    }

    public static void Save(IEnumerable<ReminderItem> reminders)
    {
        Directory.CreateDirectory(Folder);
        File.WriteAllText(FilePath, JsonSerializer.Serialize(reminders, new JsonSerializerOptions { WriteIndented = true }));
    }
}

public sealed class ReminderService : IDisposable
{
    private static readonly PersianCalendar Persian = new();
    private readonly Action<ReminderItem> _notify;
    private readonly DispatcherTimer _timer;

    public ReminderService(Action<ReminderItem> notify)
    {
        _notify = notify;
        _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(30) };
        _timer.Tick += (_, _) => Check();
        _timer.Start();
        Check();
    }

    private void Check()
    {
        var now = DateTime.Now;
        var reminders = ReminderStore.Load();
        var changed = false;
        foreach (var item in reminders)
        {
            var occurrence = LatestOccurrence(item, now);
            if (occurrence == null || occurrence > now || occurrence < now.AddMinutes(-2) || item.LastNotifiedOccurrence == occurrence) continue;
            item.LastNotifiedOccurrence = occurrence;
            _notify(item);
            changed = true;
        }
        if (changed) ReminderStore.Save(reminders);
    }

    public static DateTime? LatestOccurrence(ReminderItem item, DateTime now)
    {
        if (item.StartsAt > now) return null;
        if (item.Repeat == ReminderRepeat.None) return item.StartsAt;
        if (item.Repeat == ReminderRepeat.Daily) return now.Date.Add(item.StartsAt.TimeOfDay);

        if (item.Calendar == ReminderCalendar.Gregorian)
        {
            if (item.Repeat == ReminderRepeat.Monthly)
                return new DateTime(now.Year, now.Month, Math.Min(item.StartsAt.Day, DateTime.DaysInMonth(now.Year, now.Month))).Add(item.StartsAt.TimeOfDay);
            return new DateTime(now.Year, item.StartsAt.Month, Math.Min(item.StartsAt.Day, DateTime.DaysInMonth(now.Year, item.StartsAt.Month))).Add(item.StartsAt.TimeOfDay);
        }

        var year = Persian.GetYear(now);
        var month = item.Repeat == ReminderRepeat.Yearly ? Persian.GetMonth(item.StartsAt) : Persian.GetMonth(now);
        var day = Math.Min(Persian.GetDayOfMonth(item.StartsAt), Persian.GetDaysInMonth(year, month));
        return Persian.ToDateTime(year, month, day, item.StartsAt.Hour, item.StartsAt.Minute, 0, 0);
    }

    public void Dispose() => _timer.Stop();
}
