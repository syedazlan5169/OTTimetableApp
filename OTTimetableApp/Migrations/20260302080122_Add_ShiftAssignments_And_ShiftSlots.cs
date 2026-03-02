using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OTTimetableApp.Migrations
{
    /// <inheritdoc />
    public partial class Add_ShiftAssignments_And_ShiftSlots : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ShiftAssignments",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    CalendarDayId = table.Column<int>(type: "int", nullable: false),
                    ShiftType = table.Column<int>(type: "int", nullable: false),
                    GroupId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ShiftAssignments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ShiftAssignments_CalendarDays_CalendarDayId",
                        column: x => x.CalendarDayId,
                        principalTable: "CalendarDays",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ShiftAssignments_Groups_GroupId",
                        column: x => x.GroupId,
                        principalTable: "Groups",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "ShiftSlots",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    ShiftAssignmentId = table.Column<int>(type: "int", nullable: false),
                    SlotIndex = table.Column<int>(type: "int", nullable: false),
                    PlannedEmployeeId = table.Column<int>(type: "int", nullable: true),
                    ActualEmployeeId = table.Column<int>(type: "int", nullable: true),
                    ReplacedEmployeeId = table.Column<int>(type: "int", nullable: true),
                    FillType = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ShiftSlots", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ShiftSlots_Employees_ActualEmployeeId",
                        column: x => x.ActualEmployeeId,
                        principalTable: "Employees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_ShiftSlots_Employees_PlannedEmployeeId",
                        column: x => x.PlannedEmployeeId,
                        principalTable: "Employees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_ShiftSlots_Employees_ReplacedEmployeeId",
                        column: x => x.ReplacedEmployeeId,
                        principalTable: "Employees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_ShiftSlots_ShiftAssignments_ShiftAssignmentId",
                        column: x => x.ShiftAssignmentId,
                        principalTable: "ShiftAssignments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_ShiftAssignments_CalendarDayId_ShiftType",
                table: "ShiftAssignments",
                columns: new[] { "CalendarDayId", "ShiftType" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ShiftAssignments_GroupId",
                table: "ShiftAssignments",
                column: "GroupId");

            migrationBuilder.CreateIndex(
                name: "IX_ShiftSlots_ActualEmployeeId",
                table: "ShiftSlots",
                column: "ActualEmployeeId");

            migrationBuilder.CreateIndex(
                name: "IX_ShiftSlots_PlannedEmployeeId",
                table: "ShiftSlots",
                column: "PlannedEmployeeId");

            migrationBuilder.CreateIndex(
                name: "IX_ShiftSlots_ReplacedEmployeeId",
                table: "ShiftSlots",
                column: "ReplacedEmployeeId");

            migrationBuilder.CreateIndex(
                name: "IX_ShiftSlots_ShiftAssignmentId_SlotIndex",
                table: "ShiftSlots",
                columns: new[] { "ShiftAssignmentId", "SlotIndex" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ShiftSlots");

            migrationBuilder.DropTable(
                name: "ShiftAssignments");
        }
    }
}
