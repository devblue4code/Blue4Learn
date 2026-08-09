using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Blue4Learn.Web.Data.Migrations
{
    /// <inheritdoc />
    public partial class CourseSyllabusAndMethodologies : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Methodologies",
                table: "Courses",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Syllabus",
                table: "Courses",
                type: "TEXT",
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Methodologies",
                table: "Courses");

            migrationBuilder.DropColumn(
                name: "Syllabus",
                table: "Courses");
        }
    }
}
