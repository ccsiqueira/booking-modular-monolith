namespace Operator.Identity.Consumers.RegisteringNewUser.V1;

using Ardalis.GuardClauses;
using BuildingBlocks.Contracts.EventBus.Messages;
using BuildingBlocks.Core;
using BuildingBlocks.Core.Event;
using BuildingBlocks.Web;
using Data;
using Humanizer;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Operator.Operators.ValueObjects;
using Operator.Operators.Events;

public class RegisterNewUserHandler : IConsumer<UserCreated>
{
    private readonly OperatorDbContext _operatorDbContext;
    private readonly IEventDispatcher _eventDispatcher;
    private readonly ILogger<RegisterNewUserHandler> _logger;
    private readonly AppOptions _options;

    public RegisterNewUserHandler(OperatorDbContext operatorDbContext,
        IEventDispatcher eventDispatcher,
        ILogger<RegisterNewUserHandler> logger,
        IOptions<AppOptions> options)
    {
        _operatorDbContext = operatorDbContext;
        _eventDispatcher = eventDispatcher;
        _logger = logger;
        _options = options.Value;
    }

    public async Task Consume(ConsumeContext<UserCreated> context)
    {
        Guard.Against.Null(context.Message, nameof(UserCreated));

        _logger.LogInformation($"consumer for {nameof(UserCreated).Underscore()} in {_options.Name}");

        // Check if operator already exists by email (since we don't have passport number anymore)
        var operatorExist =
            await _operatorDbContext.Operators.AnyAsync(x => x.Name.Value == context.Message.Name);

        if (operatorExist)
        {
            return;
        }

        // Create operator with basic information from user creation
        // WarName will be set later during registration completion
        var operatorEntity = Operators.Models.Operator.Create(
            OperatorId.Of(NewId.NextGuid()), 
            Name.Of(context.Message.Name),
            WarName.Of(context.Message.Name.Replace(" ", "_", StringComparison.Ordinal)) // Temporary warname based on name
        );

        await _operatorDbContext.AddAsync(operatorEntity);

        await _operatorDbContext.SaveChangesAsync();

        await _eventDispatcher.SendAsync(
            new OperatorCreatedDomainEvent(operatorEntity.Id.Value, operatorEntity.Name.Value, operatorEntity.WarName.Value),
            typeof(IInternalCommand));
    }
}