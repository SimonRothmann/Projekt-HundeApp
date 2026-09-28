using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Dogity.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddDogOwnerInvitation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "InvitedByUserId",
                table: "dog_owners",
                type: "uuid",
                nullable: true);

            // Bestehende Mitbesitzer:innen bleiben ohne Rückfrage aktiv - es
            // sind gewachsene Beziehungen, und eine Nachbestätigung würde nur
            // verwirren. Erst neue Einladungen brauchen eine Zusage.
            migrationBuilder.AddColumn<string>(
                name: "Status",
                table: "dog_owners",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "Active");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "InvitedByUserId",
                table: "dog_owners");

            migrationBuilder.DropColumn(
                name: "Status",
                table: "dog_owners");
        }
    }
}
