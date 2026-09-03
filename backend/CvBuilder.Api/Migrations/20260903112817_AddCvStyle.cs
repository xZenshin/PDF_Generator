using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CvBuilder.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddCvStyle : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Style",
                table: "Cvs",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Style",
                table: "Cvs");
        }
    }
}
