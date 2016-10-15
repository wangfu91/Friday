using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.UI.Xaml.Data;

namespace Friday.UI.Converters
{
    internal class TimeSpanToSecondsConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            var timeSpan = value as TimeSpan?;
            return timeSpan?.TotalSeconds ?? 0.0;
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            var seconds = value as double?;
            return TimeSpan.FromSeconds(seconds ?? 0);
        }
    }
}
