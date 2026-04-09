using System.Globalization;
using Telegram.Bot;
using Telegram.Bot.Polling;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;
using Telegram_bot.Data;
using Telegram_bot.ReminderService;
using Telegram_bot.Services;

internal static class Program
{
    private const string AddTaskPrompt = "Please enter a task in format:\nTask name; YYYY-MM-DD HH:mm";
    private const string DeleteTaskPrompt = "Enter the ID of the task you want to delete.";
    private const string CompleteTaskPrompt = "Enter the ID of the task you completed.";

    private static readonly ReplyKeyboardMarkup MainMenu = new(new[]
    {
        new KeyboardButton[] { "Tasks", "Add Task" },
        new KeyboardButton[] { "Delete Task", "Complete Task" },
        new KeyboardButton[] { "Exit" }
    })
    {
        ResizeKeyboard = true
    };

    private static ITelegramBotClient? _bot;

    private static async Task Main()
    {
        DataBase.Initialize();

        var token = Environment.GetEnvironmentVariable("TELEGRAM_BOT_TOKEN");
        if (string.IsNullOrWhiteSpace(token))
        {
            Console.WriteLine("Set the TELEGRAM_BOT_TOKEN environment variable before starting the bot.");
            return;
        }

        _bot = new TelegramBotClient(token);
        var me = await _bot.GetMe();

        Console.OutputEncoding = System.Text.Encoding.UTF8;
        Console.WriteLine($"Bot @{me.Username} is started.");

        ReminderScheduler.Start(_bot);

        using var cts = new CancellationTokenSource();
        var receiverOptions = new ReceiverOptions
        {
            AllowedUpdates = Array.Empty<UpdateType>()
        };

        _bot.StartReceiving(
            UpdateHandler,
            ErrorHandler,
            receiverOptions,
            cts.Token);

        Console.WriteLine("Press Enter to stop the bot.");
        Console.ReadLine();

        cts.Cancel();
        ReminderScheduler.Stop();
    }

