using SQLite;

namespace TelegramBudgetBot.Models
{
    public class Transaction
    {
        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }
        public long UserId { get; set; }
        public decimal Amount { get; set; }
        public string Category { get; set; } = string.Empty;
        public string Type { get; set; } = "expense"; // expense | income
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public string? Description { get; set; }
    }

    public class CategoryLimit
    {
        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }
        public long UserId { get; set; }
        public string Category { get; set; } = string.Empty;
        public decimal LimitAmount { get; set; }
    }

    public class Reminder
    {
        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }
        public long UserId { get; set; }
        public int Hour { get; set; }
        public int Minute { get; set; }
        public bool Enabled { get; set; } = true;
    }

    public class Referral
    {
        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }
        public long InviterId { get; set; }
        public long InvitedId { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
