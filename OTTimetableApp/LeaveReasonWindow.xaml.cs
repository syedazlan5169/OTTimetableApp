using System.Windows;
using OTTimetableApp.Data.Models;

namespace OTTimetableApp;

public partial class LeaveReasonWindow : Window
{
    public LeaveReason? SelectedReason { get; private set; }

    public LeaveReasonWindow()
    {
        InitializeComponent();
    }

    private void Reason_Checked(object sender, RoutedEventArgs e)
    {
        OkButton.IsEnabled = BercutiRadio.IsChecked == true || BerkursusRadio.IsChecked == true;
    }

    private void Ok_Click(object sender, RoutedEventArgs e)
    {
        if (BercutiRadio.IsChecked == true)
            SelectedReason = LeaveReason.Bercuti;
        else if (BerkursusRadio.IsChecked == true)
            SelectedReason = LeaveReason.Berkursus;
        else
            return;

        DialogResult = true;
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
    }
}
