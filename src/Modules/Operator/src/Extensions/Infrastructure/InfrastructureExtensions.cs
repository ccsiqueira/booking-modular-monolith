using BuildingBlocks.EFCore;
using BuildingBlocks.Mapster;
using BuildingBlocks.Mongo;
using BuildingBlocks.Web;
using FluentValidation;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Operator.Data;
using Operator.GrpcServer.Services;

namespace Operator.Extensions.Infrastructure;

public static class InfrastructureExtensions
{
    public static WebApplicationBuilder AddOperatorModules(this WebApplicationBuilder builder)
    {
        builder.Services.AddScoped<OperatorEventMapper>();
        builder.AddMinimalEndpoints(assemblies: typeof(OperatorRoot).Assembly);
        builder.Services.AddValidatorsFromAssembly(typeof(OperatorRoot).Assembly);
        builder.Services.AddCustomMapster(typeof(OperatorRoot).Assembly);
        builder.AddCustomDbContext<OperatorDbContext>(nameof(Operator));
        builder.AddMongoDbContext<OperatorReadDbContext>();

        builder.Services.AddCustomMediatR();

        return builder;
    }


    public static WebApplication UseOperatorModules(this WebApplication app)
    {
        app.UseMigration<OperatorDbContext>();
        app.MapGrpcService<OperatorGrpcServices>();

        return app;
    }
}