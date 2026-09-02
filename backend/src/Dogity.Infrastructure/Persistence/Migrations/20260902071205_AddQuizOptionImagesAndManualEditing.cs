using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Dogity.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddQuizOptionImagesAndManualEditing : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "EditedAt",
                table: "quiz_questions",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "EditedByUserId",
                table: "quiz_questions",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ImageName",
                table: "quiz_options",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_quiz_questions_EditedAt",
                table: "quiz_questions",
                column: "EditedAt");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_quiz_questions_EditedAt",
                table: "quiz_questions");

            migrationBuilder.DropColumn(
                name: "EditedAt",
                table: "quiz_questions");

            migrationBuilder.DropColumn(
                name: "EditedByUserId",
                table: "quiz_questions");

            migrationBuilder.DropColumn(
                name: "ImageName",
                table: "quiz_options");
        }
    }
}