    private static async Task UpdateHandler(ITelegramBotClient bot, Update update, CancellationToken token)
    {
        if (update.Type != UpdateType.Message || update.Message?.Text is null)
        {
            return;
        }

        var message = update.Message;
        var userId = message.Chat.Id;
        var text = message.Text.Trim();
        var firstName = message.From?.FirstName ?? "User";
        var lastName = message.From?.LastName ?? string.Empty;
        var username = message.From?.Username ?? "unknown";

        Console.WriteLine($"{firstName} {lastName} (@{username}), user ID: {userId}.");

        switch (text)
        {
            case "/start":
                Service.ClearPendingAction(userId);
                await bot.SendMessage(
                    chatId: userId,
                    text: "Welcome! I am your personal planner bot.\nUse /help or the buttons below to manage tasks.",
                    replyMarkup: MainMenu,
                    cancellationToken: token);
                return;

            case "/help":
                await bot.SendMessage(
                    chatId: userId,
                    text: "Commands:\n" +
                          "- /today - view your tasks\n" +
                          "- /add - add a new task\n" +
                          "- /delete - delete one of your tasks by ID\n" +
                          "- /done - mark a task as completed\n" +
                          "- /exit - reset the current action",
                    replyMarkup: MainMenu,
                    cancellationToken: token);
                return;

            case "Tasks":
            case "/today":
                await bot.SendMessage(
                    chatId: userId,
                    text: Service.GetUserTasks(userId),
                    replyMarkup: MainMenu,
                    cancellationToken: token);
                return;

            case "Add Task":
            case "/add":
                Service.SetPendingAction(userId, UserAction.AddTask);
                await bot.SendMessage(
                    chatId: userId,
                    text: AddTaskPrompt,
                    replyMarkup: MainMenu,
                    cancellationToken: token);
                return;

            case "Delete Task":
            case "/delete":
                Service.SetPendingAction(userId, UserAction.DeleteTask);
                await bot.SendMessage(
                    chatId: userId,
                    text: $"{DeleteTaskPrompt}\n\n{Service.GetActiveTasks(userId)}",
                    replyMarkup: MainMenu,
                    cancellationToken: token);
                return;

            case "Complete Task":
            case "/done":
                Service.SetPendingAction(userId, UserAction.CompleteTask);
                await bot.SendMessage(
                    chatId: userId,
                    text: $"{CompleteTaskPrompt}\n\n{Service.GetActiveTasks(userId)}",
                    replyMarkup: MainMenu,
                    cancellationToken: token);
                return;

            case "Exit":
            case "/exit":
                Service.ClearPendingAction(userId);
                await bot.SendMessage(
                    chatId: userId,
                    text: "Current action cancelled. You can continue using the planner.",
                    replyMarkup: MainMenu,
                    cancellationToken: token);
                return;
        }

        var pendingAction = Service.GetPendingAction(userId);

        if (pendingAction == UserAction.AddTask)
        {
            if (TryParseTaskInput(text, out var title, out var date))
            {
                Service.AddNewTask(userId, title, date);
                Service.ClearPendingAction(userId);

                await bot.SendMessage(
                    chatId: userId,
                    text: $"Task added: {title} at {date:yyyy-MM-dd HH:mm}",
                    replyMarkup: MainMenu,
                    cancellationToken: token);
                return;
            }

            await bot.SendMessage(
                chatId: userId,
                text: $"Invalid format.\n\n{AddTaskPrompt}",
                replyMarkup: MainMenu,
                cancellationToken: token);
            return;
        }

        if (pendingAction == UserAction.DeleteTask)
        {
            if (int.TryParse(text, out var taskId))
            {
                var deleted = Service.DeleteTask(userId, taskId);
                Service.ClearPendingAction(userId);

                await bot.SendMessage(
                    chatId: userId,
                    text: deleted
                        ? $"Task with ID {taskId} has been deleted."
                        : $"Task with ID {taskId} was not found in your list.",
                    replyMarkup: MainMenu,
                    cancellationToken: token);
                return;
            }

            await bot.SendMessage(
                chatId: userId,
                text: "Please enter a numeric task ID.",
                replyMarkup: MainMenu,
                cancellationToken: token);
            return;
        }

        if (pendingAction == UserAction.CompleteTask)
        {
            if (int.TryParse(text, out var taskId))
            {
                var completed = Service.CompleteTask(userId, taskId);
                Service.ClearPendingAction(userId);

                await bot.SendMessage(
                    chatId: userId,
                    text: completed
                        ? $"Task with ID {taskId} marked as completed."
                        : $"Task with ID {taskId} was not found or is already completed.",
                    replyMarkup: MainMenu,
                    cancellationToken: token);
                return;
            }

            await bot.SendMessage(
                chatId: userId,
                text: "Please enter a numeric task ID.",
                replyMarkup: MainMenu,
                cancellationToken: token);
            return;
        }

        await bot.SendMessage(
            chatId: userId,
            text: "Unknown command. Use /help or the buttons below.",
            replyMarkup: MainMenu,
            cancellationToken: token);
    }

    private static Task ErrorHandler(ITelegramBotClient bot, Exception ex, CancellationToken token)
    {
        Console.WriteLine($"Error: {ex.Message}");
        return Task.CompletedTask;
    }

    private static bool TryParseTaskInput(string text, out string title, out DateTime date)
    {
        title = string.Empty;
        date = default;

        var parts = text.Split(';', 2, StringSplitOptions.TrimEntries);
        if (parts.Length != 2 || string.IsNullOrWhiteSpace(parts[0]))
        {
            return false;
        }

        var formats = new[]
        {
            "yyyy-MM-dd HH:mm",
            "yyyy-MM-dd H:mm",
            "dd.MM.yyyy HH:mm",
            "dd.MM.yyyy H:mm"
        };

        if (!DateTime.TryParseExact(parts[1], formats, CultureInfo.InvariantCulture, DateTimeStyles.None, out date) &&
            !DateTime.TryParse(parts[1], CultureInfo.CurrentCulture, DateTimeStyles.None, out date))
        {
            return false;
        }

        if (date <= DateTime.Now)
        {
            return false;
        }

        title = parts[0].Trim();
        return true;
    }
}
