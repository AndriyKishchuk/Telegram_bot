namespace Telegram_bot.Models;

internal sealed class TaskItem
{
    public int Id { get; set; }

    public long UserId { get; set; }

    public string Title { get; set; } = string.Empty;

    public DateTime Date { get; set; }

    public bool IsDone { get; set; }
}
