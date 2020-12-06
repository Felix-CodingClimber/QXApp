using SQLite.Net;

namespace QXScan.Core
{
    public class DbFactory
    {
        public static readonly string InsertCommand = "INSERT INTO History (Cateogry, Text, CreateDate) VALUES (?, ?, ?)";

        public static SQLiteConnection Open(string db)
        {
            return new SQLiteConnection(new SQLite.Net.Platform.WinRT.SQLitePlatformWinRT(), db, false);
        }
    }
}
