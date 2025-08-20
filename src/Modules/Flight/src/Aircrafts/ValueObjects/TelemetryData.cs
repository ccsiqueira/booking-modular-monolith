namespace Flight.Aircrafts.ValueObjects;

using System;
using Flight.Aircrafts.Exceptions;

public record TelemetryData
{
    public double Speed { get; }        // Ground speed in knots
    public double Heading { get; }      // Heading in degrees (0-360)
    public double FuelLevel { get; }    // Fuel level percentage (0-100)
    public string FlightPhase { get; }  // Current flight phase

    private TelemetryData(double speed, double heading, double fuelLevel, string flightPhase)
    {
        Speed = speed;
        Heading = heading;
        FuelLevel = fuelLevel;
        FlightPhase = flightPhase;
    }

    public static TelemetryData Of(double speed, double heading, double fuelLevel, string flightPhase)
    {
        if (speed < 0 || speed > 1000)
        {
            throw new InvalidTelemetryDataException($"Speed must be between 0 and 1000 knots. Got: {speed}");
        }

        var normalizedHeading = NormalizeHeading(heading);

        if (fuelLevel < 0 || fuelLevel > 100)
        {
            throw new InvalidTelemetryDataException($"Fuel level must be between 0 and 100 percent. Got: {fuelLevel}");
        }

        if (string.IsNullOrWhiteSpace(flightPhase))
        {
            throw new InvalidTelemetryDataException("Flight phase cannot be null or empty");
        }

        return new TelemetryData(speed, normalizedHeading, fuelLevel, flightPhase.ToUpperInvariant());
    }

    public static TelemetryData Empty => new TelemetryData(0, 0, 0, "UNKNOWN");

    private static double NormalizeHeading(double heading)
    {
        while (heading < 0) heading += 360;
        while (heading >= 360) heading -= 360;
        return heading;
    }

    public override string ToString()
    {
        return $"Telemetry(Speed: {Speed:F0}kts, Heading: {Heading:F0}°, Fuel: {FuelLevel:F1}%, Phase: {FlightPhase})";
    }
}