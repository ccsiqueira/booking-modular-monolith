using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Flight.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddAircraftTelemetryFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_flight_aircraft_aircraft_id",
                table: "flight");

            migrationBuilder.DropIndex(
                name: "ix_flight_aircraft_id",
                table: "flight");

            migrationBuilder.AddColumn<double>(
                name: "altitude",
                table: "aircraft",
                type: "double precision",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "flight_phase",
                table: "aircraft",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "fuel_level",
                table: "aircraft",
                type: "double precision",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "last_telemetry_update",
                table: "aircraft",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "latitude",
                table: "aircraft",
                type: "double precision",
                precision: 18,
                scale: 6,
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "longitude",
                table: "aircraft",
                type: "double precision",
                precision: 18,
                scale: 6,
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "pitch",
                table: "aircraft",
                type: "double precision",
                precision: 18,
                scale: 6,
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "roll",
                table: "aircraft",
                type: "double precision",
                precision: 18,
                scale: 6,
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "speed",
                table: "aircraft",
                type: "double precision",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "telemetry_heading",
                table: "aircraft",
                type: "double precision",
                precision: 18,
                scale: 6,
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "yaw",
                table: "aircraft",
                type: "double precision",
                precision: 18,
                scale: 6,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "altitude",
                table: "aircraft");

            migrationBuilder.DropColumn(
                name: "flight_phase",
                table: "aircraft");

            migrationBuilder.DropColumn(
                name: "fuel_level",
                table: "aircraft");

            migrationBuilder.DropColumn(
                name: "last_telemetry_update",
                table: "aircraft");

            migrationBuilder.DropColumn(
                name: "latitude",
                table: "aircraft");

            migrationBuilder.DropColumn(
                name: "longitude",
                table: "aircraft");

            migrationBuilder.DropColumn(
                name: "pitch",
                table: "aircraft");

            migrationBuilder.DropColumn(
                name: "roll",
                table: "aircraft");

            migrationBuilder.DropColumn(
                name: "speed",
                table: "aircraft");

            migrationBuilder.DropColumn(
                name: "telemetry_heading",
                table: "aircraft");

            migrationBuilder.DropColumn(
                name: "yaw",
                table: "aircraft");

            migrationBuilder.CreateIndex(
                name: "ix_flight_aircraft_id",
                table: "flight",
                column: "aircraft_id");

            migrationBuilder.AddForeignKey(
                name: "fk_flight_aircraft_aircraft_id",
                table: "flight",
                column: "aircraft_id",
                principalTable: "aircraft",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
