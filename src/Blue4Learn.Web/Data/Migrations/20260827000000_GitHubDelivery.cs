using Blue4Learn.Web.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Blue4Learn.Web.Data.Migrations
{
    [DbContext(typeof(ApplicationDbContext))]
    [Migration("20260827000000_GitHubDelivery")]
    public class GitHubDelivery : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "RequiresGitHubDelivery",
                table: "Activities",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "DeliveryNote",
                table: "ActivitySubmissions",
                type: "TEXT",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "GitHubPrUrl",
                table: "ActivitySubmissions",
                type: "TEXT",
                maxLength: 500,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "RequiresGitHubDelivery",
                table: "Activities");

            migrationBuilder.DropColumn(
                name: "DeliveryNote",
                table: "ActivitySubmissions");

            migrationBuilder.DropColumn(
                name: "GitHubPrUrl",
                table: "ActivitySubmissions");
        }
    }
}
