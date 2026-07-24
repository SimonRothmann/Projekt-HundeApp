using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Dogity.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddAdaptivePlanningModel : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "DayIndex",
                table: "training_plan_items",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<string>(
                name: "Difficulty",
                table: "training_plan_items",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Reason",
                table: "training_plan_items",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Source",
                table: "training_plan_items",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "Auto");

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "LastPlanGeneratedAt",
                table: "goals",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "TrainingDaysPerWeek",
                table: "goals",
                type: "integer",
                nullable: false,
                defaultValue: 2);

            migrationBuilder.AddColumn<int>(
                name: "WeeklyExerciseCount",
                table: "goals",
                type: "integer",
                nullable: false,
                defaultValue: 5);

            migrationBuilder.CreateTable(
                name: "exercise_masteries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    DogId = table.Column<Guid>(type: "uuid", nullable: false),
                    ExerciseId = table.Column<Guid>(type: "uuid", nullable: false),
                    Box = table.Column<int>(type: "integer", nullable: false, defaultValue: 1),
                    LastTrainedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    DueAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    RecentAvgRating = table.Column<double>(type: "double precision", nullable: false),
                    SessionCount = table.Column<int>(type: "integer", nullable: false),
                    ManualPriority = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    DeletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_exercise_masteries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_exercise_masteries_dogs_DogId",
                        column: x => x.DogId,
                        principalTable: "dogs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_exercise_masteries_exercises_ExerciseId",
                        column: x => x.ExerciseId,
                        principalTable: "exercises",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_exercise_masteries_DogId_ExerciseId",
                table: "exercise_masteries",
                columns: new[] { "DogId", "ExerciseId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_exercise_masteries_ExerciseId",
                table: "exercise_masteries",
                column: "ExerciseId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "exercise_masteries");

            migrationBuilder.DropColumn(
                name: "DayIndex",
                table: "training_plan_items");

            migrationBuilder.DropColumn(
                name: "Difficulty",
                table: "training_plan_items");

            migrationBuilder.DropColumn(
                name: "Reason",
                table: "training_plan_items");

            migrationBuilder.DropColumn(
                name: "Source",
                table: "training_plan_items");

            migrationBuilder.DropColumn(
                name: "LastPlanGeneratedAt",
                table: "goals");

            migrationBuilder.DropColumn(
                name: "TrainingDaysPerWeek",
                table: "goals");

            migrationBuilder.DropColumn(
                name: "WeeklyExerciseCount",
                table: "goals");
        }
    }
}
