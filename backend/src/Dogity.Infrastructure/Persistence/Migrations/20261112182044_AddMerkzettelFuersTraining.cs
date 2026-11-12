using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Dogity.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddMerkzettelFuersTraining : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "ReminderTrainingNotes",
                table: "user_preferences",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.CreateTable(
                name: "dog_training_rhythms",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    DogId = table.Column<Guid>(type: "uuid", nullable: false),
                    Days = table.Column<int>(type: "integer", nullable: false),
                    Hour = table.Column<int>(type: "integer", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    DeletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_dog_training_rhythms", x => x.Id);
                    table.ForeignKey(
                        name: "FK_dog_training_rhythms_dogs_DogId",
                        column: x => x.DogId,
                        principalTable: "dogs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "training_notes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    DogId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: true),
                    Text = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    ExerciseId = table.Column<Guid>(type: "uuid", nullable: true),
                    Goal = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    TargetDate = table.Column<DateOnly>(type: "date", nullable: true),
                    DoneAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    DoneInTrainingId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    DeletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_training_notes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_training_notes_dogs_DogId",
                        column: x => x.DogId,
                        principalTable: "dogs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_training_notes_exercises_ExerciseId",
                        column: x => x.ExerciseId,
                        principalTable: "exercises",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_dog_training_rhythms_DogId",
                table: "dog_training_rhythms",
                column: "DogId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_training_notes_DogId_DoneAt",
                table: "training_notes",
                columns: new[] { "DogId", "DoneAt" });

            migrationBuilder.CreateIndex(
                name: "IX_training_notes_ExerciseId",
                table: "training_notes",
                column: "ExerciseId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "dog_training_rhythms");

            migrationBuilder.DropTable(
                name: "training_notes");

            migrationBuilder.DropColumn(
                name: "ReminderTrainingNotes",
                table: "user_preferences");
        }
    }
}
