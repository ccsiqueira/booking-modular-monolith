using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using BuildingBlocks.Core;
using BuildingBlocks.Core.Event;
using Flight.Aircrafts.Events;
using MediatR;
using BuildingBlocks.RosConnector;
using Flight.Aircrafts.Features.GettingAircraftTelemetry.V1;
using Flight.Aircrafts.Models;
using Flight.Aircrafts.ValueObjects;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

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
        var eventDispatcher = scope.ServiceProvider.GetRequiredService<IEventDispatcher>();

        try
        {
            // Collect telemetry data from ROS2 topics
            var telemetryData = await CollectTelemetryFromRosAsync();

            if (telemetryData == null)
            {
                _logger.LogDebug("No telemetry data received from ROS2");
                return;
            }

            // Convert string aircraft ID to Guid
            var aircraftGuid = ConvertStringToGuid(_options.AircraftId);

            // Create and publish telemetry event directly
            // Note: This bypasses the Aircraft entity and publishes telemetry events independently
            var telemetryEvent = new AircraftTelemetryUpdatedDomainEvent(
                aircraftGuid,
                telemetryData.Position ?? Position.Empty,
                telemetryData.Attitude ?? Attitude.Empty,
                telemetryData.TelemetryData ?? TelemetryData.Empty,
                DateTime.UtcNow);

            // Dispatch the event (will be handled by projections, integrations, etc.)
            await eventDispatcher.SendAsync(telemetryEvent, cancellationToken: cancellationToken);

            _logger.LogDebug("Published telemetry event for aircraft {AircraftId}: {Position}, {Attitude}, {TelemetryData}",
                aircraftGuid, telemetryData.Position, telemetryData.Attitude, telemetryData.TelemetryData);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error publishing aircraft telemetry event");
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

            // Debug each topic data
            _logger.LogInformation($"Topic data status:");
            _logger.LogInformation($"  GPS: {GetDataStatus(gpsData)} - {GetDataContent(gpsData)}");
            _logger.LogInformation($"  Attitude: {GetDataStatus(attitudeData)} - {GetDataContent(attitudeData)}");
            _logger.LogInformation($"  Velocity: {GetDataStatus(velocityData)} - {GetDataContent(velocityData)}");
            _logger.LogInformation($"  Battery: {GetDataStatus(batteryData)} - {GetDataContent(batteryData)}");
            _logger.LogInformation($"  Altitude: {GetDataStatus(altitudeData)} - {GetDataContent(altitudeData)}");
            _logger.LogInformation($"  Airspeed: {GetDataStatus(airspeedData)} - {GetDataContent(airspeedData)}");
            _logger.LogInformation($"  Heading: {GetDataStatus(headingData)} - {GetDataContent(headingData)}");
            _logger.LogInformation($"  Phase: {GetDataStatus(phaseData)} - {GetDataContent(phaseData)}");

            // Parse and validate the collected data
            //if (gpsData is null || attitudeData is null || 
            //    (gpsData is JsonElement gpsElement && gpsElement.ValueKind == JsonValueKind.Null) ||
            //    (attitudeData is JsonElement attElement && attElement.ValueKind == JsonValueKind.Null))
            if (gpsData is null || 
                (gpsData is JsonElement gpsElement && gpsElement.ValueKind == JsonValueKind.Null))
            {
                _logger.LogDebug("Missing essential telemetry data (GPS or attitude)");
                return null;
            }

            // Convert dynamic objects to our value objects
            var position = ParsePosition(gpsData, altitudeData);
            var attitude = ParseAttitude(attitudeData);
            var telemetry = ParseTelemetryData(airspeedData, headingData, batteryData, phaseData);

            return new CollectedTelemetryData(position, null, telemetry);
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
            var gpsType = HasValidData(gpsData) ? gpsData.GetType().Name : "null";
            _logger.LogDebug($"Parsing GPS data of type: {gpsType}");
            
            // Parse GPS data
            var latitude = GetPropertyValue<double>(gpsData, "latitude");
            var longitude = GetPropertyValue<double>(gpsData, "longitude");
            var altitude = GetPropertyValue<double>(gpsData, "altitude");

            var latValue = latitude ?? 0.0;
            var lonValue = longitude ?? 0.0;
            var altValue = altitude ?? 0.0;
            _logger.LogDebug($"Extracted values: Lat={latValue}, Lon={lonValue}, Alt={altValue}");

            var finalLatitude = latitude ?? 0.0;
            var finalLongitude = longitude ?? 0.0;
            var finalAltitude = altitude ?? 0.0;

            // Use separate altitude if available
            if (HasValidData(altitudeData))
            {
                var separateAltitude = GetPropertyValue<double>(altitudeData, "data");
                if (separateAltitude != null)
                {
                    var originalAltitude = (double)separateAltitude;
                    finalAltitude = originalAltitude * 0.3048; // Convert feet to meters if needed
                    _logger.LogDebug($"Using separate altitude: {originalAltitude} -> {finalAltitude}");
                }
            }

            var result = Position.Of(finalLatitude, finalLongitude, finalAltitude);
            _logger.LogDebug($"Created position: {result}");
            return result;
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
            if (!HasValidData(vector)) return Attitude.Empty;

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
        if (obj is null) 
        {
            _logger.LogDebug("GetPropertyValue: obj is null for property '{PropertyName}'", propertyName);
            return default;
        }

        try
        {
            _logger.LogDebug("GetPropertyValue: Getting property '{PropertyName}'", propertyName);
            
            // Handle JsonElement
            if (obj is JsonElement element)
            {
                _logger.LogDebug("GetPropertyValue: Processing JsonElement for property '{PropertyName}'", propertyName);
                
                if (element.TryGetProperty(propertyName, out var property))
                {
                    _logger.LogDebug("GetPropertyValue: Found property '{PropertyName}' in JsonElement", propertyName);
                    
                    if (typeof(T) == typeof(double) || typeof(T) == typeof(double?))
                    {
                        var doubleValue = property.GetDouble();
                        _logger.LogDebug("GetPropertyValue: Converted '{PropertyName}' to double: {Value}", propertyName, doubleValue);
                        return (T)(object)doubleValue;
                    }
                    if (typeof(T) == typeof(string))
                    {
                        var stringValue = property.GetString()!;
                        _logger.LogDebug("GetPropertyValue: Converted '{PropertyName}' to string: '{Value}'", propertyName, stringValue);
                        return (T)(object)stringValue;
                    }
                    if (typeof(T) == typeof(object))
                    {
                        _logger.LogDebug("GetPropertyValue: Returning '{PropertyName}' as object", propertyName);
                        return (T)(object)property;
                    }
                }
                else
                {
                    _logger.LogWarning("GetPropertyValue: Property '{PropertyName}' not found in JsonElement", propertyName);
                }
                return default;
            }

            // Handle dynamic object properties
            var type = obj.GetType();
            _logger.LogDebug("GetPropertyValue: Processing dynamic object");
            
            var propertyInfo = type.GetProperty(propertyName);
            if (propertyInfo != null)
            {
                var value = propertyInfo.GetValue(obj);
                _logger.LogDebug("GetPropertyValue: Found property '{PropertyName}' with value", propertyName);
                
                if (value is T typedValue)
                {
                    return typedValue;
                }
            }
            else
            {
                _logger.LogWarning("GetPropertyValue: Property '{PropertyName}' not found", propertyName);
            }

            return default;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "GetPropertyValue: Exception getting property '{PropertyName}'", propertyName);
            return default;
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Aircraft Telemetry Collector Service stopping...");
        await _rosConnector.DisconnectAsync();
        await base.StopAsync(cancellationToken);
    }

    /// <summary>
    /// Converts a string aircraft ID to "3c5c0000-97c6-fc34-fcd3-08db322230c8".
    /// This ensures the same string always maps to the same Guid.
    /// </summary>
    private static Guid ConvertStringToGuid(string aircraftId)
    {
        if (string.IsNullOrWhiteSpace(aircraftId))
        {
            return Guid.Empty;
        }

        //using var md5 = MD5.Create();
        //var hash = md5.ComputeHash(Encoding.UTF8.GetBytes(aircraftId));
        return new Guid("3c5c0000-97c6-fc34-fcd3-08db322230c8");
    }

    /// <summary>
    /// Safely determines if data is null or has content, handling JsonElement edge cases
    /// </summary>
    private string GetDataStatus(dynamic data)
    {
        if (data is null) return "NULL";
        if (data is JsonElement element && element.ValueKind == JsonValueKind.Null) return "JSON_NULL";
        return "RECEIVED";
    }

    /// <summary>
    /// Safely checks if data is valid (not null), handling JsonElement edge cases
    /// </summary>
    private bool HasValidData(dynamic data)
    {
        if (data is null) return false;
        if (data is JsonElement element && element.ValueKind == JsonValueKind.Null) return false;
        return true;
    }

    /// <summary>
    /// Safely gets string representation of data, handling JsonElement edge cases
    /// </summary>
    private string GetDataContent(dynamic data)
    {
        if (data is null) return "No data";
        if (data is JsonElement element && element.ValueKind == JsonValueKind.Null) return "JsonElement null";
        try
        {
            return data.ToString();
        }
        catch
        {
            return "Error getting content";
        }
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