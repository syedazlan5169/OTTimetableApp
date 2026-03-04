using System.Windows;
using OTTimetableApp.ViewModels;

namespace OTTimetableApp;

public partial class EmployeeManagerWindow : Window
{
    private readonly EmployeeManagerVM _vm;

    public EmployeeManagerWindow(EmployeeManagerVM vm)
    {
        InitializeComponent();
        _vm = vm;
        DataContext = _vm;

        Loaded += (_, __) => _vm.Load();
    }



private void New_Click(object sender, RoutedEventArgs e)
    {
        _vm.NewEmployee();
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            _vm.Save();
            MessageBox.Show("Saved.");
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "Save Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void Delete_Click(object sender, RoutedEventArgs e)
    {
        if (_vm.Edit.Id == 0) return;

        var ok = MessageBox.Show("Delete this employee?", "Confirm",
            MessageBoxButton.YesNo, MessageBoxImage.Warning);

        if (ok != MessageBoxResult.Yes) return;

        try
        {
            _vm.DeleteSelected();
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "Delete Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void Salary_LostFocus(object sender, RoutedEventArgs e)
    {
        if (DataContext is EmployeeManagerVM vm)
            vm.RefreshComputed();
    }
}