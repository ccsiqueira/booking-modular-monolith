namespace BuildingBlocks.RosConnector;

public interface IRosConnectorService
{
    Task<bool> ConnectAsync(string rosUri);
    Task<bool> IsConnectedAsync();
    Task<T?> GetLatestAsync<T>(string topicName, int timeoutMs = 1000) where T : class;
    Task DisconnectAsync();
}
