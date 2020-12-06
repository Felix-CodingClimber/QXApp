using System;
using System.Collections.Generic;
using Windows.UI.Xaml.Data;

namespace QXScan.Core
{
    public class IconConverter : IValueConverter
    {
        private static List<string> Images = new List<string>(new string[] { "", "text", "email", "link", "call", "barcode", "isbn", "event", "wifi", "namecard" });
         
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            // value is the data from the source object.
            int i = (int)value;
              
            return "Assets/icons/" + Images[i] + ".png";
        }

        // ConvertBack is not implemented for a OneWay binding.
        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }
    }
}
