using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Dogity.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddExamSubmissionId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "SubmissionId",
                table: "quiz_exam_attempts",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_quiz_exam_attempts_UserId_SubmissionId",
                table: "quiz_exam_attempts",
                columns: new[] { "UserId", "SubmissionId" },
                unique: true,
                filter: "\"SubmissionId\" IS NOT NULL AND \"DeletedAt\" IS NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_quiz_exam_attempts_UserId_SubmissionId",
                table: "quiz_exam_attempts");

            migrationBuilder.DropColumn(
                name: "SubmissionId",
                table: "quiz_exam_attempts");
        }
    }
}
