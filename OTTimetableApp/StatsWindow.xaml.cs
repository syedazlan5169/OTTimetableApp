using Microsoft.EntityFrameworkCore;
using OTTimetableApp.Data;
using OTTimetableApp.Domain.OT;
using OTTimetableApp.Services;
using OTTimetableApp.ViewModels;
using OxyPlot;
using OxyPlot.Axes;
using OxyPlot.Series;
using System.Diagnostics;
using System.Windows;

namespace OTTimetableApp;

public partial class StatsWindow : Window
{
    private readonly IDbContextFactory<AppDbContext> _dbFactory;
    private readonly OtCalculatorService _otSvc;

    private record MonthItem(int Month, string Name);

    private record StatsData(
        decimal TotalOtHours,
        int TotalClaimLines,
        int EmployeesWithOt,
        decimal HighestHours,
        List<(string Name, decimal Hours)> TopEmployees,
        List<(string Label, decimal Hours)> CategoryHours,
        decimal DayHours,
        decimal NightHours,
        List<(int Month, decimal Hours)> MonthlyHours,
        long ElapsedMs);

    public StatsWindow(IDbContextFactory<AppDbContext> dbFactory, OtCalculatorService otSvc)
    {
        InitializeComponent();
        _dbFactory = dbFactory;
        _otSvc = otSvc;
        LoadFilters();
    }

    private void LoadFilters()
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

        var months = new List<MonthItem> { new(0, "All Year") };
        months.AddRange(Enumerable.Range(1, 12)
            .Select(m => new MonthItem(m, new DateTime(2026, m, 1).ToString("MMMM"))));

