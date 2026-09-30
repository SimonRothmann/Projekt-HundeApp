using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Dogity.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddGoalExamResult : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateOnly>(
                name: "ExamDate",
                table: "goals",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ExamNote",
                table: "goals",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ExamScore",
                table: "goals",
                type: "integer",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ExamDate",
                table: "goals");

            migrationBuilder.DropColumn(
                name: "ExamNote",
                table: "goals");

            migrationBuilder.DropColumn(
                name: "ExamScore",
                table: "goals");
        }
    }
}
