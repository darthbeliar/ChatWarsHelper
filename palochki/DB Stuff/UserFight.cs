using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace palochki.DB_Stuff
{
    [Table("UserFight")]
    public class UserFight
    {
        [ForeignKey("UserDb")]
        [Column]
        public int? UserId { get; set; }
        [Column]
        public int? FightMsgId { get; set; }
        public UserDb UserDb { get; set; }
        [Key]
        [Column]
        public int? FightId { get; set; }
    }
}