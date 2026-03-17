using Microsoft.EntityFrameworkCore;
using OTTimetableApp.Data;
using OTTimetableApp.ViewModels;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace OTTimetableApp.Services;

public class PdfExportService
{
    private readonly IDbContextFactory<AppDbContext> _dbFactory;
    private readonly MonthViewService _monthViewSvc;

    public PdfExportService(IDbContextFactory<AppDbContext> dbFactory, MonthViewService monthViewSvc)
    {
        _dbFactory = dbFactory;
        _monthViewSvc = monthViewSvc;
        
        // Required for QuestPDF
        QuestPDF.Settings.License = LicenseType.Community;
    }

    public void ExportToPdf(int calendarId, DateOnly startDate, DateOnly endDate, string outputPath)
    {
        using var db = _dbFactory.CreateDbContext();
        
        // Get calendar info
        var calendar = db.Calendars.AsNoTracking().FirstOrDefault(c => c.Id == calendarId);
        if (calendar == null)
            throw new Exception("Calendar not found");

        // Get all days in the date range
        var days = new List<DayRowVM>();
        var currentDate = startDate;
        while (currentDate <= endDate)
        {
            var year = currentDate.Year;
            var month = currentDate.Month;
            var (_, dayRows) = _monthViewSvc.LoadMonth(calendarId, month);

            // Filter to the specific date range
            var filteredDays = dayRows.Where(d => d.Date >= startDate && d.Date <= endDate).ToList();
            days.AddRange(filteredDays);

            // Move to next month
            var nextMonth = new DateOnly(year, month, 1).AddMonths(1);
            currentDate = new DateOnly(nextMonth.Year, nextMonth.Month, 1);

            // Break if we've passed the end date
            if (currentDate > endDate)
                break;
        }

        // Generate PDF
        Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4.Landscape());
                page.Margin(20);

                page.Header().Column(column =>
                {
                    column.Item().AlignCenter().Text($"OT Timetable - {calendar.Name}")
                        .FontSize(18).Bold();
                    column.Item().AlignCenter().Text($"{startDate:dd/MM/yyyy} - {endDate:dd/MM/yyyy}")
                        .FontSize(12);
                });

                page.Content().Table(table =>
                {
                    // Define columns
                    table.ColumnsDefinition(columns =>
                    {
                        columns.ConstantColumn(80);  // Date
                        columns.RelativeColumn(1);   // Night
                        columns.RelativeColumn(1);   // Morning
                        columns.RelativeColumn(1);   // Evening
                    });

                    // Header
                    table.Header(header =>
                    {
                        header.Cell().Background(Colors.Grey.Lighten2).Padding(5)
                            .Text("Tarikh").Bold().FontSize(10);
                        header.Cell().Background(Colors.Grey.Lighten2).Padding(5)
                            .Text("22:00 - 07:00").Bold().FontSize(10);
                        header.Cell().Background(Colors.Grey.Lighten2).Padding(5)
                            .Text("07:00 - 15:00").Bold().FontSize(10);
                        header.Cell().Background(Colors.Grey.Lighten2).Padding(5)
                            .Text("14:00 - 23:00").Bold().FontSize(10);
                    });

                    // Rows
                    foreach (var day in days)
                    {
                        // Date column
                        table.Cell().Border(1).Padding(5)
                            .Column(column =>
                            {
                                column.Item().Text(day.DateDisplay).FontSize(9).Bold();
                                column.Item().Text(day.DayName).FontSize(8);
                                
                                if (day.IsPublicHoliday)
                                {
                                    column.Item().Text("PUBLIC HOLIDAY")
                                        .FontSize(7).Bold().FontColor(Colors.Red.Medium);
                                    if (!string.IsNullOrEmpty(day.PublicHolidayName))
                                        column.Item().Text(day.PublicHolidayName)
                                            .FontSize(7).Italic().FontColor(Colors.Red.Darken2);
                                }
                            });

                        // Night shift
                        table.Cell().Border(1).Padding(5)
                            .Column(column => RenderShift(column, day.Night));

                        // Morning shift
                        table.Cell().Border(1).Padding(5)
                            .Column(column => RenderShift(column, day.Morning));

                        // Evening shift
                        table.Cell().Border(1).Padding(5)
                            .Column(column => RenderShift(column, day.Evening));
                    }
                });

                page.Footer()
                    .AlignCenter()
                    .Text(text =>
                    {
                        text.Span("Page ");
                        text.CurrentPageNumber();
                        text.Span(" of ");
                        text.TotalPages();
                    });
            });
        })
        .GeneratePdf(outputPath);
    }

    private void RenderShift(ColumnDescriptor column, ShiftVM shift)
    {
        // Group name header
        var bgColor = GetGroupColor(shift.GroupId);
        column.Item().Background(bgColor).Padding(3)
            .Text(shift.GroupName).FontSize(8).Bold();

        // Slots
        foreach (var slot in shift.Slots)
        {
            column.Item().PaddingTop(2).Row(row =>
            {
                row.AutoItem().Width(15).Text($"{slot.SlotIndex}.").FontSize(8);
                
                row.AutoItem().Width(50).PaddingLeft(2)
                    .Background(GetStatusColor(slot.FillType))
                    .Padding(2)
                    .Text(slot.StatusText).FontSize(7).Bold();

                row.RelativeItem().PaddingLeft(3)
                    .Text(GetEmployeeName(slot)).FontSize(8);
            });

            // Show replacement or on leave info
            if (!string.IsNullOrEmpty(slot.ReplacesName))
            {
                column.Item().PaddingLeft(18).Text($"Replaces: {slot.ReplacesName}")
                    .FontSize(7).Italic().FontColor(Colors.Grey.Darken1);
            }
            
            if (!string.IsNullOrEmpty(slot.OnLeaveName))
            {
                column.Item().PaddingLeft(18).Text($"On Leave: {slot.OnLeaveName}")
                    .FontSize(7).Italic().FontColor(Colors.Orange.Medium);
            }
        }
    }

    private string GetEmployeeName(ShiftSlotVM slot)
    {
        var empId = slot.ActualEmployeeId ?? 0;
        if (empId == 0) return "(None)";
        
        var emp = slot.EmployeeOptions.FirstOrDefault(e => e.Id == empId);
        return emp?.Name ?? $"Employee #{empId}";
    }

    private string GetGroupColor(int groupId)
    {
        return groupId switch
        {
            1 => "#FFE5B4",  // Group A - Peach
            2 => "#B4E5FF",  // Group B - Light Blue
            3 => "#FFB4E5",  // Group C - Light Pink
            4 => "#B4FFE5",  // Group D - Light Mint
            _ => "#E0E0E0"   // Default - Light Gray
        };
    }

    private string GetStatusColor(Data.Models.SlotFillType fillType)
    {
        return fillType switch
        {
            Data.Models.SlotFillType.Planned => "#90EE90",      // LightGreen
            Data.Models.SlotFillType.Replacement => "#F0E68C",  // Khaki
            Data.Models.SlotFillType.EmptyFill => "#87CEEB",    // LightSkyBlue
            _ => "#D3D3D3"                                       // LightGray
        };
    }
}
