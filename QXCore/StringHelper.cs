using System;
using System.IO;
using System.Runtime.Serialization.Json;

namespace QXScan.Core
{
    public class StringHelper
    {
        public static bool IsURL(string link)
        {
            return link.StartsWith("http") || link.StartsWith("HTTP");
        }

        public static string Serialize<T>(T instance) where T : class
        {
            var serializer = new DataContractJsonSerializer(typeof(T));

            using (var ms = new MemoryStream())
            {
                serializer.WriteObject(ms, instance);

                ms.Flush();
                ms.Position = 0;

                using (var reader = new StreamReader(ms))
                {
                    return reader.ReadToEnd();
                }
            }
        }

        public static T Deserialize<T>(string str) where T : class
        {
            var serializer = new DataContractJsonSerializer(typeof(T));

            using (var ms = new MemoryStream())
            {
                using (var writer = new StreamWriter(ms))
                {
                    writer.Write(str);
                    writer.Flush();

                    ms.Position = 0;

                    return serializer.ReadObject(ms) as T;
                }
            }
        }
    }
}
