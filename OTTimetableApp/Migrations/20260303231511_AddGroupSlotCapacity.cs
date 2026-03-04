using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OTTimetableApp.Migrations
{
    /// <inheritdoc />
    public partial class AddGroupSlotCapacity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "SlotCapacity",
                table: "Groups",
                type: "int",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SlotCapacity",
                table: "Groups");
        }
    }
}
