using System;
using SQLite.Net.Attributes;

namespace QXScan.Core
{ 
    public class History
    {
        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }

        public int Cateogry { get; set; }

        public string Text { get; set; }

        public DateTime CreateDate { get; set; }
    }
}
