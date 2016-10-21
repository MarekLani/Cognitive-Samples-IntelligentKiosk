using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.UI;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Media;

namespace IntelligentKioskSample.Views
{

    public class SentimentToColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            double score = (double)value;

            // Linear gradient function, from a red 0x99 when score = 0 to a green 0x77 when score = 1.
            return new SolidColorBrush(Color.FromArgb(0xff, (byte)(0x99 - (score * 0x99)), (byte)(score * 0x77), 0));
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            // not used
            return 0.5;
        }
    }
}
