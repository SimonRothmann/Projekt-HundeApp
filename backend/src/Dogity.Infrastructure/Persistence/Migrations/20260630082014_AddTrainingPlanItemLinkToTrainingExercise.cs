using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Dogity.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddTrainingPlanItemLinkToTrainingExercise : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "TrainingPlanItemId",
                table: "training_exercises",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_training_exercises_TrainingPlanItemId",
                table: "training_exercises",
                column: "TrainingPlanItemId");

            migrationBuilder.AddForeignKey(
                name: "FK_training_exercises_training_plan_items_TrainingPlanItemId",
                table: "training_exercises",
                column: "TrainingPlanItemId",
                principalTable: "training_plan_items",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_training_exercises_training_plan_items_TrainingPlanItemId",
                table: "training_exercises");

            migrationBuilder.DropIndex(
                name: "IX_training_exercises_TrainingPlanItemId",
                table: "training_exercises");

            migrationBuilder.DropColumn(
                name: "TrainingPlanItemId",
                table: "training_exercises");
        }
    }
}
