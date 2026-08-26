using Xunit;
using TelegramBudgetBot.Models;
using TelegramBudgetBot.Services;

namespace TelegramBudgetBot.Tests
{
    public class BotDatabaseTests : IDisposable
    {
        private readonly BotDatabase _db;
        private readonly string _dbPath;

        public BotDatabaseTests()
        {
            _dbPath = Path.Combine(Path.GetTempPath(), $"bot_test_{Guid.NewGuid():N}.db");
            SQLitePCL.Batteries_V2.Init();
            _db = new BotDatabase(_dbPath);
            _db.InitAsync().Wait();
        }

        public void Dispose()
        {
            try { File.Delete(_dbPath); } catch { }
            try { File.Delete(_dbPath + "-wal"); } catch { }
            try { File.Delete(_dbPath + "-shm"); } catch { }
        }

        private static Transaction MakeTx(long userId, decimal amount, string type = "expense",
            string category = "еда", DateTime? date = null)
        {
            return new Transaction
            {
                UserId = userId,
                Amount = amount,
                Category = category,
                Type = type,
                CreatedAt = date ?? DateTime.UtcNow
            };
        }

        [Fact]
        public async Task AddTransaction_And_GetBalance()
        {
            var userId = 12345L;
            await _db.AddTransactionAsync(MakeTx(userId, 1000, "income"));
            await _db.AddTransactionAsync(MakeTx(userId, 300, "expense", "еда"));
            await _db.AddTransactionAsync(MakeTx(userId, 200, "expense", "транспорт"));

            var balance = await _db.GetBalanceAsync(userId, DateTime.UtcNow);

            Assert.Equal(500m, balance);
        }

        [Fact]
        public async Task GetMonthTransactions_Filters_By_Month()
        {
            var userId = 12345L;
            var now = DateTime.UtcNow;
            await _db.AddTransactionAsync(MakeTx(userId, 100, "expense", date: now));
            await _db.AddTransactionAsync(MakeTx(userId, 200, "expense",
                date: now.AddMonths(-1)));

            var txs = await _db.GetMonthTransactionsAsync(userId, now);

            Assert.Single(txs);
        }

        [Fact]
        public async Task UpsertLimit_Inserts_And_Updates()
        {
            var userId = 12345L;
            await _db.UpsertLimitAsync(userId, "еда", 15000);
            var limit = await _db.GetLimitAsync(userId, "еда");
            Assert.Equal(15000m, limit!.LimitAmount);

            await _db.UpsertLimitAsync(userId, "еда", 20000);
            limit = await _db.GetLimitAsync(userId, "еда");
            Assert.Equal(20000m, limit!.LimitAmount);
        }

        [Fact]
        public async Task GetCategorySpending_Sums_Expenses()
        {
            var userId = 12345L;
            var now = DateTime.UtcNow;
            await _db.AddTransactionAsync(MakeTx(userId, 500, "expense", "еда", now));
            await _db.AddTransactionAsync(MakeTx(userId, 300, "expense", "еда", now));
            await _db.AddTransactionAsync(MakeTx(userId, 200, "expense", "транспорт", now));

            var spending = await _db.GetCategorySpendingAsync(userId, now);

            Assert.Equal(800m, spending["еда"]);
            Assert.Equal(200m, spending["транспорт"]);
        }

        [Fact]
        public async Task SetReminder_And_GetActive()
        {
            var userId = 12345L;
            await _db.SetReminderAsync(userId, new TimeOnly(21, 0));

            var reminders = await _db.GetActiveRemindersAsync();
            Assert.Single(reminders);
            Assert.Equal(21, reminders[0].Hour);
            Assert.Equal(0, reminders[0].Minute);

            await _db.DisableReminderAsync(userId);
            reminders = await _db.GetActiveRemindersAsync();
            Assert.Empty(reminders);
        }

        [Fact]
        public async Task AddReferral_And_GetCount()
        {
            var inviter = 100L;
            var invited1 = 200L;
            var invited2 = 300L;

            await _db.AddReferralAsync(inviter, invited1);
            await _db.AddReferralAsync(inviter, invited2);

            var count = await _db.GetReferralCountAsync(inviter);
            Assert.Equal(2, count);
        }

        [Fact]
        public async Task Premium_User_Lifecycle()
        {
            var userId = 12345L;

            Assert.False(await _db.IsPremiumAsync(userId));

            await _db.AddPremiumAsync(userId, "charge_123");

            Assert.True(await _db.IsPremiumAsync(userId));
            Assert.Equal(1, await _db.GetPremiumCountAsync());
        }

        [Fact]
        public async Task GetMonthCount_Returns_Correct_Count()
        {
            var userId = 12345L;
            var now = DateTime.UtcNow;
            await _db.AddTransactionAsync(MakeTx(userId, 100, "expense", date: now));
            await _db.AddTransactionAsync(MakeTx(userId, 200, "expense", date: now));
            await _db.AddTransactionAsync(MakeTx(userId, 300, "expense",
                date: now.AddMonths(-1)));

            var count = await _db.GetMonthCountAsync(userId, now);
            Assert.Equal(2, count);
        }
    }

    public class ChartGeneratorTests
    {
        [Fact]
        public void GenerateBarChart_Empty_Data_Returns_Empty()
        {
            var result = ChartGenerator.GenerateBarChart(new Dictionary<string, decimal>(), "Test");
            Assert.Empty(result);
        }

        [Fact]
        public void GenerateBarChart_With_Data_Returns_PNG()
        {
            var data = new Dictionary<string, decimal>
            {
                ["еда"] = 5000,
                ["транспорт"] = 2000,
                ["жильё"] = 15000
            };

            var result = ChartGenerator.GenerateBarChart(data, "Расходы");

            Assert.NotEmpty(result);
            // PNG magic bytes: 137 80 78 71
            Assert.Equal(137, result[0]);
            Assert.Equal(80, result[1]);
            Assert.Equal(78, result[2]);
            Assert.Equal(71, result[3]);
        }

        [Fact]
        public void GenerateBarChart_Single_Category()
        {
            var data = new Dictionary<string, decimal> { ["еда"] = 3000 };

            var result = ChartGenerator.GenerateBarChart(data, "Тест");

            Assert.NotEmpty(result);
            Assert.Equal(137, result[0]); // PNG header
        }
    }
}
