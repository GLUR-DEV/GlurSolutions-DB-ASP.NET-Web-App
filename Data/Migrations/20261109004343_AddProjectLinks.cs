using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GlurSolutions.Web.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddProjectLinks : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "DesignNotesUrl",
                table: "Projects",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LiveSiteUrl",
                table: "Projects",
                type: "nvarchar(max)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DesignNotesUrl",
                table: "Projects");

            migrationBuilder.DropColumn(
                name: "LiveSiteUrl",
                table: "Projects");
        }
    }
}
