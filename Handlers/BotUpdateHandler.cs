using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;
using TelegramBudgetBot.Models;
using TelegramBudgetBot.Services;

namespace TelegramBudgetBot.Handlers;

public class BotUpdateHandler
{
    private readonly BotDatabase _db;
    private const int FreeLimit = 20;

    private static readonly string[] DefaultCategories =
        ["еда", "транспорт", "жильё", "одежда", "здоровье", "развлечения", "связь", "образование", "прочее"];

    public BotUpdateHandler(BotDatabase db)
    {
        _db = db;
    }

    public async Task HandleUpdateAsync(ITelegramBotClient bot, Update update, CancellationToken ct)
    {
        if (update.Message is not { Text: { } text } message) return;

        var chatId = message.Chat.Id;
        var userId = message.From?.Id ?? 0;
        var args = text.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var command = args[0].ToLower().Replace($"@{bot.GetMeAsync(ct).Result.Username}", "");

        try
        {
            switch (command)
            {
                case "/start":
                    await SendWelcome(bot, chatId, ct);
                    break;
                case "/help":
                    await SendHelp(bot, chatId, ct);
                    break;
                case "/add":
                    await HandleAdd(bot, chatId, userId, args, "expense", ct);
                    break;
                case "/income":
                    await HandleAdd(bot, chatId, userId, args, "income", ct);
                    break;
                case "/balance":
                    await HandleBalance(bot, chatId, userId, ct);
                    break;
                case "/report":
                    await HandleReport(bot, chatId, userId, ct);
                    break;
                case "/limit":
                    await HandleLimit(bot, chatId, userId, args, ct);
                    break;
                case "/categories":
                    await HandleCategories(bot, chatId, userId, ct);
                    break;
                default:
                    await bot.SendMessage(chatId, "Неизвестная команда. Напиши /help", cancellationToken: ct);
                    break;
            }
        }
        catch (Exception ex)
        {
            await bot.SendMessage(chatId, $"Ошибка: {ex.Message}", cancellationToken: ct);
        }
    }

    private async Task SendWelcome(ITelegramBotClient bot, long chatId, CancellationToken ct)
    {
        var text = """
            👋 Привет! Я — Бюджет+ Бот.

            Помогаю вести личный или семейный бюджет прямо в Telegram.

            💰 Добавление расхода:
            /add 500 еда
            /add 200 транспорт обед на вынос

            💵 Добавление дохода:
            /income 50000 зарплата

            📊 Команды:
            /balance — баланс за месяц
            /report — отчёт по категориям
            /limit еда 10000 — лимит на категорию
            /categories — список категорий
            /help — помощь

            Начни с /add или /income!
            """;
        await bot.SendMessage(chatId, text, cancellationToken: ct);
    }

    private async Task SendHelp(ITelegramBotClient bot, long chatId, CancellationToken ct)
    {
        var text = """
            📖 Помощь

            /add [сумма] [категория] [описание] — добавить расход
            /income [сумма] [описание] — добавить доход
            /balance — баланс текущего месяца
            /report — расходы по категориям
            /limit [категория] [сумма] — установить лимит
            /categories — все категории

            Примеры:
            /add 350 еда обед в столовой
            /add 1500 транспорт
            /income 50000 зарплата
            /limit еда 15000
            """;
        await bot.SendMessage(chatId, text, cancellationToken: ct);
    }

    private async Task HandleAdd(ITelegramBotClient bot, long chatId, long userId, string[] args, string type, CancellationToken ct)
    {
        var count = await _db.GetMonthCountAsync(userId, DateTime.UtcNow);
        if (count >= FreeLimit && type == "expense")
        {
            var keyboard = new InlineKeyboardMarkup(new[]
            {
                new[] { InlineKeyboardButton.WithUrl("🔓 Снять лимит — 99₽", "https://t.me/your_payment_link") }
            });
            await bot.SendMessage(chatId,
                $"⚠ Бесплатный лимит: {FreeLimit} операций в месяц.\n\n" +
                $"Осталось: {FreeLimit - count} из {FreeLimit}\n" +
                $"Сними лимит или подожди до следующего месяца.",
                replyMarkup: keyboard, cancellationToken: ct);
            return;
        }

        if (args.Length < 3)
        {
            var example = type == "income"
                ? "/income 50000 зарплата"
                : "/add 500 еда обед";
            await bot.SendMessage(chatId,
                $"Формат: /{(type == "income" ? "income" : "add")} [сумма] [категория] [описание]\n" +
                $"Пример: {example}",
                cancellationToken: ct);
            return;
        }

        if (!decimal.TryParse(args[1], out var amount) || amount <= 0)
        {
            await bot.SendMessage(chatId, "Сумма должна быть числом больше 0", cancellationToken: ct);
            return;
        }

        var category = args[2].ToLower();
        var description = args.Length > 3 ? string.Join(' ', args.Skip(3)) : null;

        var tx = new Transaction
        {
            UserId = userId,
            Amount = amount,
            Category = category,
            Type = type,
            Description = description,
            CreatedAt = DateTime.UtcNow
        };

        await _db.AddTransactionAsync(tx);

        var emoji = type == "income" ? "💵" : "💸";
        var label = type == "income" ? "Доход" : "Расход";
        await bot.SendMessage(chatId,
            $"{emoji} {label} добавлен: {amount:N0}₽ — {category}",
            cancellationToken: ct);

        if (type == "expense")
        {
            var limit = await _db.GetLimitAsync(userId, category);
            if (limit != null)
            {
                var spending = await _db.GetCategorySpendingAsync(userId, DateTime.UtcNow);
                var total = spending.GetValueOrDefault(category, 0);
                var pct = (int)(total / limit.LimitAmount * 100);

                string warning = pct switch
                {
                    >= 100 => $"🔴 Превышен лимит! {total:N0} из {limit.LimitAmount:N0}₽ ({pct}%)",
                    >= 80 => $"🟡 Приближаешься к лимиту: {total:N0} из {limit.LimitAmount:N0}₽ ({pct}%)",
                    _ => $"🟢 В пределах лимита: {total:N0} из {limit.LimitAmount:N0}₽ ({pct}%)"
                };
                await bot.SendMessage(chatId, warning, cancellationToken: ct);
            }
        }
    }

