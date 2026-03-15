using ClosedXML.Excel;
using OTTimetableApp.Data.Models;
using System.IO;
using Microsoft.EntityFrameworkCore;
using OTTimetableApp.Data;
using OTTimetableApp.ViewModels;

namespace OTTimetableApp.Services;

public class ExcelExportService
{
    private readonly EmployeeService _employeeService;
    private readonly IDbContextFactory<AppDbContext> _dbFactory;

    public ExcelExportService(EmployeeService employeeService, IDbContextFactory<AppDbContext> dbFactory)
    {
        _employeeService = employeeService;
        _dbFactory = dbFactory;
    }

    public void ExportClaim(
        int calendarId,
        int month,
        int employeeId,
        decimal hourlyRate,
        decimal excessWorkingHours,
        List<ClaimLineVM> claimLines,
        string outputPath)
    {
        var templatePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Template", "ot_template.xlsx");

        if (!File.Exists(templatePath))
            throw new FileNotFoundException("Template file not found", templatePath);

        var employee = _employeeService.GetAll().FirstOrDefault(e => e.Id == employeeId);
        if (employee == null)
            throw new InvalidOperationException("Employee not found");

        // Get calendar year
        int year;
        using (var db = _dbFactory.CreateDbContext())
        {
            var calendar = db.Calendars.AsNoTracking().FirstOrDefault(c => c.Id == calendarId);
            if (calendar == null)
                throw new InvalidOperationException("Calendar not found");
            year = calendar.Year;
        }

        using var workbook = new XLWorkbook(templatePath);
        var worksheet = workbook.Worksheet(1); // First worksheet

        // Fill month and year in cell A5
        var monthName = GetMalayMonthName(month);
        worksheet.Cell("A5").Value = $"{monthName} {year}".ToUpper();

        // Fill employee information
        worksheet.Cell("C8").Value = employee.FullName?.ToUpper();
        worksheet.Cell("C9").Value = employee.IcNo?.ToUpper();
        worksheet.Cell("C10").Value = employee.PikNo?.ToUpper();
        worksheet.Cell("C11").Value = employee.Branch?.ToUpper();
        worksheet.Cell("C12").Value = employee.PhoneNo?.ToUpper();
        worksheet.Cell("L8").Value = employee.Salary;
        worksheet.Cell("L9").Value = hourlyRate;
        worksheet.Cell("L11").Value = employee.SalaryNo?.ToUpper();
        worksheet.Cell("L12").Value = employee.BankAccountNo?.ToUpper();
        worksheet.Cell("L13").Value = employee.BankName?.ToUpper();

        // Fill Lebihan Jam Bertugas (Excess Working Hours)
        worksheet.Cell("F18").Value = excessWorkingHours;

        // Fill OT claim lines (only checked lines)
        var checkedLines = claimLines.Where(l => l.IsChecked).ToList();
        FillOtLines(worksheet, checkedLines);

        workbook.SaveAs(outputPath);
    }

