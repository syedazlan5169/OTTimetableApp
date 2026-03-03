using Microsoft.Extensions.DependencyInjection;
using OTTimetableApp.Services;
using OTTimetableApp.ViewModels;
using System.Windows;
using System.Windows.Controls;

namespace OTTimetableApp;

public partial class MainWindow : Window
{
    private readonly MonthViewerVM _vm;
    private readonly SlotUpdateService _slotSvc;

    public MainWindow(MonthViewerVM vm, SlotUpdateService slotSvc)
    {
        InitializeComponent();
        _vm = vm;
        _slotSvc = slotSvc;
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
        if (sender is not ComboBox cb) return;
        if (cb.DataContext is not ShiftSlotVM slotVm) return;

        try
        {
            int? selected = cb.SelectedValue as int?;
            if (selected == 0) selected = null;

            _slotSvc.UpdateSlot(slotVm.ShiftSlotId, selected);

            _vm.LoadMonth();
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            _vm.LoadMonth();
        }
    }

    private void ManageCalendars_Click(object sender, RoutedEventArgs e)
    {
        var win = App.Services.GetRequiredService<CalendarManagerWindow>();
        win.Owner = this;
        win.ShowDialog();

        // Reload calendars after closing manager
        _vm.LoadCalendars();
        _vm.LoadMonth();
    }



}