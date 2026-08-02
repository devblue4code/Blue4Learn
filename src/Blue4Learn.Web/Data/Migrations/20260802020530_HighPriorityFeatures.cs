using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Blue4Learn.Web.Data.Migrations
{
    /// <inheritdoc />
    public partial class HighPriorityFeatures : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "SubmissionAttachments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    SubmissionId = table.Column<Guid>(type: "TEXT", nullable: false),
                    OriginalFileName = table.Column<string>(type: "TEXT", maxLength: 260, nullable: false),
                    StoredFileName = table.Column<string>(type: "TEXT", maxLength: 80, nullable: false),
                    ContentType = table.Column<string>(type: "TEXT", maxLength: 120, nullable: false),
                    SizeBytes = table.Column<long>(type: "INTEGER", nullable: false),
                    UploadedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SubmissionAttachments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SubmissionAttachments_ActivitySubmissions_SubmissionId",
                        column: x => x.SubmissionId,
                        principalTable: "ActivitySubmissions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SubmissionAttachments_SubmissionId",
                table: "SubmissionAttachments",
                column: "SubmissionId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SubmissionAttachments");
        }
    }
}
