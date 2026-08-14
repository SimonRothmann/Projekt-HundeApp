using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Dogity.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddWeatherAndSessionContext : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<double>(
                name: "Latitude",
                table: "training_sessions",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LocationName",
                table: "training_sessions",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "Longitude",
                table: "training_sessions",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "RelativeHumidity",
                table: "training_sessions",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<TimeOnly>(
                name: "StartTime",
                table: "training_sessions",
                type: "time without time zone",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "TemperatureC",
                table: "training_sessions",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "WeatherCode",
                table: "training_sessions",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "WeatherFetchedAt",
                table: "training_sessions",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "WindSpeedKmh",
                table: "training_sessions",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "LaidRelativeHumidity",
                table: "gps_tracks",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "LaidTemperatureC",
                table: "gps_tracks",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "LaidWeatherCode",
                table: "gps_tracks",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "LaidWindSpeedKmh",
                table: "gps_tracks",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "SearchRelativeHumidity",
                table: "gps_tracks",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "SearchTemperatureC",
                table: "gps_tracks",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "SearchWeatherCode",
                table: "gps_tracks",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "SearchWindSpeedKmh",
                table: "gps_tracks",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "WeatherFetchedAt",
                table: "gps_tracks",
                type: "timestamp with time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Latitude",
                table: "training_sessions");

            migrationBuilder.DropColumn(
                name: "LocationName",
                table: "training_sessions");

            migrationBuilder.DropColumn(
                name: "Longitude",
                table: "training_sessions");

            migrationBuilder.DropColumn(
                name: "RelativeHumidity",
                table: "training_sessions");

            migrationBuilder.DropColumn(
                name: "StartTime",
                table: "training_sessions");

            migrationBuilder.DropColumn(
                name: "TemperatureC",
                table: "training_sessions");

            migrationBuilder.DropColumn(
                name: "WeatherCode",
                table: "training_sessions");

            migrationBuilder.DropColumn(
                name: "WeatherFetchedAt",
                table: "training_sessions");

            migrationBuilder.DropColumn(
                name: "WindSpeedKmh",
                table: "training_sessions");

            migrationBuilder.DropColumn(
                name: "LaidRelativeHumidity",
                table: "gps_tracks");

            migrationBuilder.DropColumn(
                name: "LaidTemperatureC",
                table: "gps_tracks");

            migrationBuilder.DropColumn(
                name: "LaidWeatherCode",
                table: "gps_tracks");

            migrationBuilder.DropColumn(
                name: "LaidWindSpeedKmh",
                table: "gps_tracks");

            migrationBuilder.DropColumn(
                name: "SearchRelativeHumidity",
                table: "gps_tracks");

            migrationBuilder.DropColumn(
                name: "SearchTemperatureC",
                table: "gps_tracks");

            migrationBuilder.DropColumn(
                name: "SearchWeatherCode",
                table: "gps_tracks");

            migrationBuilder.DropColumn(
                name: "SearchWindSpeedKmh",
                table: "gps_tracks");

            migrationBuilder.DropColumn(
                name: "WeatherFetchedAt",
                table: "gps_tracks");
        }
    }
}
