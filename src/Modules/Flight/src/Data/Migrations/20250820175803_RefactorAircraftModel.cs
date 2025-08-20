using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Flight.Data.Migrations
{
    /// <inheritdoc />
    public partial class RefactorAircraftModel : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "current_attitude_pitch",
                table: "aircraft");

            migrationBuilder.DropColumn(
                name: "current_attitude_roll",
                table: "aircraft");

            migrationBuilder.DropColumn(
                name: "current_attitude_yaw",
                table: "aircraft");

            migrationBuilder.DropColumn(
                name: "current_position_altitude",
                table: "aircraft");

            migrationBuilder.DropColumn(
                name: "current_position_latitude",
                table: "aircraft");

            migrationBuilder.DropColumn(
                name: "current_position_longitude",
                table: "aircraft");

            migrationBuilder.DropColumn(
                name: "current_telemetry_flight_phase",
                table: "aircraft");

            migrationBuilder.DropColumn(
                name: "current_telemetry_fuel_level",
                table: "aircraft");

            migrationBuilder.DropColumn(
                name: "current_telemetry_heading",
                table: "aircraft");

            migrationBuilder.DropColumn(
                name: "current_telemetry_speed",
                table: "aircraft");

            migrationBuilder.DropColumn(
                name: "last_telemetry_update",
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

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_flight_aircraft_aircraft_id",
                table: "flight");

            migrationBuilder.DropIndex(
                name: "ix_flight_aircraft_id",
                table: "flight");

            migrationBuilder.AddColumn<double>(
                name: "current_attitude_pitch",
                table: "aircraft",
                type: "double precision",
                precision: 18,
                scale: 6,
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "current_attitude_roll",
                table: "aircraft",
                type: "double precision",
                precision: 18,
                scale: 6,
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "current_attitude_yaw",
                table: "aircraft",
                type: "double precision",
                precision: 18,
                scale: 6,
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "current_position_altitude",
                table: "aircraft",
                type: "double precision",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "current_position_latitude",
                table: "aircraft",
                type: "double precision",
                precision: 18,
                scale: 6,
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "current_position_longitude",
                table: "aircraft",
                type: "double precision",
                precision: 18,
                scale: 6,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "current_telemetry_flight_phase",
                table: "aircraft",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "current_telemetry_fuel_level",
                table: "aircraft",
                type: "double precision",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "current_telemetry_heading",
                table: "aircraft",
                type: "double precision",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "current_telemetry_speed",
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
        }
    }
}