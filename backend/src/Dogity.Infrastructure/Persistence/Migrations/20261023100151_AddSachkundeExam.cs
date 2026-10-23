using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Dogity.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddSachkundeExam : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ExamQuestionCount",
                table: "quiz_catalogs",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "quiz_exam_attempts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    CatalogId = table.Column<Guid>(type: "uuid", nullable: false),
                    SubmittedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Points = table.Column<int>(type: "integer", nullable: false),
                    MaxPoints = table.Column<int>(type: "integer", nullable: false),
                    Passed = table.Column<bool>(type: "boolean", nullable: false),
                    QuestionCount = table.Column<int>(type: "integer", nullable: false),
                    CorrectCount = table.Column<int>(type: "integer", nullable: false),
                    SectionResultsJson = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    DeletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_quiz_exam_attempts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_quiz_exam_attempts_quiz_catalogs_CatalogId",
                        column: x => x.CatalogId,
                        principalTable: "quiz_catalogs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_quiz_exam_attempts_CatalogId",
                table: "quiz_exam_attempts",
                column: "CatalogId");

            migrationBuilder.CreateIndex(
                name: "IX_quiz_exam_attempts_UserId_CatalogId_SubmittedAt",
                table: "quiz_exam_attempts",
                columns: new[] { "UserId", "CatalogId", "SubmittedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "quiz_exam_attempts");

            migrationBuilder.DropColumn(
                name: "ExamQuestionCount",
                table: "quiz_catalogs");
        }
    }
}
