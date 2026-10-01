using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Dogity.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddGroupRegistrations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "RegistrationCode",
                table: "groups",
                type: "character varying(32)",
                maxLength: 32,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "group_registrations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    GroupId = table.Column<Guid>(type: "uuid", nullable: false),
                    FirstName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    LastName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    DogName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    DogBreed = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    DogBirthDate = table.Column<DateOnly>(type: "date", nullable: false),
                    Phone = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    Source = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    RegisteredAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    PaidAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    PaidByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    Notes = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    DeletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_group_registrations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_group_registrations_groups_GroupId",
                        column: x => x.GroupId,
                        principalTable: "groups",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "group_registration_attendances",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    RegistrationId = table.Column<Guid>(type: "uuid", nullable: false),
                    Date = table.Column<DateOnly>(type: "date", nullable: false),
                    GroupTrainingSessionId = table.Column<Guid>(type: "uuid", nullable: true),
                    MarkedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    MarkedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    DeletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_group_registration_attendances", x => x.Id);
                    table.ForeignKey(
                        name: "FK_group_registration_attendances_group_registrations_Registra~",
                        column: x => x.RegistrationId,
                        principalTable: "group_registrations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_groups_RegistrationCode",
                table: "groups",
                column: "RegistrationCode",
                unique: true,
                filter: "\"RegistrationCode\" IS NOT NULL AND \"RegistrationCode\" <> ''");

            migrationBuilder.CreateIndex(
                name: "IX_group_registration_attendances_RegistrationId_Date",
                table: "group_registration_attendances",
                columns: new[] { "RegistrationId", "Date" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_group_registrations_GroupId",
                table: "group_registrations",
                column: "GroupId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "group_registration_attendances");

            migrationBuilder.DropTable(
                name: "group_registrations");

            migrationBuilder.DropIndex(
                name: "IX_groups_RegistrationCode",
                table: "groups");

            migrationBuilder.DropColumn(
                name: "RegistrationCode",
                table: "groups");
        }
    }
}
