using System.Windows;
using OTTimetableApp.ViewModels;

namespace OTTimetableApp;

public partial class ClaimPreviewWindow : Window
{
    private readonly ClaimPreviewVM _vm;

    public ClaimPreviewWindow(ClaimPreviewVM vm)
    {
        InitializeComponent();
        _vm = vm;
        DataContext = _vm;

        Loaded += OnLoaded;
        Closed += OnClosed;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        Loaded -= OnLoaded;
        _vm.LoadLookups();
    }

    private void OnClosed(object? sender, EventArgs e)
    {
        Closed -= OnClosed;
        _vm.Dispose();
    }

    private void Generate_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            _vm.Generate();
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "Generate Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void Export_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            _vm.ExportToExcel();
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "Export Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
}