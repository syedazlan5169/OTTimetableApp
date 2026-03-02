using MySqlConnector;
using OTTimetableApp.Data;
using OTTimetableApp.Infrastructure;
using OTTimetableApp.Services;
using OTTimetableApp.Data.Models;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace OTTimetableApp
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();

            TestGenerateCalendar();
        }

private void TestGenerateCalendar()
    {
        using var db = new AppDbContext(AppDbContext.BuildOptions());

        // Create a sample calendar if none exists
        var cal = db.Calendars.FirstOrDefault(c => c.Year == 2026 && c.Name == "Test 2026");
        if (cal == null)
        {
            var groups = db.Groups.OrderBy(g => g.Name).ToList();

            // Default: Night=A, Morning=B, Evening=C (match your earlier example)
            var gA = groups.First(g => g.Name == "KUMPULAN A").Id;
            var gB = groups.First(g => g.Name == "KUMPULAN B").Id;
            var gC = groups.First(g => g.Name == "KUMPULAN C").Id;

            cal = new Data.Models.Calendar
            {
                Name = "Test 2026",
                Year = 2026,
                InitNightGroupId = gA,
                InitMorningGroupId = gB,
                InitEveningGroupId = gC,
                IsGenerated = false
            };

            db.Calendars.Add(cal);
            db.SaveChanges();
        }

        var gen = new CalendarGeneratorService(db);
        gen.GenerateYear(cal.Id);

        MessageBox.Show($"Generated calendar: {cal.Name}. Days={db.CalendarDays.Count(d => d.CalendarId == cal.Id)}");
    }
}
}