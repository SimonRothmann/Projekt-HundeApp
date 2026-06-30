using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Dogity.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddRegulationToGoal : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "RegulationId",
                table: "goals",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_goals_RegulationId",
                table: "goals",
                column: "RegulationId");

            migrationBuilder.AddForeignKey(
                name: "FK_goals_regulations_RegulationId",
                table: "goals",
                column: "RegulationId",
                principalTable: "regulations",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_goals_regulations_RegulationId",
                table: "goals");

            migrationBuilder.DropIndex(
                name: "IX_goals_RegulationId",
                table: "goals");

            migrationBuilder.DropColumn(
                name: "RegulationId",
                table: "goals");
        }
    }
}
