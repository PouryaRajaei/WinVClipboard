using System.Globalization;
using System.IO;
using System.Text.Json;
using System.Windows.Threading;

namespace WinVClipboard;

public enum ReminderRepeat { None = 0, Daily = 1, Monthly = 2, Yearly = 3, Weekly = 4 }
public enum ReminderCalendar { Persian, Gregorian }

public sealed class ReminderItem
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Title { get; set; } = "";
    public DateTime StartsAt { get; set; }
    public ReminderRepeat Repeat { get; set; }
    public ReminderCalendar Calendar { get; set; } = ReminderCalendar.Persian;
    public int? MaxOccurrences { get; set; }
    public List<string> CompletedOccurrences { get; set; } = [];
    public DateTime? LastNotifiedOccurrence { get; set; }
}

public static class ReminderStore
{
    private static readonly string Folder = Path.Combine(Environment.GetFolderPath(
        Environment.SpecialFolder.LocalApplicationData), "WinVClipboard");
    public static readonly string FilePath = Path.Combine(Folder, "reminders.json");
    public static readonly string StateFilePath = Path.Combine(Folder, "reminder-state.json");

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

public sealed class ReminderRuntimeState
{
    public DateTime LastCheckedAt { get; set; }
}

public sealed class ReminderService : IDisposable
{
    private static readonly PersianCalendar Persian = new();
    private readonly Action<ReminderItem, bool> _notify;
    private readonly DispatcherTimer _timer;
    private DateTime _lastCheckedAt;

    public ReminderService(Action<ReminderItem, bool> notify)
    {
        _notify = notify;
        _lastCheckedAt = LoadLastCheckedAt();
        _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(30) };
        _timer.Tick += (_, _) => Check();
        _timer.Start();
        Check();
    }

    private void Check()
    {
        var now = DateTime.Now;
        if (_lastCheckedAt > now) _lastCheckedAt = now.AddMinutes(-2);
        var reminders = ReminderStore.Load();
        var changed = false;
        foreach (var item in reminders)
        {
            var occurrence = LatestOccurrence(item, now);
            if (occurrence == null || occurrence > now || occurrence <= _lastCheckedAt || item.LastNotifiedOccurrence == occurrence) continue;
            item.LastNotifiedOccurrence = occurrence;
            _notify(item, occurrence < now.AddMinutes(-2));
            changed = true;
        }
        if (changed) ReminderStore.Save(reminders);
        _lastCheckedAt = now;
        SaveLastCheckedAt(now);
    }

