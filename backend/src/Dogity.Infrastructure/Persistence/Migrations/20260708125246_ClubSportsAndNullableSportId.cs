using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Dogity.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ClubSportsAndNullableSportId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_exercises_sports_SportId",
                table: "exercises");

            migrationBuilder.DropIndex(
                name: "IX_sports_Code",
                table: "sports");

            migrationBuilder.AddColumn<Guid>(
                name: "ClubId",
                table: "sports",
                type: "uuid",
                nullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "SportId",
                table: "exercises",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.CreateIndex(
                name: "IX_sports_ClubId",
                table: "sports",
                column: "ClubId");

            migrationBuilder.CreateIndex(
                name: "IX_sports_Code_ClubId",
                table: "sports",
                columns: new[] { "Code", "ClubId" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_exercises_sports_SportId",
                table: "exercises",
                column: "SportId",
                principalTable: "sports",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_exercises_sports_SportId",
                table: "exercises");

            migrationBuilder.DropIndex(
                name: "IX_sports_ClubId",
                table: "sports");

            migrationBuilder.DropIndex(
                name: "IX_sports_Code_ClubId",
                table: "sports");

            migrationBuilder.DropColumn(
                name: "ClubId",
                table: "sports");

            migrationBuilder.AlterColumn<Guid>(
                name: "SportId",
                table: "exercises",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_sports_Code",
                table: "sports",
                column: "Code",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_exercises_sports_SportId",
                table: "exercises",
                column: "SportId",
                principalTable: "sports",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
