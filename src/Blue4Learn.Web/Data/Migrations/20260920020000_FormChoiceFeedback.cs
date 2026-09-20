using Blue4Learn.Web.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Blue4Learn.Web.Data.Migrations
{
    [DbContext(typeof(ApplicationDbContext))]
    [Migration("20260920020000_FormChoiceFeedback")]
    public class FormChoiceFeedback : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CorrectOptionsJson",
                table: "FormQuestions",
                type: "TEXT",
                maxLength: 2000,
                nullable: false,
                defaultValue: "[]");

            migrationBuilder.AddColumn<string>(
                name: "FeedbackCorrect",
                table: "FormQuestions",
                type: "TEXT",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FeedbackIncorrect",
                table: "FormQuestions",
                type: "TEXT",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsCorrect",
                table: "FormAnswers",
                type: "INTEGER",
                nullable: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "CorrectOptionsJson", table: "FormQuestions");
            migrationBuilder.DropColumn(name: "FeedbackCorrect", table: "FormQuestions");
            migrationBuilder.DropColumn(name: "FeedbackIncorrect", table: "FormQuestions");
            migrationBuilder.DropColumn(name: "IsCorrect", table: "FormAnswers");
        }
    }
}
