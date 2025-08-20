namespace Flight.Aircrafts.Models;

using System;

public class AircraftReadModel
{
    // Aircraft static properties
    public required Guid Id { get; init; }
    public required Guid AircraftId { get; init; }
    public required string Name { get; init; }
    public required string Model { get; init; }
    public required int ManufacturingYear { get; init; }
    public required bool IsDeleted { get; init; }

    // Position telemetry
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
    public double? Altitude { get; set; }

    // Attitude telemetry
    public double? Roll { get; set; }
    public double? Pitch { get; set; }
    public double? Yaw { get; set; }

    // Flight telemetry
    public double? Speed { get; set; }
    public double? Heading { get; set; }
    public double? FuelLevel { get; set; }
    public string? FlightPhase { get; set; }

    // Metadata
    public DateTime? LastTelemetryUpdate { get; set; }
}