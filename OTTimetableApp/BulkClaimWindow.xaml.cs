using System.Windows;
using Microsoft.Win32;
using OTTimetableApp.ViewModels;

namespace OTTimetableApp;

public partial class BulkClaimWindow : Window
{
    private readonly BulkClaimVM _vm;

    public BulkClaimWindow(BulkClaimVM vm)
    {
        InitializeComponent();
        _vm = vm;
        DataContext = _vm;

        Loaded += OnLoaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        Loaded -= OnLoaded;
        _vm.LoadLookups();
    }

    private void Generate_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            _vm.GenerateAll();
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "Generate Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void ExportAll_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var folderDialog = new OpenFolderDialog
            {
                Title = "Select folder to export claims"
            };

            if (folderDialog.ShowDialog() != true)
                return;

            var (exported, skipped) = _vm.ExportAll(folderDialog.FolderName);

            var message = $"Exported {exported} claim(s) successfully.";
            if (skipped.Count > 0)
            {
                message += "\n\nSkipped:\n" + string.Join("\n", skipped);
            }

            MessageBox.Show(message, "Export Complete", MessageBoxButton.OK,
                skipped.Count > 0 ? MessageBoxImage.Warning : MessageBoxImage.Information);

            if (exported > 0)
            {
                System.Diagnostics.Process.Start("explorer.exe", folderDialog.FolderName);
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "Export Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
}
