using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Biblioteka.Migrations
{
    /// <inheritdoc />
    public partial class UpdateBorrowingSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsProlonged",
                table: "Borrowings");

            migrationBuilder.AddColumn<string>(
                name: "Comments",
                table: "Borrowings",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Comments",
                table: "Borrowings");

            migrationBuilder.AddColumn<bool>(
                name: "IsProlonged",
                table: "Borrowings",
                type: "bit",
                nullable: false,
                defaultValue: false);
        }
    }
}
