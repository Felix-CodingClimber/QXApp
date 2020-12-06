using System; 

namespace QXScan.Core
{   
    public class QXConfig 
    {
        public string DB
        {
            get
            {
                return "data.db3";
            }
        }

        public string LogFile
        {
            get

            {
                return "error.log";
            }
        }

        public bool Sound { get; set; } 
        public bool Vibrate { get; set; }

        public QXConfig()
        {
            this.Sound = true;
            this.Vibrate = false;            
        }   
    }
}
