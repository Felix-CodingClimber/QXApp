using System;
using System.Threading.Tasks;
using Windows.Storage;

namespace QXScan.Core
{
    public class QXLog
    {
        public string File { get; set; }
 
        public async Task Write(string str)
        {
            // access the local folder
            var folder = ApplicationData.Current.LocalFolder;
            var file = await folder.GetFileAsync(this.File);

            if (file != null)
            {
                string text = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + " " + str + "\r\n";

                await FileIO.AppendTextAsync(file, text);
            }
        }

        public async Task<string> Read()
        {
            // access the local folder
            var folder = ApplicationData.Current.LocalFolder;
            var file = await folder.GetFileAsync(this.File);

            return await FileIO.ReadTextAsync(file); 
        }
    }
}