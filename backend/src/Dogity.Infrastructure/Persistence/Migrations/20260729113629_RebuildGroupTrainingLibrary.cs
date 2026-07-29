using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Dogity.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RebuildGroupTrainingLibrary : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Alt-Daten (nur wegwerfbare Seed-/Testinhalte des ersetzten
            // Gruppentraining-Modells) vor dem Umbau entfernen - die neuen
            // NOT-NULL-Spalten (ClubId, GroupTrainingExerciseId) und die
            // zugehörigen Fremdschlüssel würden sonst auf bestehenden Zeilen
            // mit Null-GUID scheitern. Auf einer frischen DB No-ops.
            migrationBuilder.Sql("DELETE FROM group_training_unit_items;");
            migrationBuilder.Sql("DELETE FROM group_training_units;");

            migrationBuilder.DropForeignKey(
                name: "FK_group_training_units_groups_GroupId",
                table: "group_training_units");

            migrationBuilder.DropIndex(
                name: "IX_group_training_units_Category_CreatedByUserId",
                table: "group_training_units");

            migrationBuilder.DropIndex(
                name: "IX_group_training_units_GroupId",
                table: "group_training_units");

            migrationBuilder.DropColumn(
                name: "GroupId",
                table: "group_training_units");

            migrationBuilder.DropColumn(
                name: "SortOrder",
                table: "group_training_units");

            migrationBuilder.DropColumn(
                name: "Description",
                table: "group_training_unit_items");

            migrationBuilder.DropColumn(
                name: "DurationMinutes",
                table: "group_training_unit_items");

            migrationBuilder.DropColumn(
                name: "Focus",
                table: "group_training_unit_items");

            migrationBuilder.DropColumn(
                name: "Title",
                table: "group_training_unit_items");

            migrationBuilder.AddColumn<Guid>(
                name: "ClubId",
                table: "group_training_units",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "GroupTrainingExerciseId",
                table: "group_training_unit_items",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.CreateTable(
                name: "group_training_exercises",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ClubId = table.Column<Guid>(type: "uuid", nullable: false),
                    Category = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Focus = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    DurationMinutes = table.Column<int>(type: "integer", nullable: true),
                    Description = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    ExamTargets = table.Column<int>(type: "integer", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    DeletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_group_training_exercises", x => x.Id);
                    table.ForeignKey(
                        name: "FK_group_training_exercises_clubs_ClubId",
                        column: x => x.ClubId,
                        principalTable: "clubs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_group_training_units_ClubId_Category",
                table: "group_training_units",
                columns: new[] { "ClubId", "Category" });

            migrationBuilder.CreateIndex(
                name: "IX_group_training_unit_items_GroupTrainingExerciseId",
                table: "group_training_unit_items",
                column: "GroupTrainingExerciseId");

            migrationBuilder.CreateIndex(
                name: "IX_group_training_exercises_ClubId_Category",
                table: "group_training_exercises",
                columns: new[] { "ClubId", "Category" });

            migrationBuilder.AddForeignKey(
                name: "FK_group_training_unit_items_group_training_exercises_GroupTra~",
                table: "group_training_unit_items",
                column: "GroupTrainingExerciseId",
                principalTable: "group_training_exercises",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_group_training_units_clubs_ClubId",
                table: "group_training_units",
                column: "ClubId",
                principalTable: "clubs",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_group_training_unit_items_group_training_exercises_GroupTra~",
                table: "group_training_unit_items");

            migrationBuilder.DropForeignKey(
                name: "FK_group_training_units_clubs_ClubId",
                table: "group_training_units");

            migrationBuilder.DropTable(
                name: "group_training_exercises");

            migrationBuilder.DropIndex(
                name: "IX_group_training_units_ClubId_Category",
                table: "group_training_units");

            migrationBuilder.DropIndex(
                name: "IX_group_training_unit_items_GroupTrainingExerciseId",
                table: "group_training_unit_items");

            migrationBuilder.DropColumn(
                name: "ClubId",
                table: "group_training_units");

            migrationBuilder.DropColumn(
                name: "GroupTrainingExerciseId",
                table: "group_training_unit_items");

            migrationBuilder.AddColumn<Guid>(
                name: "GroupId",
                table: "group_training_units",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "SortOrder",
                table: "group_training_units",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "Description",
                table: "group_training_unit_items",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "DurationMinutes",
                table: "group_training_unit_items",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Focus",
                table: "group_training_unit_items",
                type: "character varying(80)",
                maxLength: 80,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Title",
                table: "group_training_unit_items",
                type: "character varying(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateIndex(
                name: "IX_group_training_units_Category_CreatedByUserId",
                table: "group_training_units",
                columns: new[] { "Category", "CreatedByUserId" });

            migrationBuilder.CreateIndex(
                name: "IX_group_training_units_GroupId",
                table: "group_training_units",
                column: "GroupId");

            migrationBuilder.AddForeignKey(
                name: "FK_group_training_units_groups_GroupId",
                table: "group_training_units",
                column: "GroupId",
                principalTable: "groups",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
