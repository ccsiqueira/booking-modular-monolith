using BuildingBlocks.EventStoreDB.Repository;
using BuildingBlocks.RosConnector;
using Flight.Aircrafts.Models;
using Flight.Aircrafts.ValueObjects;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Text.Json;

namespace Flight.Aircrafts.Services;

public class AircraftTelemetryCollectorService : BackgroundService
{
    private readonly IRosConnectorService _rosConnector;
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<AircraftTelemetryCollectorService> _logger;
    private readonly AircraftTelemetryCollectorOptions _options;

    public AircraftTelemetryCollectorService(
        IRosConnectorService rosConnector,
        IServiceProvider serviceProvider,
        ILogger<AircraftTelemetryCollectorService> logger,
        IOptions<AircraftTelemetryCollectorOptions> options)
    {
        _rosConnector = rosConnector;
        _serviceProvider = serviceProvider;
        _logger = logger;
        _options = options.Value;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Aircraft Telemetry Collector Service started - Always ON mode");

        // Connect to ROS2
        var connected = await _rosConnector.ConnectAsync(_options.RosUri);
        if (!connected)
        {
            _logger.LogError("Failed to connect to ROS2 at {Uri}. Service will retry connection.", _options.RosUri);
        }

        // Always running - no start/stop
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                // Ensure we're still connected
                if (!await _rosConnector.IsConnectedAsync())
                {
                    _logger.LogWarning("Lost connection to ROS2. Attempting to reconnect...");
                    await _rosConnector.ConnectAsync(_options.RosUri);
                }

                await CollectAndUpdateTelemetryAsync(stoppingToken);
                
                // Wait for next collection cycle (1Hz frequency)
                await Task.Delay(TimeSpan.FromSeconds(_options.CollectionIntervalSeconds), stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in telemetry collection cycle");
                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken); // Error delay
            }
        }

        _logger.LogInformation("Aircraft Telemetry Collector Service stopped");
    }

    private async Task CollectAndUpdateTelemetryAsync(CancellationToken cancellationToken)
    {
        using var scope = _serviceProvider.CreateScope();
        var aircraftRepository = scope.ServiceProvider.GetRequiredService<IEventStoreDBRepository<Aircraft>>();

        try
        {
            // Collect telemetry data from ROS2 topics
            var telemetryData = await CollectTelemetryFromRosAsync();
            
            if (telemetryData == null)
            {
                _logger.LogDebug("No telemetry data received from ROS2");
                return;
            }

            // Find or create aircraft (AIRCRAFT_001 is our simulated aircraft)
            var aircraftId = AircraftId.Of(_options.AircraftId);
            var aircraft = await aircraftRepository.FindAsync(aircraftId, cancellationToken);

            if (aircraft == null)
            {
                _logger.LogWarning("Aircraft {AircraftId} not found in event store. Creating aircraft first.", _options.AircraftId);
                // In a real scenario, you might want to create the aircraft here or skip telemetry update
                return;
            }

            // Update telemetry (creates and applies event)
            aircraft.UpdateTelemetry(
                telemetryData.Position,
                telemetryData.Attitude,
                telemetryData.TelemetryData);

            // Save to EventStoreDB
            await aircraftRepository.SaveAsync(aircraft, cancellationToken);

            _logger.LogDebug("Updated telemetry for aircraft {AircraftId}: {Position}, {Attitude}, {TelemetryData}", 
                aircraftId.Value, telemetryData.Position, telemetryData.Attitude, telemetryData.TelemetryData);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating aircraft telemetry");
        }
    }

    private async Task<CollectedTelemetryData?> CollectTelemetryFromRosAsync()
    {
        try
        {
            // Collect data from all ROS2 topics
            var gpsTask = _rosConnector.GetLatestAsync<dynamic>("/aircraft/AIRCRAFT_001/gps");
            var attitudeTask = _rosConnector.GetLatestAsync<dynamic>("/aircraft/AIRCRAFT_001/attitude");
            var velocityTask = _rosConnector.GetLatestAsync<dynamic>("/aircraft/AIRCRAFT_001/velocity");
            var batteryTask = _rosConnector.GetLatestAsync<dynamic>("/aircraft/AIRCRAFT_001/battery");
            var altitudeTask = _rosConnector.GetLatestAsync<dynamic>("/aircraft/AIRCRAFT_001/altitude");
            var airspeedTask = _rosConnector.GetLatestAsync<dynamic>("/aircraft/AIRCRAFT_001/airspeed");
            var headingTask = _rosConnector.GetLatestAsync<dynamic>("/aircraft/AIRCRAFT_001/heading");
            var phaseTask = _rosConnector.GetLatestAsync<dynamic>("/aircraft/AIRCRAFT_001/flight_phase");

            // Wait for all data collection tasks
            await Task.WhenAll(gpsTask, attitudeTask, velocityTask, batteryTask, altitudeTask, airspeedTask, headingTask, phaseTask);

            var gpsData = await gpsTask;
            var attitudeData = await attitudeTask;
            var velocityData = await velocityTask;
            var batteryData = await batteryTask;
            var altitudeData = await altitudeTask;
            var airspeedData = await airspeedTask;
            var headingData = await headingTask;
            var phaseData = await phaseTask;

            // Parse and validate the collected data
            if (gpsData == null || attitudeData == null)
            {
                _logger.LogDebug("Missing essential telemetry data (GPS or attitude)");
                return null;
            }

            // Convert dynamic objects to our value objects
            var position = ParsePosition(gpsData, altitudeData);
            var attitude = ParseAttitude(attitudeData);
            var telemetry = ParseTelemetryData(airspeedData, headingData, batteryData, phaseData);

            return new CollectedTelemetryData(position, attitude, telemetry);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error collecting telemetry from ROS2");
            return null;
        }
    }

    private Position ParsePosition(dynamic gpsData, dynamic? altitudeData)
    {
        try
        {
            // Parse GPS data
            var latitude = GetPropertyValue<double>(gpsData, "latitude") ?? 0.0;
            var longitude = GetPropertyValue<double>(gpsData, "longitude") ?? 0.0;
            var altitude = GetPropertyValue<double>(gpsData, "altitude") ?? 0.0;

            // Use separate altitude if available
            if (altitudeData != null)
            {
                var separateAltitude = GetPropertyValue<double>(altitudeData, "data");
                if (separateAltitude.HasValue)
                {
                    altitude = separateAltitude.Value * 0.3048; // Convert feet to meters if needed
                }
            }

            return Position.Of(latitude, longitude, altitude);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error parsing position data");
            return Position.Empty;
        }
    }

    private Attitude ParseAttitude(dynamic attitudeData)
    {
        try
        {
            var vector = GetPropertyValue<dynamic>(attitudeData, "vector");
            if (vector == null) return Attitude.Empty;

            var roll = GetPropertyValue<double>(vector, "x") ?? 0.0;
            var pitch = GetPropertyValue<double>(vector, "y") ?? 0.0;
            var yaw = GetPropertyValue<double>(vector, "z") ?? 0.0;

            return Attitude.Of(roll, pitch, yaw);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error parsing attitude data");
            return Attitude.Empty;
        }
    }

    private TelemetryData ParseTelemetryData(dynamic? airspeedData, dynamic? headingData, dynamic? batteryData, dynamic? phaseData)
    {
        try
        {
            var speed = GetPropertyValue<double>(airspeedData, "data") ?? 0.0;
            var heading = GetPropertyValue<double>(headingData, "data") ?? 0.0;
            var fuelLevel = GetPropertyValue<double>(batteryData, "percentage") ?? 0.0;
            var flightPhase = GetPropertyValue<string>(phaseData, "data") ?? "UNKNOWN";

            // Convert fuel percentage to 0-100 range if needed
            if (fuelLevel <= 1.0) fuelLevel *= 100.0;

            return TelemetryData.Of(speed, heading, fuelLevel, flightPhase);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error parsing telemetry data");
            return TelemetryData.Empty;
        }
    }

    private T? GetPropertyValue<T>(dynamic? obj, string propertyName)
    {
        if (obj == null) return default;

        try
        {
            // Handle JsonElement
            if (obj is JsonElement element)
            {
                if (element.TryGetProperty(propertyName, out var property))
                {
                    if (typeof(T) == typeof(double) || typeof(T) == typeof(double?))
                    {
                        return (T)(object)property.GetDouble();
                    }
                    if (typeof(T) == typeof(string))
                    {
                        return (T)(object)property.GetString()!;
                    }
                    if (typeof(T) == typeof(dynamic))
                    {
                        return (T)(object)property;
                    }
                }
                return default;
            }

            // Handle dynamic object properties
            var type = obj.GetType();
            var propertyInfo = type.GetProperty(propertyName);
            if (propertyInfo != null)
            {
                var value = propertyInfo.GetValue(obj);
                if (value is T typedValue)
                {
                    return typedValue;
                }
            }

            return default;
        }
        catch
        {
            return default;
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Aircraft Telemetry Collector Service stopping...");
        await _rosConnector.DisconnectAsync();
        await base.StopAsync(cancellationToken);
    }
}

// Helper classes
public record CollectedTelemetryData(Position Position, Attitude Attitude, TelemetryData TelemetryData);

public class AircraftTelemetryCollectorOptions
{
    public const string SectionName = "AircraftTelemetryCollector";
    
    public string RosUri { get; set; } = "ws://localhost:9090";
    public string AircraftId { get; set; } = "AIRCRAFT_001"; // Default to our simulated aircraft
    public int CollectionIntervalSeconds { get; set; } = 1; // 1Hz frequency
    public int MaxRetries { get; set; } = 3;
    public bool EnableLiveData { get; set; } = true;
}
