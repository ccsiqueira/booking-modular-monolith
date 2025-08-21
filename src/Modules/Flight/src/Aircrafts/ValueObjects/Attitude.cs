namespace Flight.Aircrafts.ValueObjects;

using System;
using System.Text.Json.Serialization;
using Flight.Aircrafts.Exceptions;

public record Attitude
{
    public double Roll { get; init; }  // Rotation around X-axis (degrees)
    public double Pitch { get; init; } // Rotation around Y-axis (degrees)
    public double Yaw { get; init; }   // Rotation around Z-axis (degrees)

    // Default constructor for JSON serialization
    public Attitude() { }

    // Parameterized constructor for validation
    public Attitude(double roll, double pitch, double yaw)
    {
        Roll = roll;
        Pitch = pitch;
        Yaw = yaw;
    }

    public static Attitude Of(double roll, double pitch, double yaw)
    {
        // Normalize angles to valid ranges
        var normalizedRoll = NormalizeAngle(roll, -180, 180);
        var normalizedPitch = NormalizeAngle(pitch, -90, 90);
        var normalizedYaw = NormalizeAngle(yaw, 0, 360);

        return new Attitude(normalizedRoll, normalizedPitch, normalizedYaw);
    }

    public static Attitude Empty => new Attitude(0, 0, 0);

    private static double NormalizeAngle(double angle, double min, double max)
    {
        var range = max - min;
        while (angle < min) angle += range;
        while (angle > max) angle -= range;
        return angle;
    }

    public override string ToString()
    {
        return $"Attitude(Roll: {Roll:F1}°, Pitch: {Pitch:F1}°, Yaw: {Yaw:F1}°)";
    }
}