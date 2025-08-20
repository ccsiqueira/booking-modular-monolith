namespace Flight.Aircrafts.ValueObjects;

using System;
using Flight.Aircrafts.Exceptions;

public record Position
{
    public double Latitude { get; }
    public double Longitude { get; }
    public double Altitude { get; }

    private Position(double latitude, double longitude, double altitude)
    {
        Latitude = latitude;
        Longitude = longitude;
        Altitude = altitude;
    }

    public static Position Of(double latitude, double longitude, double altitude)
    {
        if (latitude < -90 || latitude > 90)
        {
            throw new InvalidPositionException($"Latitude must be between -90 and 90 degrees. Got: {latitude}");
        }

        if (longitude < -180 || longitude > 180)
        {
            throw new InvalidPositionException($"Longitude must be between -180 and 180 degrees. Got: {longitude}");
        }

        if (altitude < -1000 || altitude > 100000)
        {
            throw new InvalidPositionException($"Altitude must be between -1000 and 100000 meters. Got: {altitude}");
        }

        return new Position(latitude, longitude, altitude);
    }

    public static Position Empty => new Position(0, 0, 0);

    public override string ToString()
    {
        return $"Position(Lat: {Latitude:F6}°, Lon: {Longitude:F6}°, Alt: {Altitude:F0}m)";
    }
}
