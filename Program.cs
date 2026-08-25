using System.IO;
using Telegram.Bot;
using Telegram.Bot.Polling;
using Telegram.Bot.Types.Enums;
using TelegramBudgetBot.Handlers;
using TelegramBudgetBot.Services;

var token = Environment.GetEnvironmentVariable("TELEGRAM_BOT_TOKEN")
    ?? throw new InvalidOperationException("Установи переменную TELEGRAM_BOT_TOKEN");

var dbPath = Path.Combine(AppContext.BaseDirectory, "budget_bot.db");
var db = new BotDatabase(dbPath);
await db.InitAsync();

var botClient = new TelegramBotClient(token);
var handler = new BotUpdateHandler(db);

Console.WriteLine("Бюджет+ Бот запущен...");

using var cts = new CancellationTokenSource();

var receiverOptions = new ReceiverOptions
{
    AllowedUpdates = [UpdateType.Message]
};

botClient.StartReceiving(
    new DefaultUpdateHandler(
        (bot, update, ct) => handler.HandleUpdateAsync(bot, update, ct),
        (bot, ex, source, ct) =>
        {
            Console.WriteLine($"Ошибка: {ex.Message}");
            return Task.CompletedTask;
        }
    ),
    receiverOptions,
    cancellationToken: cts.Token
);

var me = await botClient.GetMe(cancellationToken: cts.Token);
Console.WriteLine($"Бот @{me.Username} запущен. Нажми Ctrl+C для остановки.");

Console.CancelKeyPress += (_, e) =>
{
    e.Cancel = true;
    cts.Cancel();
};

try
{
    await Task.Delay(Timeout.Infinite, cts.Token);
}
catch (OperationCanceledException)
{
    Console.WriteLine("Бот остановлен.");
}
