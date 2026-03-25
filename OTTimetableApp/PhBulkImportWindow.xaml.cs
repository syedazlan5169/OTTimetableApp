using Microsoft.EntityFrameworkCore;
using Microsoft.Win32;
using OTTimetableApp.Data;
using OTTimetableApp.Services;
using OTTimetableApp.ViewModels;
using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Controls;

namespace OTTimetableApp;

public partial class PhBulkImportWindow : Window
{
    private readonly IDbContextFactory<AppDbContext> _dbFactory;
    private readonly PublicHolidayService _phSvc;

    private List<PhImportRowVM> _parsedRows = [];

    private static readonly string[] DateFormats =
    [
        "dd/MM/yyyy", "d/M/yyyy", "d/MM/yyyy", "dd/M/yyyy",
        "yyyy-MM-dd", "dd-MM-yyyy", "d-M-yyyy",
        "MM/dd/yyyy", "M/d/yyyy"
    ];

    public PhBulkImportWindow(IDbContextFactory<AppDbContext> dbFactory, PublicHolidayService phSvc)
    {
        InitializeComponent();
        _dbFactory = dbFactory;
        _phSvc = phSvc;
        LoadCalendars();
    }

    private void LoadCalendars()
    {
        using var db = _dbFactory.CreateDbContext();
        var calendars = db.Calendars
            .AsNoTracking()
            .OrderByDescending(c => c.Year)
            .ThenBy(c => c.Name)
            .Select(c => new CalendarOptionVM { Id = c.Id, Display = $"{c.Year} - {c.Name}" })
            .ToList();

        CalendarFilter.ItemsSource = calendars;
        if (calendars.Count > 0)
            CalendarFilter.SelectedIndex = 0;
    }

    private void Browse_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFileDialog
        {
            Filter = "CSV Files (*.csv)|*.csv|All Files (*.*)|*.*",
            Title = "Select Public Holiday CSV"
        };

        if (dlg.ShowDialog() != true) return;

        FilePathText.Text = dlg.FileName;
        ParseFile(dlg.FileName);
    }

    private void ParseFile(string filePath)
    {
        _parsedRows.Clear();

        try
        {
            var lines = File.ReadAllLines(filePath);
            bool firstDataRow = true;

            foreach (var line in lines)
            {
                if (string.IsNullOrWhiteSpace(line)) continue;

                var parts = line.Split(',', 2);
                if (parts.Length < 2)
                {
                    _parsedRows.Add(new PhImportRowVM
                    {
                        DateDisplay = line.Trim(),
                        Name = "",
                        Status = "✗ Invalid format (expected: date,name)",
                        IsValid = false
                    });
                    continue;
                }

                string rawDate = parts[0].Trim();
                string name = parts[1].Trim().Trim('"');

                if (!TryParseDate(rawDate, out var date))
                {
                    // Skip header row silently only on the very first line
                    if (firstDataRow) { firstDataRow = false; continue; }

                    _parsedRows.Add(new PhImportRowVM
                    {
                        DateDisplay = rawDate,
                        Name = name,
                        Status = $"✗ Cannot parse date: \"{rawDate}\"",
                        IsValid = false
                    });
                    continue;
                }

                firstDataRow = false;
                _parsedRows.Add(new PhImportRowVM
                {
                    Date = date,
                    DateDisplay = date.ToString("dd/MM/yyyy"),
                    Name = name,
                    Status = "Pending validation...",
                    IsValid = true
                });
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Failed to read file:\n{ex.Message}", "Error",
                MessageBoxButton.OK, MessageBoxImage.Error);
            return;
        }

        if (CalendarFilter.SelectedValue is int calId)
            ValidateAgainstCalendar(calId);
        else
            RefreshGrid();
    }

    private void CalendarFilter_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_parsedRows.Count > 0 && CalendarFilter.SelectedValue is int calId)
            ValidateAgainstCalendar(calId);
    }

    private void ValidateAgainstCalendar(int calendarId)
    {
        using var db = _dbFactory.CreateDbContext();

        var calendar = db.Calendars.AsNoTracking().FirstOrDefault(c => c.Id == calendarId);
        if (calendar == null) return;

        var calendarDates = db.CalendarDays
            .AsNoTracking()
            .Where(d => d.CalendarId == calendarId)
            .Select(d => d.Date)
            .ToHashSet();

        foreach (var row in _parsedRows)
        {
            if (!row.IsValid || row.Date == null) continue;

            if (calendarDates.Contains(row.Date.Value))
            {
                row.Status = "✓ Ready to import";
                row.IsValid = true;
            }
            else
            {
                row.Status = $"✗ Date not in calendar ({calendar.Year})";
                row.IsValid = false;
            }
        }

        RefreshGrid();
    }

    private void RefreshGrid()
    {
        PreviewGrid.ItemsSource = null;
        PreviewGrid.ItemsSource = _parsedRows;

        int valid = _parsedRows.Count(r => r.IsValid);
        int invalid = _parsedRows.Count(r => !r.IsValid);

        if (_parsedRows.Count == 0)
        {
            StatusLabel.Text = "";
            ImportButton.IsEnabled = false;
            ImportButton.Content = "Import";
        }
        else
        {
            StatusLabel.Text = $"{valid} row(s) ready to import, {invalid} row(s) will be skipped.";
            ImportButton.IsEnabled = valid > 0;
            ImportButton.Content = $"Import {valid} Row(s)";
        }
    }

    private void Import_Click(object sender, RoutedEventArgs e)
    {
        if (CalendarFilter.SelectedValue is not int calId) return;

        var entries = _parsedRows
            .Where(r => r.IsValid && r.Date.HasValue)
            .Select(r => (r.Date!.Value, r.Name))
            .ToList();

        if (entries.Count == 0) return;

        try
        {
            var (imported, skipped, errors) = _phSvc.BulkImportPH(calId, entries);

            string msg = $"Import complete.\n\nImported: {imported}\nAlready set (skipped): {skipped}";
            if (errors.Count > 0)
                msg += $"\n\nWarnings:\n{string.Join("\n", errors.Take(10))}";

            MessageBox.Show(msg, "Import Complete", MessageBoxButton.OK, MessageBoxImage.Information);
            Close();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Import failed:\n{ex.Message}", "Error",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void Close_Click(object sender, RoutedEventArgs e) => Close();

    private static bool TryParseDate(string s, out DateOnly result)
    {
        foreach (var fmt in DateFormats)
            if (DateOnly.TryParseExact(s, fmt, CultureInfo.InvariantCulture, DateTimeStyles.None, out result))
                return true;

        result = default;
        return false;
    }
}

public class PhImportRowVM
{
    public DateOnly? Date { get; set; }
    public string DateDisplay { get; set; } = "";
    public string Name { get; set; } = "";
    public string Status { get; set; } = "";
    public bool IsValid { get; set; }
}
