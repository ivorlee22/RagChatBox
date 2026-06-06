using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RagChatBox.DAL.Migrations
{
    /// <inheritdoc />
    public partial class AddUploadedByToDocument : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "UploadedBy",
                table: "Document",
                type: "character varying(255)",
                maxLength: 255,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "UploadedBy",
                table: "Document");
        }
    }
}
