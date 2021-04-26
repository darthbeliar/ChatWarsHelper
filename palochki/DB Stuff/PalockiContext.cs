using System.Data.Entity;
using System.Data.Entity.Core.Common;
using System.Data.SQLite;
using System.Data.SQLite.EF6;

namespace palochki.DB_Stuff
{
    class PalockiContext : DbContext
    {
        public DbSet<UserFight> UserFights{ get; set; }
        public DbSet<UserInfo> UserInfos{ get; set; }
        public DbSet<UserDb> DbUsers{ get; set; }
        public DbSet<LowHpReplies> LowHpReplies{ get; set; }

        public PalockiContext() :
            base(new SQLiteConnection()
            {
                ConnectionString = new SQLiteConnectionStringBuilder() { DataSource = @"palochkiDB.db", ForeignKeys = true }.ConnectionString
            }, true){}
    }

    public class SqLiteConfiguration : DbConfiguration
    {
        public SqLiteConfiguration()
        {
            SetProviderFactory("System.Data.SQLite", SQLiteFactory.Instance);
            SetProviderFactory("System.Data.SQLite.EF6", SQLiteProviderFactory.Instance);
            SetProviderServices("System.Data.SQLite", (DbProviderServices)SQLiteProviderFactory.Instance.GetService(typeof(DbProviderServices)));
        }
    }
}
