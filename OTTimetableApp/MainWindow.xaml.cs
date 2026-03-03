using System.Windows;
using System.Windows.Controls;
using OTTimetableApp.ViewModels;

namespace OTTimetableApp;

public partial class MainWindow : Window
{
    private readonly MonthViewerVM _vm;

    public MainWindow(MonthViewerVM vm)
    {
        InitializeComponent();

        _vm = vm;
        DataContext = _vm;

        _vm.LoadCalendars();
        _vm.LoadMonth();
    }

    private void Calendar_Changed(object sender, SelectionChangedEventArgs e)
    {
        _vm.LoadMonth();
    }

    private void Month_Changed(object sender, SelectionChangedEventArgs e)
    {
        _vm.LoadMonth();
    }

    private void PH_Changed(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement fe) return;
        if (fe.DataContext is not DayRowVM day) return;

        _vm.SavePH(day.CalendarDayId, day.IsPublicHoliday, day.PublicHolidayName);
    }

    private void PHName_LostFocus(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement fe) return;
        if (fe.DataContext is not DayRowVM day) return;

        _vm.SavePH(day.CalendarDayId, day.IsPublicHoliday, day.PublicHolidayName);
    }

    private void Slot_DropDownClosed(object sender, EventArgs e)
    {
        if (sender is not FrameworkElement fe) return;
        if (fe.DataContext is not ShiftSlotVM slot) return;

        _vm.SaveSlot(slot.ShiftSlotId, slot.ActualEmployeeId);
    }
}