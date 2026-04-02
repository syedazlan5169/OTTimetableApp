using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace OTTimetableApp.Converters;

/// <summary>
/// Returns a highlight brush when a slot's ActualEmployeeId matches the highlighted employee.
/// Values[0]: ActualEmployeeId (int?) from ShiftSlotVM
/// Values[1]: HighlightedEmployeeId (int) from MonthViewerVM – 0 means no highlight
/// </summary>
public class EmployeeHighlightConverter : IMultiValueConverter
{
    private static readonly Brush HighlightBrush =
        new SolidColorBrush(Color.FromRgb(255, 245, 157)); // #FFF59D – light amber yellow

    static EmployeeHighlightConverter()
    {
        HighlightBrush.Freeze();
    }

    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        if (values.Length < 2) return Brushes.Transparent;

        if (values[1] is not int highlightedId || highlightedId == 0)
            return Brushes.Transparent;

        int? actualId = values[0] is int i ? i : (int?)null;

        return actualId.HasValue && actualId.Value == highlightedId
            ? HighlightBrush
            : Brushes.Transparent;
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        => throw new NotImplementedException();
}
