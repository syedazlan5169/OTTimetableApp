using OTTimetableApp.ViewModels;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;

namespace OTTimetableApp;

public partial class EmployeeManagerWindow : Window
{
    private readonly EmployeeManagerVM _vm;
    private static readonly Regex _decimalRegex = new Regex(@"^\d*\.?\d*$");

    public EmployeeManagerWindow(EmployeeManagerVM vm)
    {
        InitializeComponent();
        DataObject.AddPastingHandler(this, OnPaste);
        _vm = vm;
        DataContext = _vm;

        Loaded += (_, __) => _vm.Load();
    }

    private void OnPaste(object sender, DataObjectPastingEventArgs e)
    {
        if (!e.DataObject.GetDataPresent(typeof(string)))
        {
            e.CancelCommand();
            return;
        }

        string text = (string)e.DataObject.GetData(typeof(string))!;
        if (!_decimalRegex.IsMatch(text))
            e.CancelCommand();
    }

    private void Salary_PreviewTextInput(object sender, System.Windows.Input.TextCompositionEventArgs e)
    {
        if (sender is not TextBox tb) return;

        string proposed = tb.Text.Insert(tb.SelectionStart, e.Text);

        e.Handled = !_decimalRegex.IsMatch(proposed);
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