    private async Task HandleBalance(ITelegramBotClient bot, long chatId, long userId, CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        var balance = await _db.GetBalanceAsync(userId, now);
        var txs = await _db.GetMonthTransactionsAsync(userId, now);

        var income = txs.Where(t => t.Type == "income").Sum(t => t.Amount);
        var expense = txs.Where(t => t.Type == "expense").Sum(t => t.Amount);

        var emoji = balance >= 0 ? "✅" : "⚠️";

        var text = $"""
            📊 Баланс за {now:MMMM yyyy}:

            💵 Доходы:  {income:N0}₽
            💸 Расходы: {expense:N0}₽
            {emoji} Итого:    {balance:N0}₽

            Операций: {txs.Count}
            """;

        await bot.SendMessage(chatId, text, cancellationToken: ct);
    }

    private async Task HandleReport(ITelegramBotClient bot, long chatId, long userId, CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        var spending = await _db.GetCategorySpendingAsync(userId, now);

        if (spending.Count == 0)
        {
            await bot.SendMessage(chatId, "📊 Пока нет расходов за этот месяц.", cancellationToken: ct);
            return;
        }

        var total = spending.Values.Sum();
        var sorted = spending.OrderByDescending(kv => kv.Value).ToList();

        var lines = new List<string> { $"📊 Расходы за {now:MMMM yyyy}:\n" };

        foreach (var (cat, amount) in sorted)
        {
            var bar = new string('█', Math.Max(1, (int)(amount / total * 15)));
            var pct = (int)(amount / total * 100);
            lines.Add($"{cat}: {amount:N0}₽ ({pct}%) {bar}");
        }

        lines.Add($"\nИтого: {total:N0}₽");
        await bot.SendMessage(chatId, string.Join('\n', lines), cancellationToken: ct);
    }

    private async Task HandleLimit(ITelegramBotClient bot, long chatId, long userId, string[] args, CancellationToken ct)
    {
        if (args.Length < 3)
        {
            var limits = await _db.GetLimitsAsync(userId);
            if (limits.Count == 0)
            {
                await bot.SendMessage(chatId,
                    "Формат: /limit [категория] [сумма]\nПример: /limit еда 15000",
                    cancellationToken: ct);
                return;
            }

            var lines = limits.Select(l => $"• {l.Category}: {l.LimitAmount:N0}₽").ToList();
            await bot.SendMessage(chatId, "📋 Лимиты:\n" + string.Join('\n', lines), cancellationToken: ct);
            return;
        }

        var category = args[1].ToLower();
        if (!decimal.TryParse(args[2], out var limitAmount) || limitAmount <= 0)
        {
            await bot.SendMessage(chatId, "Сумма должна быть числом больше 0", cancellationToken: ct);
            return;
        }

        await _db.UpsertLimitAsync(userId, category, limitAmount);
        await bot.SendMessage(chatId,
            $"✅ Лимит для «{category}» установлен: {limitAmount:N0}₽",
            cancellationToken: ct);
    }

    private async Task HandleCategories(ITelegramBotClient bot, long chatId, long userId, CancellationToken ct)
    {
        var text = "📂 Категории:\n\n" +
                   string.Join('\n', DefaultCategories.Select((c, i) => $"{i + 1}. {c}")) +
                   "\n\nМожно добавить свою: /add 500 моя_категория";
        await bot.SendMessage(chatId, text, cancellationToken: ct);
    }
}
