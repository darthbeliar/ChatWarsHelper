using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace palochki.DB_Stuff
{
    [Table("LowHpReplies")]
    public class LowHpReplies
    {
        [Key]
        [Column]
        public string Reply { get; set; }
    }
}