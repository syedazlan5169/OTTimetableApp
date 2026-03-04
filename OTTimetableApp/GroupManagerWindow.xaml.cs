using System.Windows;
using System.Windows.Controls;
using OTTimetableApp.ViewModels;

namespace OTTimetableApp;

public partial class GroupManagerWindow : Window
{
    private readonly GroupManagerVM _vm;

    public GroupManagerWindow(GroupManagerVM vm)
    {
        InitializeComponent();
        _vm = vm;
        DataContext = _vm;

        Loaded += (_, __) => _vm.Load();
    }

    private void ApplyCapacity_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            _vm.SaveCapacity();
            MessageBox.Show("Capacity updated.");
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void SlotChanged_DropDownOpened(object sender, EventArgs e)
    {
        if (sender is not ComboBox cb) return;

        // store previous value so we can revert on error
        cb.Tag = cb.SelectedValue;
    }

    private void SlotChanged_DropDownClosed(object sender, EventArgs e)
    {
        if (sender is not ComboBox cb) return;
        if (cb.DataContext is not GroupSlotRowVM row) return;

        // SelectedValue may be boxed int or int?
        int? selected = cb.SelectedValue is int i ? i : cb.SelectedValue as int?;
        int? previous = cb.Tag is int pi ? pi : cb.Tag as int?;

        try
        {
            _vm.SetSlot(row.SlotIndex, selected);
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                ex.Message,
                "Group Assignment Error",
                MessageBoxButton.OK,
                MessageBoxImage.Warning
            );

            // revert UI selection safely
            cb.SelectedValue = previous;
        }
    }
}