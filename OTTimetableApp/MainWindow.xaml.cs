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
}