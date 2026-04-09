using Telegram.Bot;
using Telegram_bot.Models;
using Telegram_bot.Services;

namespace Telegram_bot.ReminderService;

internal static class ReminderScheduler
{
    private static readonly TimeSpan CheckInterval = TimeSpan.FromMinutes(1);
    private static readonly object SyncRoot = new();
    private static readonly Dictionary<int, ReminderState> ReminderStates = new();

    private static Timer? _timer;
    private static ITelegramBotClient? _bot;
    private static bool _isRunning;

    public static void Start(ITelegramBotClient bot)
    {
        _bot = bot ?? throw new ArgumentNullException(nameof(bot));
        _timer = new Timer(async _ => await TickAsync(), null, TimeSpan.Zero, CheckInterval);
    }

    public static void Stop()
    {
        _timer?.Dispose();
        _timer = null;
    }

    private static async Task TickAsync()
    {
        if (_bot is null)
        {
            return;
        }

        lock (SyncRoot)
        {
            if (_isRunning)
            {
                return;
            }

            _isRunning = true;
        }

        try
        {
            var now = DateTime.Now;
            var tasks = Service.GetIncompleteTasks();
            CleanupState(tasks.Select(task => task.Id).ToHashSet());

            foreach (var task in tasks)
            {
                var plan = BuildReminderPlan(task, now);
                if (!plan.ShouldSend)
                {
                    continue;
                }

                if (!TryReserveReminder(task.Id, plan.Stage, plan.Key, now))
                {
                    continue;
                }

                await _bot.SendMessage(task.UserId, plan.Message);
                Console.WriteLine($"Reminder sent for task {task.Id} to user {task.UserId}.");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Reminder error: {ex.Message}");
        }
        finally
        {
            lock (SyncRoot)
            {
                _isRunning = false;
            }
        }
    }

    private static ReminderPlan BuildReminderPlan(TaskItem task, DateTime now)
    {
        var timeLeft = task.Date - now;

        if (timeLeft <= TimeSpan.Zero)
        {
            var key = $"overdue-{task.Date:yyyyMMddHHmm}-{now:yyyyMMddHH}-{now.Minute / 30}";
            return new ReminderPlan(
                true,
                ReminderStage.OverdueHalfHourly,
                key,
                $"Task #{task.Id} is overdue since {task.Date:yyyy-MM-dd HH:mm}: {task.Title}\nMark it as completed when you finish it.");
        }

        if (timeLeft <= TimeSpan.FromDays(1))
        {
            var key = $"{now:yyyyMMddHH}-{now.Minute / 30}";
            return new ReminderPlan(
                true,
                ReminderStage.HalfHourly,
                key,
                $"Half-hour reminder for task #{task.Id}.\nDue at {task.Date:yyyy-MM-dd HH:mm}: {task.Title}");
        }

        if (timeLeft <= TimeSpan.FromDays(7))
        {
            var key = now.ToString("yyyyMMdd");
            return new ReminderPlan(
                IsInsideDailyWindow(task, now),
                ReminderStage.Daily,
                key,
                $"Daily reminder for task #{task.Id}.\nDue on {task.Date:yyyy-MM-dd HH:mm}: {task.Title}");
        }

        var weeklyKey = GetWeeklyKey(now);
        return new ReminderPlan(
            IsInsideWeeklyWindow(task, now),
            ReminderStage.Weekly,
            weeklyKey,
            $"Weekly reminder for task #{task.Id}.\nDue on {task.Date:yyyy-MM-dd HH:mm}: {task.Title}");
    }

    private static bool IsInsideDailyWindow(TaskItem task, DateTime now)
    {
        return now.Hour == task.Date.Hour && now.Minute < CheckInterval.TotalMinutes;
    }

    private static bool IsInsideWeeklyWindow(TaskItem task, DateTime now)
    {
        return now.DayOfWeek == task.Date.DayOfWeek &&
               now.Hour == task.Date.Hour &&
               now.Minute < CheckInterval.TotalMinutes;
    }

    private static string GetWeeklyKey(DateTime now)
    {
        var startOfYear = new DateTime(now.Year, 1, 1);
        var week = ((now.DayOfYear - 1) / 7) + 1;
        return $"{startOfYear.Year}-W{week:D2}";
    }

    private static bool TryReserveReminder(int taskId, ReminderStage stage, string key, DateTime now)
    {
        lock (SyncRoot)
        {
            if (ReminderStates.TryGetValue(taskId, out var state) &&
                state.Stage == stage &&
                state.Key == key)
            {
                return false;
            }

            ReminderStates[taskId] = new ReminderState(stage, key, now);
            return true;
        }
    }

    private static void CleanupState(HashSet<int> activeTaskIds)
    {
        lock (SyncRoot)
        {
            var staleTaskIds = ReminderStates.Keys
                .Where(taskId => !activeTaskIds.Contains(taskId))
                .ToList();

            foreach (var taskId in staleTaskIds)
            {
                ReminderStates.Remove(taskId);
            }
        }
    }

    private sealed record ReminderState(ReminderStage Stage, string Key, DateTime SentAt);

    private sealed record ReminderPlan(bool ShouldSend, ReminderStage Stage, string Key, string Message);

    private enum ReminderStage
    {
        Weekly = 1,
        Daily = 2,
        HalfHourly = 3,
        OverdueHalfHourly = 4
    }
}
