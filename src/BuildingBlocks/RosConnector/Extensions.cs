using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace BuildingBlocks.RosConnector;

public static class Extensions
{
    public static IServiceCollection AddRosConnector(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<RosConnectorOptions>(configuration.GetSection(RosConnectorOptions.SectionName));
        services.AddSingleton<IRosConnectorService, RosConnectorService>();

        return services;
    }

    public static IServiceCollection AddRosConnector(this IServiceCollection services, Action<RosConnectorOptions> configureOptions)
    {
        services.Configure(configureOptions);
        services.AddSingleton<IRosConnectorService, RosConnectorService>();

        return services;
    }
}