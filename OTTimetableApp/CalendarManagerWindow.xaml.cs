using System.Windows;
using OTTimetableApp.Services;
using OTTimetableApp.Data;
using Microsoft.EntityFrameworkCore;
using OTTimetableApp.Data.Models;

namespace OTTimetableApp;

public partial class CalendarManagerWindow : Window
{
    private readonly MonthViewService _monthSvc;
    private readonly CalendarGeneratorService2 _genSvc;
    private readonly IDbContextFactory<AppDbContext> _dbFactory;

    public CalendarManagerWindow(MonthViewService monthSvc, CalendarGeneratorService2 genSvc, IDbContextFactory<AppDbContext> dbFactory)
    {
        InitializeComponent();
        _monthSvc = monthSvc;
        _genSvc = genSvc;
        _dbFactory = dbFactory;

        LoadGroups();
        RefreshGrid();
    }

    private void LoadGroups()
    {
        var groups = _monthSvc.ListGroups();

        InitNight.ItemsSource = groups;
        InitMorning.ItemsSource = groups;
        InitEvening.ItemsSource = groups;

        if (groups.Count >= 3)
        {
            InitNight.SelectedValue = groups[0].Id;
            InitMorning.SelectedValue = groups[1].Id;
            InitEvening.SelectedValue = groups[2].Id;
        }
    }

    private void RefreshGrid()
    {
        using var db = _dbFactory.CreateDbContext();
        CalendarGrid.ItemsSource = db.Calendars.AsNoTracking().OrderByDescending(c => c.Year).ThenBy(c => c.Name).ToList();
    }

    private void Create_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var name = NameBox.Text?.Trim() ?? "";
            if (!int.TryParse(YearBox.Text, out var year)) throw new Exception("Invalid year.");

            var night = (int)InitNight.SelectedValue;
            var morning = (int)InitMorning.SelectedValue;
            var evening = (int)InitEvening.SelectedValue;

            _monthSvc.CreateCalendar(name, year, night, morning, evening);

            MessageBox.Show("Calendar created. Now click 'Generate Year' to build the timetable.", "OK");
            RefreshGrid();
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void Generate_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (CalendarGrid == null)
            {
                MessageBox.Show("Calendar grid is not ready (CalendarGrid is null).", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            if (CalendarGrid.SelectedItem == null)
            {
                MessageBox.Show("Select a calendar first.", "Warning",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (CalendarGrid.SelectedItem is not Calendar cal)
            {
                MessageBox.Show($"Unexpected selected item type: {CalendarGrid.SelectedItem.GetType().FullName}",
                    "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            _genSvc.GenerateYear(cal.Id);

            MessageBox.Show("Year generated successfully.", "OK");
            RefreshGrid();
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.ToString(), "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void Delete_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (CalendarGrid.SelectedItem is not Calendar cal)
                throw new Exception("Select a calendar first.");

            if (MessageBox.Show("Delete this calendar and all its data?", "Confirm",
                    MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes)
                return;

            _monthSvc.DeleteCalendar(cal.Id);

            MessageBox.Show("Deleted.", "OK");
            RefreshGrid();
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
    private void Close_Click(object sender, RoutedEventArgs e) => Close();
}