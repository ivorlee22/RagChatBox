using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RagChatBox.DAL.Migrations
{
    /// <inheritdoc />
    public partial class UpdateTeacherSeedPassword : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                table: "User",
                keyColumn: "Id",
                keyValue: 3,
                column: "PasswordHash",
                value: "$2a$11$4lFfFCFk4hkkjAnrT8ZJ0eP3ERrExJJzOJu2H1uMbcRic9SWbo2/6");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                table: "User",
                keyColumn: "Id",
                keyValue: 3,
                column: "PasswordHash",
                value: "$2a$11$mPGtGkISvNXRmcu0tra3QeYSHAdzB4g85xSTlmJKnDqCTUNV7NW/u");
        }
    }
}
