using TelegramBudgetBot.Models;
using SQLite;

namespace TelegramBudgetBot.Services
{
    public class BotDatabase
    {
        private readonly SQLiteAsyncConnection _db;

        public BotDatabase(string dbPath)
        {
            _db = new SQLiteAsyncConnection(dbPath);
        }

        public async Task InitAsync()
        {
            await _db.CreateTableAsync<Transaction>();
            await _db.CreateTableAsync<CategoryLimit>();
            await _db.CreateTableAsync<Reminder>();
            await _db.CreateTableAsync<Referral>();
        }

        public Task<int> AddTransactionAsync(Transaction tx)
            => _db.InsertAsync(tx);

        public Task<List<Transaction>> GetMonthTransactionsAsync(long userId, DateTime month)
        {
            var start = new DateTime(month.Year, month.Month, 1, 0, 0, 0, DateTimeKind.Utc);
            var end = start.AddMonths(1);
            return _db.Table<Transaction>()
                .Where(t => t.UserId == userId && t.CreatedAt >= start && t.CreatedAt < end)
                .ToListAsync();
        }

        public Task<List<Transaction>> GetTransactionsAsync(long userId, string? type = null)
        {
            var query = _db.Table<Transaction>().Where(t => t.UserId == userId);
            if (type != null)
                query = query.Where(t => t.Type == type);
            return query.OrderByDescending(t => t.CreatedAt).ToListAsync();
        }

        public Task<decimal> GetBalanceAsync(long userId, DateTime month)
        {
            var start = new DateTime(month.Year, month.Month, 1, 0, 0, 0, DateTimeKind.Utc);
            var end = start.AddMonths(1);
            return _db.ExecuteScalarAsync<decimal>(
                "SELECT COALESCE(SUM(CASE WHEN Type='income' THEN Amount ELSE -Amount END), 0) " +
                "FROM [Transaction] WHERE UserId = ? AND CreatedAt >= ? AND CreatedAt < ?",
                userId, start, end);
        }

        public Task<List<CategoryLimit>> GetLimitsAsync(long userId)
            => _db.Table<CategoryLimit>().Where(l => l.UserId == userId).ToListAsync();

        public Task<CategoryLimit?> GetLimitAsync(long userId, string category)
            => _db.Table<CategoryLimit>()
                .Where(l => l.UserId == userId && l.Category == category)
                .FirstOrDefaultAsync();

        public Task UpsertLimitAsync(long userId, string category, decimal limitAmount)
        {
            return _db.RunInTransactionAsync(async tr =>
            {
                var existing = tr.Table<CategoryLimit>()
                    .Where(l => l.UserId == userId && l.Category == category)
                    .FirstOrDefault();
                if (existing != null)
                {
                    existing.LimitAmount = limitAmount;
                    tr.Update(existing);
                }
                else
                {
                    tr.Insert(new CategoryLimit
                    {
                        UserId = userId,
                        Category = category,
                        LimitAmount = limitAmount
                    });
                }
            });
        }

        public async Task<Dictionary<string, decimal>> GetCategorySpendingAsync(long userId, DateTime month)
        {
            var start = new DateTime(month.Year, month.Month, 1, 0, 0, 0, DateTimeKind.Utc);
            var end = start.AddMonths(1);
            var txs = await _db.Table<Transaction>()
                .Where(t => t.UserId == userId && t.Type == "expense" && t.CreatedAt >= start && t.CreatedAt < end)
                .ToListAsync();
            return txs.GroupBy(t => t.Category)
                .ToDictionary(g => g.Key, g => g.Sum(t => t.Amount));
        }

        public Task<int> GetMonthCountAsync(long userId, DateTime month)
        {
            var start = new DateTime(month.Year, month.Month, 1, 0, 0, 0, DateTimeKind.Utc);
            var end = start.AddMonths(1);
            return _db.ExecuteScalarAsync<int>(
                "SELECT COUNT(*) FROM [Transaction] WHERE UserId = ? AND CreatedAt >= ? AND CreatedAt < ?",
                userId, start, end);
        }

        public Task SetReminderAsync(long userId, TimeOnly time)
        {
            return _db.RunInTransactionAsync(tr =>
            {
                var existing = tr.Table<Reminder>()
                    .Where(r => r.UserId == userId)
                    .FirstOrDefault();
                if (existing != null)
                {
                    existing.Hour = time.Hour;
                    existing.Minute = time.Minute;
                    existing.Enabled = true;
                    tr.Update(existing);
                }
                else
                {
                    tr.Insert(new Reminder
                    {
                        UserId = userId,
                        Hour = time.Hour,
                        Minute = time.Minute,
                        Enabled = true
                    });
                }
            });
        }

        public Task DisableReminderAsync(long userId)
        {
            return _db.RunInTransactionAsync(tr =>
            {
                var existing = tr.Table<Reminder>()
                    .Where(r => r.UserId == userId)
                    .FirstOrDefault();
                if (existing != null)
                {
                    existing.Enabled = false;
                    tr.Update(existing);
                }
            });
        }

        public Task<List<Reminder>> GetActiveRemindersAsync()
            => _db.Table<Reminder>().Where(r => r.Enabled).ToListAsync();

        public Task AddReferralAsync(long inviterId, long invitedId)
            => _db.InsertAsync(new Referral { InviterId = inviterId, InvitedId = invitedId });

        public Task<int> GetReferralCountAsync(long userId)
            => _db.ExecuteScalarAsync<int>(
                "SELECT COUNT(*) FROM [Referral] WHERE InviterId = ?", userId);

        public async Task<List<Transaction>> GetGroupTransactionsAsync(long chatId)
        {
            return await _db.Table<Transaction>().ToListAsync();
        }
    }
}
