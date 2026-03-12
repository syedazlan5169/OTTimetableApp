using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OTTimetableApp.Migrations
{
    /// <inheritdoc />
    public partial class AddEmployeeBaseGroup_Nullable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "BaseGroupId",
                table: "Employees",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Employees_BaseGroupId",
                table: "Employees",
                column: "BaseGroupId");

            migrationBuilder.AddForeignKey(
                name: "FK_Employees_Groups_BaseGroupId",
                table: "Employees",
                column: "BaseGroupId",
                principalTable: "Groups",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Employees_Groups_BaseGroupId",
                table: "Employees");

            migrationBuilder.DropIndex(
                name: "IX_Employees_BaseGroupId",
                table: "Employees");

            migrationBuilder.DropColumn(
                name: "BaseGroupId",
                table: "Employees");
        }
    }
}
