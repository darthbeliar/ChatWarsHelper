using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace palochki.DB_Stuff
{
    [Table("UserDb")]
    public class UserDb
    {
        [Key]
        [Column]
        public int Id { get; set; }
        [Column]
        public string UserName { get; set; }
        [Column]
        public string UserTelId { get; set; }
        [Column]
        public string UserTelHash { get; set; }
        [Column]
        public int? GuildChatId { get; set; }
        [Column]
        public string? GuildChatName { get; set; }
        [Column]
        public int AcceptOrders { get; set; }
        [Column]
        public int? OrdersChatId { get; set; }
        [Column]
        public string? OrdersChatName { get; set; }
        [Column]
        public int? SavesChatId { get; set; }
        [Column]
        public int? ResultsChatId { get; set; }
        [Column]
        public string? ResultsChatName { get; set; }
        [Column]
        public int BotEnabled { get; set; }
        [Column]
        public int ArenasEnabled { get; set; }
        [Column]
        public int StamaEnabled { get; set; }
        [Column]
        public int PotionsEnabled { get; set; }
        [Column]
        public int AutoGDefEnabled { get; set; }
        [Column]
        public int HyperionUser { get; set; }
        [Column]
        public int CorovansEnabled { get; set; }
    }
}