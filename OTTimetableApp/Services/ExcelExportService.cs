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
        worksheet.Cell("A5").Value = $"{monthName} {year}";

        // Fill employee information
        worksheet.Cell("C8").Value = employee.FullName;
        worksheet.Cell("C9").Value = employee.IcNo;
        worksheet.Cell("C10").Value = employee.PikNo;
        worksheet.Cell("C11").Value = employee.Branch;
        worksheet.Cell("C12").Value = employee.PhoneNo;
        worksheet.Cell("L8").Value = employee.Salary;
        worksheet.Cell("L9").Value = hourlyRate;
        worksheet.Cell("L11").Value = employee.SalaryNo;
        worksheet.Cell("L12").Value = employee.BankAccountNo;
        worksheet.Cell("L13").Value = employee.BankName;

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

            // Row 1: Date (formatted as dd/MM/yyyy) in Column A
            worksheet.Cell(currentRow, 1).Value = date.ToString("dd/MM/yyyy");

            // Row 1: Category in Column B
            // If multiple lines for same date have different categories, concatenate them
            var categories = dateGroup.Select(l => l.Category).Distinct().ToList();
            var categoryText = string.Join(", ", categories);
            worksheet.Cell(currentRow, 2).Value = categoryText;

            // Row 2: Day name in Malay in Column A
            var dayName = GetMalayDayName(date.DayOfWeek);
            worksheet.Cell(currentRow + 1, 1).Value = dayName;

            // Move to next date (skip 2 rows)
            currentRow += 2;
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
