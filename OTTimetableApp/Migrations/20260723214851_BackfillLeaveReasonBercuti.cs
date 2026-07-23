using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OTTimetableApp.Migrations
{
    /// <inheritdoc />
    public partial class BackfillLeaveReasonBercuti : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Default existing Replacement slots (created before LeaveReason existed) to "Bercuti"
            migrationBuilder.Sql(
                "UPDATE `ShiftSlots` SET `LeaveReason` = 1 WHERE `FillType` = 2 AND `LeaveReason` IS NULL;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {

        }
    }
}
