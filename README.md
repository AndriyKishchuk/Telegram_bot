# Telegram Planner Bot

Simple Telegram bot for task planning, reminders, and task completion tracking.

## Features

- Add tasks with a deadline
- View all tasks with IDs and statuses
- Delete tasks by ID
- Mark tasks as completed
- Receive reminders depending on how close the deadline is

## Commands

- `/start` - start the bot and show the main menu
- `/help` - show available commands
- `/today` - show all tasks
- `/add` - add a new task
- `/delete` - delete a task by ID
- `/done` - mark a task as completed
- `/exit` - cancel the current action

## Task Format

When adding a task, use one of these formats:

```text
Task name; 2026-04-10 14:30
Task name; 10.04.2026 14:30
```

The date must be in the future.

## Reminder Logic

The bot sends reminders automatically based on the deadline:

- More than 7 days left: once per week
- 7 days or less left: once per day
- 24 hours or less left: every 30 minutes
- After the deadline passes: every 30 minutes until the task is marked as completed

## Setup

1. Install `.NET 8 SDK`
2. Set your bot token in the `TELEGRAM_BOT_TOKEN` environment variable
3. Build and run the project

### PowerShell Example

```powershell
$env:TELEGRAM_BOT_TOKEN="your_bot_token_here"
dotnet build Telegram_bot.csproj -p:UseAppHost=false -o build_verify --ignore-failed-sources
dotnet .\build_verify\Telegram_bot.dll
```

## Data Storage

- Tasks are stored in a local SQLite database file named `planner.db`
- The database is created automatically on startup

## Notes

- A task stops generating reminders only after it is marked as completed with `/done`
- Deleted tasks are removed permanently
- The bot checks reminder conditions every minute
