using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace OTTimetableApp.Converters;

/// <summary>
/// Returns a highlight brush when an OFF-column member name matches the highlighted employee name.
/// Values[0]: member name string (bound item in OffGroupMembers list)
/// Values[1]: HighlightedEmployeeName (string) from MonthViewerVM – empty means no highlight
/// </summary>
public class EmployeeNameHighlightConverter : IMultiValueConverter
{
    private static readonly Brush HighlightBrush =
        new SolidColorBrush(Color.FromRgb(255, 245, 157)); // #FFF59D – light amber yellow

    private static readonly Brush DefaultBrush =
        new SolidColorBrush(Color.FromRgb(250, 250, 250)); // #FAFAFA – original OFF-item background

    static EmployeeNameHighlightConverter()
    {
        HighlightBrush.Freeze();
        DefaultBrush.Freeze();
    }

    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        if (values.Length < 2) return DefaultBrush;

        var name = values[0] as string ?? "";
        var highlightedName = values[1] as string ?? "";

        if (string.IsNullOrEmpty(highlightedName))
            return DefaultBrush;

        return name == highlightedName ? HighlightBrush : DefaultBrush;
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        => throw new NotImplementedException();
}
