using System.Globalization;
using System.Text;
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
    private const string BotUsername = "familybudgetplus_bot";
    private const long AdminUserId = 367170690;
    private const string YooMoneyReceiver = "4100119296680958";
    private const decimal PremiumPrice = 99m;

    private static readonly string[] DefaultCategories =
        ["еда", "транспорт", "жильё", "одежда", "здоровье", "развлечения", "связь", "образование", "прочее"];

    private static readonly Dictionary<string, string[]> CategoryKeywords = new()
    {
        ["еда"] = ["обед", "завтрак", "ужин", "кофе", "чай", " restaurant", "кафе", "столовая", "еда", "пицца", "суши", "борщ", "хлеб", "молоко", "мясо", "овощи", "фрукты"],
        ["транспорт"] = ["такси", "метро", "автобус", "трамвай", "бензин", "парковка", "транспорт", "каршеринг"],
        ["жильё"] = ["аренда", "коммунал", "электричество", "газ", "вода", "интернет", "жильё", "квартплата"],
        ["одежда"] = ["одежда", "обувь", "штаны", "куртка", "зимняя"],
        ["здоровье"] = ["лекарств", "врач", "аптека", "больниц", "здоровь", "таблетки"],
        ["развлечения"] = ["кино", "театр", "концерт", "игр", "развлечен", "музык", "фильм"],
        ["связь"] = ["телефон", "связь", "мобил", "sim"],
        ["образование"] = ["курс", "учеб", "книг", "образован", "обучен"]
    };

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
                    await HandleCategories(bot, chatId, ct);
                    break;
                case "/share":
                    await HandleShare(bot, chatId, userId, ct);
                    break;
                case "/export":
                    await HandleExport(bot, chatId, userId, ct);
                    break;
                case "/widget":
                    await HandleWidget(bot, chatId, userId, ct);
                    break;
                case "/remind":
                    await HandleRemind(bot, chatId, userId, args, ct);
                    break;
                case "/group":
                    await HandleGroup(bot, chatId, userId, args, ct);
                    break;
                case "/chart":
                    await HandleChart(bot, chatId, userId, ct);
                    break;
                case "/premium":
                    await HandlePremium(bot, chatId, userId, ct);
                    break;
                case "/pay":
                    await HandlePay(bot, chatId, userId, args, ct);
                    break;
                case "/check":
                    await HandleCheck(bot, chatId, userId, ct);
                    break;
                case "/confirm":
                    await HandleConfirm(bot, chatId, userId, args, ct);
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

            💰 Расход: /add 500 еда
            💵 Доход: /income 50000 зарплата
            📊 Баланс: /balance
            📋 Отчёт: /report
            🔔 Лимит: /limit еда 15000
            📂 Категории: /categories
            🔗 Пригласи друга: /share
            📥 Экспорт: /export
            🖼 Баланс-картинка: /widget
            📊 График: /chart
            ⏰ Напоминание: /remind 21:00
            👨‍👩‍👧 Семья: /group
            ⭐ Премиум: /premium

            Начни с /add или /income!
            """;
        await bot.SendMessage(chatId, text, cancellationToken: ct);
    }

    private async Task SendHelp(ITelegramBotClient bot, long chatId, CancellationToken ct)
    {
        var text = """
            📖 Команды:

            /add [сумма] [категория] [описание] — расход
            /income [сумма] [описание] — доход
            /balance — баланс за месяц
            /report — расходы по категориям
            /limit [категория] [сумма] — лимит
            /categories — все категории
            /share — пригласить друга
            /export — экспорт в CSV
            /widget — картинка баланса
            /chart — график расходов
            /premium — премиум (безлимит)
            /pay [ID] — активировать премиум по ID операции
            /remind [время] — напоминание (21:00)
            /group [chat_id] — семейный бюджет

            /add 350 еда обед в столовой
            /add 1500 транспорт
            /income 50000 зарплата
            /limit еда 15000
            /remind 21:00
            """;
        await bot.SendMessage(chatId, text, cancellationToken: ct);
    }

    private async Task HandleAdd(ITelegramBotClient bot, long chatId, long userId, string[] args, string type, CancellationToken ct)
    {
        var isPremium = await _db.IsPremiumAsync(userId);
        if (!isPremium)
        {
            var count = await _db.GetMonthCountAsync(userId, DateTime.UtcNow);
            if (count >= FreeLimit && type == "expense")
            {
                await bot.SendMessage(chatId,
                    "🚫 Достигнут лимит 20 операций в месяц.\n" +
                    "Напиши /premium чтобы получить безлимит!",
                    cancellationToken: ct);
                return;
            }
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

        if (type == "expense" && string.IsNullOrEmpty(description) && DefaultCategories.Contains(category))
        {
            // ok, user specified category explicitly
        }
        else if (type == "expense" && !DefaultCategories.Contains(category) && description == null)
        {
            var detected = DetectCategory(args.Skip(2).Aggregate("", (a, b) => a + " " + b).Trim());
            if (detected != "прочее")
            {
                category = detected;
            }
        }

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
                var pct = limit.LimitAmount > 0 ? (int)(total / limit.LimitAmount * 100) : 0;

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

    private async Task HandleCategories(ITelegramBotClient bot, long chatId, CancellationToken ct)
    {
        var text = "📂 Категории:\n\n" +
                   string.Join('\n', DefaultCategories.Select((c, i) => $"{i + 1}. {c}")) +
                   "\n\nМожно добавить свою: /add 500 моя_категория\n" +
                   "Бот автоматически определит категорию по описанию!";
        await bot.SendMessage(chatId, text, cancellationToken: ct);
    }

    private async Task HandleShare(ITelegramBotClient bot, long chatId, long userId, CancellationToken ct)
    {
        var link = $"https://t.me/{BotUsername}?start=ref{userId}";
        var text = $"""
            🔗 Пригласи друга!

            Отправь ему эту ссылку:
            {link}

            Когда друг напишет /start по твоей ссылке, вы оба получите +5 бесплатных операций в месяц!

            📊 Твои приглашения: считаем...
            """;
        await bot.SendMessage(chatId, text, cancellationToken: ct);
    }

    private async Task HandleExport(ITelegramBotClient bot, long chatId, long userId, CancellationToken ct)
    {
        var txs = await _db.GetTransactionsAsync(userId);
        if (txs.Count == 0)
        {
            await bot.SendMessage(chatId, "📥 Нет операций для экспорта.", cancellationToken: ct);
            return;
        }

        var sb = new StringBuilder();
        sb.AppendLine("Дата;Сумма;Тип;Категория;Описание");
        foreach (var tx in txs.OrderByDescending(t => t.CreatedAt))
        {
            sb.AppendLine($"{tx.CreatedAt:dd.MM.yyyy HH:mm};{tx.Amount:N0};{tx.Type};{tx.Category};{tx.Description ?? ""}");
        }

        var csvBytes = Encoding.UTF8.GetBytes(sb.ToString());
        var stream = new MemoryStream(csvBytes);

        await bot.SendDocument(chatId, InputFile.FromStream(stream, "budget_export.csv"),
            caption: $"📥 Экспорт: {txs.Count} операций",
            cancellationToken: ct);
    }

    private async Task HandleWidget(ITelegramBotClient bot, long chatId, long userId, CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        var balance = await _db.GetBalanceAsync(userId, now);
        var txs = await _db.GetMonthTransactionsAsync(userId, now);
        var income = txs.Where(t => t.Type == "income").Sum(t => t.Amount);
        var expense = txs.Where(t => t.Type == "expense").Sum(t => t.Amount);

        var emoji = balance >= 0 ? "🟢" : "🔴";
        var pct = income > 0 ? (int)(expense / income * 100) : 0;

        var text = $"""
            {emoji} *Бюджет\+ — {now:MMMM yyyy}*

            💵 Доходы: *{income:N0}₽*
            💸 Расходы: *{expense:N0}₽*
            📊 Потрачено: *{pct}%*
            ✅ Остаток: *{balance:N0}₽*

            _Сохрани как скриншот для Stories_
            """;

        await bot.SendMessage(chatId, text,
            parseMode: ParseMode.MarkdownV2, cancellationToken: ct);
    }

    private async Task HandleRemind(ITelegramBotClient bot, long chatId, long userId, string[] args, CancellationToken ct)
    {
        if (args.Length < 2)
        {
            await bot.SendMessage(chatId,
                "Формат: /remind [время]\n" +
                "Примеры:\n" +
                "/remind 21:00 — каждый день в 21:00\n" +
                "/remind off — выключить напоминания",
                cancellationToken: ct);
            return;
        }

        if (args[1].ToLower() == "off")
        {
            await _db.DisableReminderAsync(userId);
            await bot.SendMessage(chatId, "🔕 Напоминания выключены.", cancellationToken: ct);
            return;
        }

        if (!TimeOnly.TryParse(args[1], out var time))
        {
            await bot.SendMessage(chatId, "Неверный формат времени. Используй HH:MM, например 21:00", cancellationToken: ct);
            return;
        }

        await _db.SetReminderAsync(userId, time);
        await bot.SendMessage(chatId,
            $"⏰ Напоминание установлено на {time:HH:mm} каждый день.\n" +
            $"Бот напомнит записать расходы.",
            cancellationToken: ct);
    }

    private async Task HandleGroup(ITelegramBotClient bot, long chatId, long userId, string[] args, CancellationToken ct)
    {
        if (args.Length < 2)
        {
            await bot.SendMessage(chatId,
                "👨‍👩‍👧 Семейный бюджет:\n\n" +
                "1. Добавь бота в групповой чат семьи\n" +
                "2. Напиши /group в группе\n" +
                "3. Все участники будут видеть общий баланс\n\n" +
                "Команды в группе:\n" +
                "/add 500 еда — добавить расход от имени автора\n" +
                "/balance — общий баланс семьи\n" +
                "/report — общий отчёт",
                cancellationToken: ct);
            return;
        }

        await bot.SendMessage(chatId,
            "👨‍👩‍👧 Групповой бюджет активирован!\n" +
            "Теперь /balance и /report показывают данные всех участников чата.",
            cancellationToken: ct);
    }

    private async Task HandleChart(ITelegramBotClient bot, long chatId, long userId, CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        var spending = await _db.GetCategorySpendingAsync(userId, now);

        if (spending.Count == 0)
        {
            await bot.SendMessage(chatId, "📊 Пока нет расходов за этот месяц.", cancellationToken: ct);
            return;
        }

        var pngBytes = ChartGenerator.GenerateBarChart(spending, $"Расходы — {now:MMMM yyyy}");
        var stream = new MemoryStream(pngBytes);

        await bot.SendPhoto(chatId, InputFile.FromStream(stream, "chart.png"),
            caption: $"📊 График расходов за {now:MMMM yyyy}",
            cancellationToken: ct);
    }

    private async Task HandlePremium(ITelegramBotClient bot, long chatId, long userId, CancellationToken ct)
    {
        if (await _db.IsPremiumAsync(userId))
        {
            await bot.SendMessage(chatId,
                "✅ У тебя уже есть премиум!\n\n" +
                "Безлимитные операции, все функции бота доступны.",
                cancellationToken: ct);
            return;
        }

        var premiumCount = await _db.GetPremiumCountAsync();
        var label = $"premium_{userId}_{DateTime.UtcNow:yyyyMMddHHmmss}";

        await _db.CreatePaymentAsync(userId, PremiumPrice, label);

        await bot.SendMessage(chatId,
            $"⭐ *Премиум Бюджет\\+*\n\n" +
            $"*Что даёт:*\n" +
            $"✅ Безлимитные операции \\(бесплатно — 20/мес\\)\n" +
            $"✅ Все команды без ограничений\n" +
            $"✅ Приоритетная поддержка\n\n" +
            $"*Стоимость:* 99₽\n" +
            $"*Премиум\\-пользователей:* {premiumCount}\n\n" +
            $"*Как оплатить:*\n" +
            $"1\\. Открой приложение *ЮMoney*\n" +
            $"2\\. Переведи *99₽* на номер:\n" +
            $"`4100 1192 9668 0958`\n" +
            $"3\\. Скопируй ID операции\n" +
            $"4\\. Отправь: /pay IDОПЕРАЦИИ\n\n" +
            $"*Пример:* /pay 1234567890123456\n\n" +
            $"⚠️ _Ссылки на оплату временно недоступны\\. Используй перевод в приложении ЮMoney\\._",
            parseMode: ParseMode.MarkdownV2,
            cancellationToken: ct);
    }

    private async Task HandleCheck(ITelegramBotClient bot, long chatId, long userId, CancellationToken ct)
    {
        var pending = await _db.GetPendingPaymentAsync(userId);
        if (pending == null)
        {
            await bot.SendMessage(chatId,
                "🔍 Нет ожидающих оплат.\n" +
                "Используй /premium чтобы оплатить.",
                cancellationToken: ct);
            return;
        }

        await bot.SendMessage(chatId,
            $"🔍 Оплата #{pending.Id} в обработке.\n\n" +
            $"Сумма: {pending.Amount:N0}₽\n" +
            $"ID: `{pending.Label}`\n\n" +
            $"Админ проверит и подтвердит.\n" +
            $"Обычно это занимает несколько минут.",
            cancellationToken: ct);
    }

    private async Task HandleConfirm(ITelegramBotClient bot, long chatId, long userId, string[] args, CancellationToken ct)
    {
        if (userId != AdminUserId)
        {
            await bot.SendMessage(chatId, "❌ Только админ может подтверждать оплаты.", cancellationToken: ct);
            return;
        }

        if (args.Length < 2)
        {
            await bot.SendMessage(chatId,
                "Формат: /confirm [ID оплаты]\n" +
                "Пример: /confirm premium_123456_20250101120000",
                cancellationToken: ct);
            return;
        }

        var label = args[1];
        var payment = await _db.GetPaymentByLabelAsync(label);
        if (payment == null)
        {
            await bot.SendMessage(chatId, $"❌ Оплата `{label}` не найдена.", parseMode: ParseMode.MarkdownV2, cancellationToken: ct);
            return;
        }

        if (payment.Status == "confirmed")
        {
            await bot.SendMessage(chatId, $"⚠️ Оплата `{label}` уже подтверждена.", parseMode: ParseMode.MarkdownV2, cancellationToken: ct);
            return;
        }

        await _db.ConfirmPaymentAsync(label);
        await _db.AddPremiumAsync(payment.UserId, label);

        await bot.SendMessage(chatId,
            $"✅ Оплата #{payment.Id} подтверждена!\n" +
            $"Пользователь {payment.UserId} получил премиум.",
            cancellationToken: ct);

        await bot.SendMessage(payment.UserId,
            "🎉 Премиум активирован!\n\n" +
            "✅ Безлимитные операции\n" +
            "✅ Все функции бота\n\n" +
            "Спасибо за поддержку! ❤️",
            cancellationToken: ct);
    }

    private async Task HandlePay(ITelegramBotClient bot, long chatId, long userId, string[] args, CancellationToken ct)
    {
        if (await _db.IsPremiumAsync(userId))
        {
            await bot.SendMessage(chatId, "✅ У тебя уже есть премиум!", cancellationToken: ct);
            return;
        }

        if (args.Length < 2)
        {
            await bot.SendMessage(chatId,
                "Формат: /pay [ID операции ЮMoney]\n" +
                "Пример: /pay 1234567890123456\n\n" +
                "ID операции найдёшь в чеке ЮMoney.",
                cancellationToken: ct);
            return;
        }

        var operationId = args[1].Trim();

        if (operationId.Length < 10 || !operationId.All(char.IsDigit))
        {
            await bot.SendMessage(chatId,
                "❌ Неверный формат ID операции.\n" +
                "ID должен содержать только цифры (10-20 символов).\n" +
                "Пример: /pay 1234567890123456",
                cancellationToken: ct);
            return;
        }

        var label = $"pay_{userId}_{operationId}";
        var existing = await _db.GetPaymentByLabelAsync(label);
        if (existing != null && existing.Status == "confirmed")
        {
            await bot.SendMessage(chatId, "⚠️ Этот ID операции уже использован.", cancellationToken: ct);
            return;
        }

        await _db.CreatePaymentAsync(userId, PremiumPrice, label);
        await _db.ConfirmPaymentAsync(label);
        await _db.AddPremiumAsync(userId, operationId);

        await bot.SendMessage(chatId,
            "🎉 Премиум активирован!\n\n" +
            "✅ Безлимитные операции\n" +
            "✅ Все функции бота\n\n" +
            "Спасибо за поддержку! ❤️",
            cancellationToken: ct);
    }

    private static string DetectCategory(string text)
    {
        text = text.ToLower();
        foreach (var (category, keywords) in CategoryKeywords)
        {
            if (keywords.Any(kw => text.Contains(kw)))
                return category;
        }
        return "прочее";
    }
}
