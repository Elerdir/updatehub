using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UpdateHub.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddReleaseMinFromVersion : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "MinFromVersion",
                table: "Releases",
                type: "TEXT",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "MinFromVersion",
                table: "Releases");
        }
    }
}
