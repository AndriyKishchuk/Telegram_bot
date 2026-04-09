using Telegram_bot.Data;
using Telegram_bot.Models;

namespace Telegram_bot.Services;

internal static class Service
{
    private static readonly Dictionary<long, UserAction> PendingActions = new();

    public static void AddNewTask(long userId, string title, DateTime date)
    {
        var task = new TaskItem
        {
            UserId = userId,
            Title = title.Trim(),
            Date = date,
            IsDone = false
        };

        DataBase.AddTask(task);
    }

    public static string GetUserTasks(long userId)
    {
        var tasks = DataBase.GetTasks(userId);
        if (tasks.Count == 0)
        {
            return "You do not have any tasks yet.";
        }

        var lines = tasks
            .OrderBy(task => task.Date)
            .Select(task =>
            {
                var status = task.IsDone ? "[done]" : "[active]";
                return $"#{task.Id} | {task.Date:yyyy-MM-dd HH:mm} | {status} | {task.Title}";
            });

        return "Your tasks:\n" + string.Join('\n', lines);
    }

    public static string GetActiveTasks(long userId)
    {
        var tasks = DataBase.GetTasks(userId)
            .Where(task => !task.IsDone)
            .OrderBy(task => task.Date)
            .ToList();

        if (tasks.Count == 0)
        {
            return "You do not have any active tasks.";
        }

        var lines = tasks.Select(task => $"#{task.Id} | {task.Date:yyyy-MM-dd HH:mm} | {task.Title}");
        return "Active tasks:\n" + string.Join('\n', lines);
    }

    public static void SetPendingAction(long userId, UserAction action)
    {
        PendingActions[userId] = action;
    }

    public static UserAction GetPendingAction(long userId)
    {
        return PendingActions.TryGetValue(userId, out var action) ? action : UserAction.None;
    }

    public static void ClearPendingAction(long userId)
    {
        PendingActions.Remove(userId);
    }

    public static bool DeleteTask(long userId, int taskId)
    {
        return DataBase.DeleteTask(userId, taskId);
    }

    public static bool CompleteTask(long userId, int taskId)
    {
        return DataBase.MarkTaskAsDone(userId, taskId);
    }

    public static List<TaskItem> GetIncompleteTasks()
    {
        return DataBase.GetAllTasks()
            .Where(task => !task.IsDone)
            .OrderBy(task => task.Date)
            .ToList();
    }
}
