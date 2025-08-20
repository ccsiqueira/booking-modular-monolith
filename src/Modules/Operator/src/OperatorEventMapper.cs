using BuildingBlocks.Contracts.EventBus.Messages;
using BuildingBlocks.Core;
using BuildingBlocks.Core.Event;

namespace Operator;

using Operator.Operators.Events;
using Operator.Operators.Features.CompletingRegisterOperator.V1;

public sealed class OperatorEventMapper : IEventMapper
{
    public IIntegrationEvent? MapToIntegrationEvent(IDomainEvent @event)
    {
        return @event switch
        {
            OperatorRegistrationCompletedDomainEvent e => new OperatorRegistrationCompleted(e.Id),
            OperatorCreatedDomainEvent e => new OperatorCreated(e.Id),
            _ => null
        };
    }

    public IInternalCommand? MapToInternalCommand(IDomainEvent @event)
    {
        return @event switch
        {
            OperatorRegistrationCompletedDomainEvent e => new CompleteRegisterOperatorMongoCommand(
                e.Id, 
                e.Name, 
                e.WarName, 
                e.Rank.Value, 
                e.Rank.Level, 
                e.Organization.Value, 
                e.Organization.Code, 
                e.IsDeleted),
            OperatorCreatedDomainEvent e => new CompleteRegisterOperatorMongoCommand(
                e.Id, 
                e.Name, 
                e.WarName, 
                "", // Empty rank name for initial creation
                0,  // Default rank level
                "", // Empty organization name
                "", // Empty organization code
                e.IsDeleted),
            _ => null
        };
    }
}
