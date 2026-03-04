using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OTTimetableApp.Migrations
{
    /// <inheritdoc />
    public partial class Unique_GroupMember_Employee : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                ALTER TABLE `GroupMembers`
                ADD UNIQUE INDEX `UX_GroupMembers_Group_Slot` (`GroupId`, `SlotIndex`);
                ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"ALTER TABLE `GroupMembers` DROP INDEX `UX_GroupMembers_EmployeeId`;");
            migrationBuilder.Sql(@"ALTER TABLE `GroupMembers` DROP INDEX `UX_GroupMembers_Group_Slot`;");
        }
    }
}
