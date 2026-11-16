using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Dogity.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddTrainingAnfragekennungUndTagesIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "ClientRequestId",
                table: "training_exercises",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_training_sessions_DogId_Date",
                table: "training_sessions",
                columns: new[] { "DogId", "Date" });

            migrationBuilder.CreateIndex(
                name: "IX_training_exercises_ClientRequestId",
                table: "training_exercises",
                column: "ClientRequestId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_training_sessions_DogId_Date",
                table: "training_sessions");

            migrationBuilder.DropIndex(
                name: "IX_training_exercises_ClientRequestId",
                table: "training_exercises");

            migrationBuilder.DropColumn(
                name: "ClientRequestId",
                table: "training_exercises");
        }
    }
}
