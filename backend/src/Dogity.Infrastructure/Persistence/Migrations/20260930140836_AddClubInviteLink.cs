using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Dogity.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddClubInviteLink : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "InviteCode",
                table: "clubs",
                type: "character varying(32)",
                maxLength: 32,
                nullable: true);

            // "Directory": Bestehende Anfragen und Mitgliedschaften kamen alle
            // über die Liste. Ein leerer Wert ließe sich nicht als Aufzählung
            // lesen und ließe jede Mitgliederabfrage scheitern.
            migrationBuilder.AddColumn<string>(
                name: "Source",
                table: "club_memberships",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "Directory");

            migrationBuilder.CreateIndex(
                name: "IX_clubs_InviteCode",
                table: "clubs",
                column: "InviteCode",
                unique: true,
                filter: "\"InviteCode\" IS NOT NULL AND \"InviteCode\" <> ''");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_clubs_InviteCode",
                table: "clubs");

            migrationBuilder.DropColumn(
                name: "InviteCode",
                table: "clubs");

            migrationBuilder.DropColumn(
                name: "Source",
                table: "club_memberships");
        }
    }
}
