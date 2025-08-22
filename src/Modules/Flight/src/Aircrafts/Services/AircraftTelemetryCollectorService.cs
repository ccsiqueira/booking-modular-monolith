using BuildingBlocks.EventStoreDB.Repository;
using BuildingBlocks.RosConnector;
using Flight.Aircrafts.Models;
using Flight.Aircrafts.ValueObjects;
using Flight.Data;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MongoDB.Driver;
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
            catch (System.Exception ex)
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

            // Find aircraft by ROS identifier (AIRCRAFT_001) in read database first
            var readDbContext = scope.ServiceProvider.GetRequiredService<FlightReadDbContext>();
            var aircraftReadModel = await readDbContext.Aircraft
                .Find(a => a.Name == _options.RosAircraftId)
                .FirstOrDefaultAsync(cancellationToken);

            if (aircraftReadModel == null)
            {
                _logger.LogWarning("Aircraft with name '{RosAircraftId}' not found in read database. Aircraft must be created first.", _options.RosAircraftId);
                return;
            }

            // Now get the aircraft from EventStore using its GUID
            var aircraft = await aircraftRepository.Find(aircraftReadModel.AircraftId, cancellationToken);

            if (aircraft == null)
            {
                _logger.LogWarning("Aircraft with GUID {AircraftGuid} not found in event store. Inconsistent state detected.", aircraftReadModel.AircraftId);
                return;
            }

            // Update telemetry (creates and applies event)
            aircraft.UpdateTelemetry(
                telemetryData.Position,
                telemetryData.Attitude,
                telemetryData.TelemetryData);

            // Save to EventStoreDB
            await aircraftRepository.Add(aircraft, cancellationToken);

            _logger.LogDebug("Updated telemetry for aircraft {RosAircraftId} (GUID: {AircraftGuid}): {Position}, {Attitude}, {TelemetryData}", 
                _options.RosAircraftId, aircraftReadModel.AircraftId, telemetryData.Position, telemetryData.Attitude, telemetryData.TelemetryData);
        }
                    catch (System.Exception ex)
            {
                _logger.LogError(ex, "Error updating aircraft telemetry");
            }
    }

    private async Task<CollectedTelemetryData?> CollectTelemetryFromRosAsync()
    {
        try
        {
            // TODO: Remove this simulation when ROS2 is available
            if (!await _rosConnector.IsConnectedAsync())
            {
                _logger.LogWarning("ROS2 not connected, using simulated telemetry data");
                return CreateSimulatedTelemetryData();
            }

            // Collect data from all ROS2 topics
            var gpsTask = _rosConnector.GetLatestAsync<dynamic>($"/aircraft/{_options.RosAircraftId}/gps");
            var attitudeTask = _rosConnector.GetLatestAsync<dynamic>($"/aircraft/{_options.RosAircraftId}/attitude");
            var velocityTask = _rosConnector.GetLatestAsync<dynamic>($"/aircraft/{_options.RosAircraftId}/velocity");
            var batteryTask = _rosConnector.GetLatestAsync<dynamic>($"/aircraft/{_options.RosAircraftId}/battery");
            var altitudeTask = _rosConnector.GetLatestAsync<dynamic>($"/aircraft/{_options.RosAircraftId}/altitude");
            var airspeedTask = _rosConnector.GetLatestAsync<dynamic>($"/aircraft/{_options.RosAircraftId}/airspeed");
            var headingTask = _rosConnector.GetLatestAsync<dynamic>($"/aircraft/{_options.RosAircraftId}/heading");
            var phaseTask = _rosConnector.GetLatestAsync<dynamic>($"/aircraft/{_options.RosAircraftId}/flight_phase");

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
            if (gpsData is null || attitudeData is null)
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
        catch (System.Exception ex)
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
            if (altitudeData is not null)
            {
                var separateAltitude = GetPropertyValue<double>(altitudeData, "data");
                if (separateAltitude.HasValue)
                {
                    altitude = separateAltitude.Value * 0.3048; // Convert feet to meters if needed
                }
            }

            return Position.Of(latitude, longitude, altitude);
        }
        catch (System.Exception ex)
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
            if (vector is null) return Attitude.Empty;

            var roll = GetPropertyValue<double>(vector, "x") ?? 0.0;
            var pitch = GetPropertyValue<double>(vector, "y") ?? 0.0;
            var yaw = GetPropertyValue<double>(vector, "z") ?? 0.0;

            return Attitude.Of(roll, pitch, yaw);
        }
        catch (System.Exception ex)
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
        catch (System.Exception ex)
        {
            _logger.LogError(ex, "Error parsing telemetry data");
            return TelemetryData.Empty;
        }
    }

    private CollectedTelemetryData CreateSimulatedTelemetryData()
    {
        // Generate realistic simulated aircraft telemetry data
        var random = new Random();
        
        // Simulate aircraft flying around New York area
        var baseLatitude = 40.7128; // NYC latitude
        var baseLongitude = -74.0060; // NYC longitude
        var baseAltitude = 10000; // 10,000 meters
        
        // Add some realistic variation
        var latitude = baseLatitude + (random.NextDouble() - 0.5) * 0.1; // ±0.05 degrees
        var longitude = baseLongitude + (random.NextDouble() - 0.5) * 0.1;
        var altitude = baseAltitude + (random.NextDouble() - 0.5) * 2000; // ±1000m variation
        
        var position = Position.Of(latitude, longitude, altitude);
        
        // Simulate aircraft attitude (slight banking and pitch variations)
        var roll = (random.NextDouble() - 0.5) * 20; // ±10 degrees
        var pitch = (random.NextDouble() - 0.5) * 10; // ±5 degrees  
        var yaw = random.NextDouble() * 360; // 0-360 degrees
        
        var attitude = Attitude.Of(roll, pitch, yaw);
        
        // Simulate flight telemetry
        var speed = 450 + (random.NextDouble() - 0.5) * 100; // 400-500 knots
        var heading = random.NextDouble() * 360; // 0-360 degrees
        var fuelLevel = 85 + (random.NextDouble() - 0.5) * 30; // 70-100%
        var flightPhase = "CRUISE"; // Simulated cruise phase
        
        var telemetry = TelemetryData.Of(speed, heading, fuelLevel, flightPhase);
        
        _logger.LogDebug("Generated simulated telemetry: Pos({Lat:F4},{Lon:F4},{Alt:F0}), Att({Roll:F1},{Pitch:F1},{Yaw:F1}), Speed: {Speed:F0}kts", 
            latitude, longitude, altitude, roll, pitch, yaw, speed);
        
        return new CollectedTelemetryData(position, attitude, telemetry);
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
                    // For dynamic or object types, return the JsonElement as is
                    if (typeof(T) == typeof(object))
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
    public string RosAircraftId { get; set; } = "AIRCRAFT_001"; // Default to our simulated aircraft
    public int CollectionIntervalSeconds { get; set; } = 1; // 1Hz frequency
    public int MaxRetries { get; set; } = 3;
    public bool EnableLiveData { get; set; } = true;
}
