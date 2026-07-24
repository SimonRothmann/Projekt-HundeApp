using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Dogity.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddDogArchivedAt : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "ArchivedAt",
                table: "dogs",
                type: "timestamp with time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ArchivedAt",
                table: "dogs");
        }
    }
}
