using Blue4Learn.Web.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Blue4Learn.Web.Data.Migrations
{
    [DbContext(typeof(ApplicationDbContext))]
    [Migration("20260930160000_LessonCreatedAt")]
    public class LessonCreatedAt : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedAtUtc",
                table: "Lessons",
                type: "TEXT",
                nullable: false,
                defaultValue: new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc));

            // Melhor aproximação da criação: data do documento, depois LessonDate.
            migrationBuilder.Sql("""
                UPDATE Lessons
                SET CreatedAtUtc = COALESCE(
                    (SELECT cd.UpdatedAtUtc FROM ContentDocuments cd WHERE cd.LessonId = Lessons.Id),
                    LessonDate,
                    CreatedAtUtc
                );
                """);

            migrationBuilder.CreateIndex(
                name: "IX_Lessons_ClassGroupId_CreatedAtUtc",
                table: "Lessons",
                columns: new[] { "ClassGroupId", "CreatedAtUtc" });
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Lessons_ClassGroupId_CreatedAtUtc",
                table: "Lessons");

            migrationBuilder.DropColumn(
                name: "CreatedAtUtc",
                table: "Lessons");
        }
    }
}
