using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace palochki.DB_Stuff
{
    [Table("UserInfo")]
    public class UserInfo
    {
        [Key]
        [ForeignKey("UserDb")]
        [Column]
        public int UserId { get; set; }
        public UserDb UserDb { get; set; }
        [Column]
        public string? LastFoundFight { get; set; }
        [Column]
        public string? ArenaFightStarted { get; set; }
        [Column]
        public string? StamaUseStarted { get; set; }
        [Column]
        public int? BattleLock { get; set; }
        [Column]
        public int? AfterBattleLock { get; set; }
        [Column]
        public int? SkipHour { get; set; }
        [Column]
        public int? ArenasPlayed { get; set; }
        [Column]
        public int? StamaCountToSpend { get; set; }
        [Column]
        public int? LastBadRequestId { get; set; }
        [Column]
        public int? MorningQuest { get; set; }
        [Column]
        public int? StockEnabled { get; set; }
        [Column]
        public int? QuestType { get; set; }
        [Column]
        public int? IsCyberTea { get; set; }
        [Column]
        public string? CyberTeaOrder { get; set; }
        [Column]
        public string? LastOrder { get; set; }
    }
}