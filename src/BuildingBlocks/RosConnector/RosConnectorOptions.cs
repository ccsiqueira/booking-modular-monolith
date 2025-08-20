namespace BuildingBlocks.RosConnector;

public class RosConnectorOptions
{
    public const string SectionName = "RosConnector";

    public string Uri { get; set; } = "ws://localhost:9090";
    public int ConnectionTimeoutMs { get; set; } = 5000;
    public int ReconnectIntervalMs { get; set; } = 10000;
    public bool EnableAutoReconnect { get; set; } = true;
}