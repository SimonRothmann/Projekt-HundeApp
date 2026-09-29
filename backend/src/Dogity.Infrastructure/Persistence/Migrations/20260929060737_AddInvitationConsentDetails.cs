using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Dogity.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddInvitationConsentDetails : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "InvitedEmail",
                table: "group_trainers",
                type: "character varying(256)",
                maxLength: 256,
                nullable: true);

            // 0 = Active: Bestehende Co-Trainer:innen bleiben, was sie sind.
            // Erst neue Einladungen brauchen eine Zusage.
            migrationBuilder.AddColumn<int>(
                name: "Status",
                table: "group_trainers",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "InvitedEmail",
                table: "group_members",
                type: "character varying(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "InvitedEmail",
                table: "dog_owners",
                type: "character varying(256)",
                maxLength: 256,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "InvitedEmail",
                table: "group_trainers");

            migrationBuilder.DropColumn(
                name: "Status",
                table: "group_trainers");

            migrationBuilder.DropColumn(
                name: "InvitedEmail",
                table: "group_members");

            migrationBuilder.DropColumn(
                name: "InvitedEmail",
                table: "dog_owners");
        }
    }
}
