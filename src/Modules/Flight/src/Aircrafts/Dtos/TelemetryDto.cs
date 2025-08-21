namespace Flight.Aircrafts.Dtos;

// DTO for telemetry serialization - avoids value object serialization issues
public record TelemetryDto
{
    public double Latitude { get; init; }
    public double Longitude { get; init; }
    public double Altitude { get; init; }
    
    public double Roll { get; init; }
    public double Pitch { get; init; }
    public double Yaw { get; init; }
    
    public double Speed { get; init; }
    public double Heading { get; init; }
    public double FuelLevel { get; init; }
    public string FlightPhase { get; init; } = string.Empty;
    
    public DateTime Timestamp { get; init; }
    
    // Default constructor for JSON serialization
    public TelemetryDto() { }
    
    // Constructor from value objects
    public TelemetryDto(
        ValueObjects.Position position,
        ValueObjects.Attitude attitude,
        ValueObjects.TelemetryData telemetry,
        DateTime timestamp)
    {
        Latitude = position.Latitude;
        Longitude = position.Longitude;
        Altitude = position.Altitude;
        
        Roll = attitude.Roll;
        Pitch = attitude.Pitch;
        Yaw = attitude.Yaw;
        
        Speed = telemetry.Speed;
        Heading = telemetry.Heading;
        FuelLevel = telemetry.FuelLevel;
        FlightPhase = telemetry.FlightPhase;
        
        Timestamp = timestamp;
    }
}