        MonthFilter.ItemsSource = months;
        MonthFilter.SelectedIndex = 0;
    }

    private async void Load_Click(object sender, RoutedEventArgs e)
    {
        if (CalendarFilter.SelectedValue is not int calId) return;
        if (MonthFilter.SelectedValue is not int month) return;

        LoadButton.IsEnabled = false;
        StatusLabel.Text = "Calculating OT…";

        try
        {
            var data = await Task.Run(() => ComputeStats(calId, month));
            RenderStats(data);
            StatusLabel.Text = $"Loaded in {data.ElapsedMs}ms";
        }
        catch (Exception ex)
        {
            StatusLabel.Text = $"Error: {ex.Message}";
        }
        finally
        {
            LoadButton.IsEnabled = true;
        }
    }

    private StatsData ComputeStats(int calendarId, int month)
    {
        var sw = Stopwatch.StartNew();

        using var db = _dbFactory.CreateDbContext();
        var employees = db.Employees.AsNoTracking()
            .Where(e => e.IsActive)
            .Select(e => new { e.Id, e.Name })
            .ToList();

        var monthsToProcess = month == 0
            ? Enumerable.Range(1, 12).ToList()
            : [month];

        var employeeHours = new Dictionary<string, decimal>();
        var categoryHours = new Dictionary<OtCategory, decimal>();
        decimal dayHours = 0, nightHours = 0;
        var monthlyHours = new Dictionary<int, decimal>();
        int totalLines = 0;

        foreach (var emp in employees)
        {
            decimal empTotal = 0;

            foreach (var m in monthsToProcess)
            {
                try
                {
                    var result = _otSvc.BuildMonthlyClaim(calendarId, emp.Id, m);

                    foreach (var line in result.ClaimLines)
                    {
                        empTotal += line.Hours;
                        totalLines++;

                        categoryHours.TryAdd(line.Category, 0);
                        categoryHours[line.Category] += line.Hours;

                        if (line.Band == RateBand.Day) dayHours  += line.Hours;
                        else                           nightHours += line.Hours;

                        monthlyHours.TryAdd(m, 0);
                        monthlyHours[m] += line.Hours;
                    }
                }
                catch (InvalidOperationException)
                {
                    // Employee not assigned to a group — skip
                }
            }

            if (empTotal > 0)
                employeeHours[emp.Name] = empTotal;
        }

        static string CatLabel(OtCategory c) => c switch
        {
            OtCategory.WorkingDay         => "Working Day",
            OtCategory.KelepasanGiliran   => "Kelepasan Giliran",
            OtCategory.KelepasanAm        => "Kelepasan Am",
            OtCategory.KelepasanAmGantian => "Kelepasan Am Gantian",
            _                             => c.ToString()
        };

        var topEmployees = employeeHours
            .OrderByDescending(x => x.Value)
            .Take(10)
            .Select(x => (x.Key, x.Value))
            .ToList();

        var catList = categoryHours
            .OrderByDescending(x => x.Value)
            .Select(x => (CatLabel(x.Key), x.Value))
            .ToList();

        var monthList = monthlyHours
            .OrderBy(x => x.Key)
            .Select(x => (x.Key, x.Value))
            .ToList();

        return new StatsData(
            TotalOtHours:    employeeHours.Values.Sum(),
            TotalClaimLines: totalLines,
            EmployeesWithOt: employeeHours.Count,
            HighestHours:    topEmployees.Count > 0 ? topEmployees[0].Value : 0,
            TopEmployees:    topEmployees,
            CategoryHours:   catList,
            DayHours:        dayHours,
            NightHours:      nightHours,
            MonthlyHours:    monthList,
            ElapsedMs:       sw.ElapsedMilliseconds);
    }

    private void RenderStats(StatsData d)
    {
        TotalShiftsText.Text       = $"{d.TotalOtHours:N1}h";
        TotalReplacementsText.Text = d.TotalClaimLines.ToString();
        TotalEmptyFillsText.Text   = d.EmployeesWithOt.ToString();
        TotalPHsText.Text          = $"{d.HighestHours:N1}h";

        TopShiftsChart.Model       = BuildHorizontalBar("OT Hours per Employee",
                                         d.TopEmployees.Select(x => (x.Name, (double)x.Hours)).ToList(),
                                         OxyColor.FromRgb(66, 165, 245));

        ShiftTypeChart.Model       = BuildCategoryPie(
                                         d.CategoryHours.Select(x => (x.Label, (double)x.Hours)).ToList());

        TopReplacementsChart.Model = BuildBandPie((double)d.DayHours, (double)d.NightHours);

        MonthlyTrendChart.Model    = BuildMonthlyTrend(
                                         d.MonthlyHours.Select(x => (x.Month, (double)x.Hours)).ToList());
    }

    // ── Chart builders ─────────────────────────────────────────────────

    private static PlotModel BuildHorizontalBar(string title, List<(string Name, double Hours)> data, OxyColor color)
    {
        var model = new PlotModel { Title = title, TitleFontSize = 12 };
        if (data.Count == 0) { model.Subtitle = "No OT data"; return model; }

        var items = data.AsEnumerable().Reverse().ToList();

        model.Axes.Add(new CategoryAxis
        {
            Position = AxisPosition.Left,
            FontSize = 11,
            ItemsSource = items.Select(x => x.Name).ToArray()
        });
        model.Axes.Add(new LinearAxis
        {
            Position = AxisPosition.Bottom,
            MinimumPadding = 0,
            AbsoluteMinimum = 0,
            Title = "Hours"
        });

        var series = new BarSeries
        {
            LabelPlacement = LabelPlacement.Inside,
            LabelFormatString = "{0:N1}",
            FillColor = color,
            TextColor = OxyColors.White
        };
        foreach (var item in items)
            series.Items.Add(new BarItem { Value = item.Hours });

        model.Series.Add(series);
        return model;
    }

    private static PlotModel BuildCategoryPie(List<(string Label, double Hours)> data)
    {
        var model = new PlotModel { Title = "OT Hours by Category", TitleFontSize = 12 };
        if (data.Count == 0) { model.Subtitle = "No OT data"; return model; }

        OxyColor[] colors =
        [
            OxyColor.FromRgb( 68, 114, 196),
            OxyColor.FromRgb(237, 125,  49),
            OxyColor.FromRgb(165, 165, 165),
            OxyColor.FromRgb(112,  48, 160)
        ];

        var series = new PieSeries { StrokeThickness = 1.5, InsideLabelPosition = 0.6, AngleSpan = 360, StartAngle = 0 };
        for (int i = 0; i < data.Count; i++)
        {
            var (label, hours) = data[i];
            if (hours > 0)
                series.Slices.Add(new PieSlice($"{label}\n({hours:N1}h)", hours)
                    { Fill = colors[i % colors.Length] });
        }

        model.Series.Add(series);
        return model;
    }

    private static PlotModel BuildBandPie(double dayHours, double nightHours)
    {
        var model = new PlotModel { Title = "Day vs Night OT Hours", TitleFontSize = 12 };
        if (dayHours + nightHours == 0) { model.Subtitle = "No OT data"; return model; }

        var series = new PieSeries { StrokeThickness = 1.5, InsideLabelPosition = 0.65, AngleSpan = 360, StartAngle = 0 };
        if (dayHours   > 0) series.Slices.Add(new PieSlice($"Day ({dayHours:N1}h)",     dayHours)   { Fill = OxyColor.FromRgb(255, 193,   7) });
        if (nightHours > 0) series.Slices.Add(new PieSlice($"Night ({nightHours:N1}h)", nightHours) { Fill = OxyColor.FromRgb( 63,  81, 181) });

        model.Series.Add(series);
        return model;
    }

    private static PlotModel BuildMonthlyTrend(List<(int Month, double Hours)> data)
    {
        var model = new PlotModel { Title = "Monthly OT Hours (Full Year)", TitleFontSize = 12 };
        string[] monthNames = ["Jan", "Feb", "Mar", "Apr", "May", "Jun",
                                "Jul", "Aug", "Sep", "Oct", "Nov", "Dec"];

        var xAxis = new CategoryAxis { Position = AxisPosition.Bottom, FontSize = 11 };
        foreach (var name in monthNames) xAxis.Labels.Add(name);
        model.Axes.Add(xAxis);

        model.Axes.Add(new LinearAxis
        {
            Position = AxisPosition.Left,
            MinimumPadding = 0.1,
            AbsoluteMinimum = 0,
            Title = "Hours"
        });

        var series = new LineSeries
        {
            MarkerType = MarkerType.Circle,
            MarkerSize = 4,
            Color = OxyColor.FromRgb(66, 165, 245),
            StrokeThickness = 2
        };

        for (int m = 1; m <= 12; m++)
        {
            var found = data.FirstOrDefault(x => x.Month == m);
            series.Points.Add(new DataPoint(m - 1, found.Month > 0 ? found.Hours : 0));
        }

        model.Series.Add(series);
        return model;
    }

    private void Close_Click(object sender, RoutedEventArgs e) => Close();
}