    public static DateTime? LatestOccurrence(ReminderItem item, DateTime now)
    {
        if (item.StartsAt > now) return null;
        if (item.Repeat == ReminderRepeat.None) return item.StartsAt;
        if (item.Repeat == ReminderRepeat.Daily)
        {
            var daily = now.Date.Add(item.StartsAt.TimeOfDay);
            if (daily > now) daily = daily.AddDays(-1);
            return IsOccurrenceAllowed(item, daily) ? daily : null;
        }
        if (item.Repeat == ReminderRepeat.Weekly)
        {
            var weeks = (now.Date - item.StartsAt.Date).Days / 7;
            var weekly = item.StartsAt.AddDays(weeks * 7);
            if (weekly > now) weekly = weekly.AddDays(-7);
            return IsOccurrenceAllowed(item, weekly) ? weekly : null;
        }

        if (item.Calendar == ReminderCalendar.Gregorian)
        {
            if (item.Repeat == ReminderRepeat.Monthly)
            {
                var monthly = new DateTime(now.Year, now.Month, Math.Min(item.StartsAt.Day, DateTime.DaysInMonth(now.Year, now.Month))).Add(item.StartsAt.TimeOfDay);
                if (monthly > now)
                {
                    var previous = now.AddMonths(-1);
                    monthly = new DateTime(previous.Year, previous.Month, Math.Min(item.StartsAt.Day, DateTime.DaysInMonth(previous.Year, previous.Month))).Add(item.StartsAt.TimeOfDay);
                }
                return IsOccurrenceAllowed(item, monthly) ? monthly : null;
            }
            var yearly = new DateTime(now.Year, item.StartsAt.Month, Math.Min(item.StartsAt.Day, DateTime.DaysInMonth(now.Year, item.StartsAt.Month))).Add(item.StartsAt.TimeOfDay);
            if (yearly > now)
            {
                var previousYear = now.Year - 1;
                yearly = new DateTime(previousYear, item.StartsAt.Month, Math.Min(item.StartsAt.Day, DateTime.DaysInMonth(previousYear, item.StartsAt.Month))).Add(item.StartsAt.TimeOfDay);
            }
            return IsOccurrenceAllowed(item, yearly) ? yearly : null;
        }

        var year = Persian.GetYear(now);
        var month = item.Repeat == ReminderRepeat.Yearly ? Persian.GetMonth(item.StartsAt) : Persian.GetMonth(now);
        var day = Math.Min(Persian.GetDayOfMonth(item.StartsAt), Persian.GetDaysInMonth(year, month));
        var occurrence = Persian.ToDateTime(year, month, day, item.StartsAt.Hour, item.StartsAt.Minute, 0, 0);
        if (occurrence > now)
        {
            if (item.Repeat == ReminderRepeat.Monthly)
            {
                month--;
                if (month == 0) { month = 12; year--; }
            }
            else year--;
            day = Math.Min(Persian.GetDayOfMonth(item.StartsAt), Persian.GetDaysInMonth(year, month));
            occurrence = Persian.ToDateTime(year, month, day, item.StartsAt.Hour, item.StartsAt.Minute, 0, 0);
        }
        return IsOccurrenceAllowed(item, occurrence) ? occurrence : null;
    }

    private static DateTime LoadLastCheckedAt()
    {
        try
        {
            if (!File.Exists(ReminderStore.StateFilePath)) return DateTime.Now.AddMinutes(-2);
            return JsonSerializer.Deserialize<ReminderRuntimeState>(File.ReadAllText(ReminderStore.StateFilePath))?.LastCheckedAt
                ?? DateTime.Now.AddMinutes(-2);
        }
        catch { return DateTime.Now.AddMinutes(-2); }
    }

    private static void SaveLastCheckedAt(DateTime value)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(ReminderStore.StateFilePath)!);
            File.WriteAllText(ReminderStore.StateFilePath, JsonSerializer.Serialize(new ReminderRuntimeState { LastCheckedAt = value }));
        }
        catch { }
    }

    public static bool IsOccurrenceAllowed(ReminderItem item, DateTime occurrence)
    {
        if (occurrence.Date < item.StartsAt.Date) return false;
        if (item.MaxOccurrences is not > 0) return true;
        var index = item.Repeat switch
        {
            ReminderRepeat.None => 0,
            ReminderRepeat.Daily => (occurrence.Date - item.StartsAt.Date).Days,
            ReminderRepeat.Weekly => (occurrence.Date - item.StartsAt.Date).Days / 7,
            ReminderRepeat.Monthly when item.Calendar == ReminderCalendar.Gregorian =>
                (occurrence.Year - item.StartsAt.Year) * 12 + occurrence.Month - item.StartsAt.Month,
            ReminderRepeat.Yearly when item.Calendar == ReminderCalendar.Gregorian =>
                occurrence.Year - item.StartsAt.Year,
            ReminderRepeat.Monthly =>
                (Persian.GetYear(occurrence) - Persian.GetYear(item.StartsAt)) * 12 +
                Persian.GetMonth(occurrence) - Persian.GetMonth(item.StartsAt),
            ReminderRepeat.Yearly => Persian.GetYear(occurrence) - Persian.GetYear(item.StartsAt),
            _ => 0
        };
        return index >= 0 && index < item.MaxOccurrences.Value;
    }

    public void Dispose() => _timer.Stop();
}
