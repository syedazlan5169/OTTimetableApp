using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace OTTimetableApp.Converters;

public class GroupIdToColorConverter : IValueConverter
{
    private static readonly Brush[] GroupColors =
    [
        new SolidColorBrush(Color.FromRgb(230, 240, 255)),  // Light blue
        new SolidColorBrush(Color.FromRgb(255, 240, 230)),  // Light orange
        new SolidColorBrush(Color.FromRgb(240, 255, 230)),  // Light green
        new SolidColorBrush(Color.FromRgb(255, 230, 240)),  // Light pink
        new SolidColorBrush(Color.FromRgb(240, 230, 255)),  // Light purple
        new SolidColorBrush(Color.FromRgb(255, 255, 230)),  // Light yellow
        new SolidColorBrush(Color.FromRgb(230, 255, 255)),  // Light cyan
        new SolidColorBrush(Color.FromRgb(255, 235, 235))   // Light red
    ];

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is int groupId && groupId > 0)
        {
            // Use modulo to cycle through colors if there are more groups than colors
            int colorIndex = (groupId - 1) % GroupColors.Length;
            return GroupColors[colorIndex];
        }

        return Brushes.Transparent;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