    private static void FillOtLines(IXLWorksheet worksheet, List<ClaimLineVM> lines)
    {
        // Group by date to ensure each date appears only once
        var groupedByDate = lines
            .GroupBy(l => l.Date)
            .OrderBy(g => g.Key)
            .ToList();

        int currentRow = 20; // Start at row 20

        foreach (var dateGroup in groupedByDate)
        {
            var date = dateGroup.Key;
            int startRow = currentRow;

            // Row 1: Date (formatted as dd/MM/yyyy) in Column A
            worksheet.Cell(startRow, 1).Value = date.ToString("dd/MM/yyyy");

            // Row 1: Category in Column B
            // If multiple lines for same date have different categories, concatenate them
            var categories = dateGroup.Select(l => l.Category).Distinct().ToList();
            var categoryText = string.Join(", ", categories);
            worksheet.Cell(startRow, 2).Value = categoryText?.ToUpper();

            // Row 2: Day name in Malay in Column A
            var dayName = GetMalayDayName(date.DayOfWeek);
            worksheet.Cell(startRow + 1, 1).Value = dayName.ToUpper();

            // Fill OT shifts and rates for this date
            int shiftRow = startRow;
            foreach (var line in dateGroup)
            {
                // Column D: Shift time (e.g., "0700-1500")
                worksheet.Cell(shiftRow, 4).Value = line.Shift?.ToUpper();

                // Column F: Total OT hours (duration of shift)
                var totalHours = CalculateShiftHours(line.Shift);
                worksheet.Cell(shiftRow, 6).Value = totalHours; // Column F

                // Fill rate columns (G=1.125, H=1.25, I=1.5, J=1.75, K=2.0)
                if (line.H1125.HasValue && line.H1125.Value > 0)
                    worksheet.Cell(shiftRow, 7).Value = line.H1125.Value; // Column G

                if (line.H125.HasValue && line.H125.Value > 0)
                    worksheet.Cell(shiftRow, 8).Value = line.H125.Value; // Column H

                if (line.H15.HasValue && line.H15.Value > 0)
                    worksheet.Cell(shiftRow, 9).Value = line.H15.Value; // Column I

                if (line.H175.HasValue && line.H175.Value > 0)
                    worksheet.Cell(shiftRow, 10).Value = line.H175.Value; // Column J

                if (line.H20.HasValue && line.H20.Value > 0)
                    worksheet.Cell(shiftRow, 11).Value = line.H20.Value; // Column K

                // Column S: Remark (2 rows above the OT line)
                if (!string.IsNullOrWhiteSpace(line.Remark))
                    worksheet.Cell(shiftRow - 2, 19).Value = line.Remark?.ToUpper(); // Column S

                shiftRow++;
            }

            // Move to next date (at least 2 rows for date + day name)
            currentRow = startRow + Math.Max(2, dateGroup.Count());
        }
    }

    private static string GetMalayDayName(DayOfWeek dayOfWeek)
    {
        return dayOfWeek switch
        {
            DayOfWeek.Monday => "Isnin",
            DayOfWeek.Tuesday => "Selasa",
            DayOfWeek.Wednesday => "Rabu",
            DayOfWeek.Thursday => "Khamis",
            DayOfWeek.Friday => "Jumaat",
            DayOfWeek.Saturday => "Sabtu",
            DayOfWeek.Sunday => "Ahad",
            _ => throw new ArgumentOutOfRangeException(nameof(dayOfWeek))
        };
    }

    private static decimal CalculateShiftHours(string? shift)
    {
        if (string.IsNullOrWhiteSpace(shift))
            return 0;

        // Expected format: "07:00-15:00" or "14:00-23:00" or "22:00-00:00"
        var parts = shift.Split('-');
        if (parts.Length != 2)
            return 0;

        if (!TimeOnly.TryParse(parts[0].Trim(), out var startTime) || 
            !TimeOnly.TryParse(parts[1].Trim(), out var endTime))
            return 0;

        // Convert to decimal hours
        decimal startHour = startTime.Hour + startTime.Minute / 60m;
        decimal endHour = endTime.Hour + endTime.Minute / 60m;

        // Handle overnight shifts (e.g., 22:00-00:00, 23:00-07:00)
        if (endHour <= startHour)
            endHour += 24;

        return endHour - startHour;
    }

    private static string GetMalayMonthName(int month)
    {
        return month switch
        {
            1 => "Januari",
            2 => "Februari",
            3 => "Mac",
            4 => "April",
            5 => "Mei",
            6 => "Jun",
            7 => "Julai",
            8 => "Ogos",
            9 => "September",
            10 => "Oktober",
            11 => "November",
            12 => "Disember",
            _ => throw new ArgumentOutOfRangeException(nameof(month), "Month must be between 1 and 12")
        };
    }
}
