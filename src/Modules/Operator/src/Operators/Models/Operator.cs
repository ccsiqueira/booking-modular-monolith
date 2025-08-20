using BuildingBlocks.Core.Model;

namespace Operator.Operators.Models;

using Features.CompletingRegisterOperator.V1;
using Identity.Consumers.RegisteringNewUser.V1;
using ValueObjects;

public record Operator : Aggregate<OperatorId>
{
    public Name Name { get; private set; } = default!;
    public WarName WarName { get; private set; } = default!;
    public Rank Rank { get; private set; } = default!;
    public Organization Organization { get; private set; } = default!;

    public void CompleteRegistrationOperator(OperatorId id, Name name, WarName warName,
        Rank rank, Organization organization, bool isDeleted = false)
    {
        Id = id;
        Name = name;
        WarName = warName;
        Rank = rank;
        Organization = organization;
        IsDeleted = isDeleted;

        var @event = new OperatorRegistrationCompletedDomainEvent(Id, Name,
            WarName, Rank, Organization, IsDeleted);

        AddDomainEvent(@event);
    }

    public static Operator Create(OperatorId id, Name name, WarName warName, bool isDeleted = false)
    {
        var operatorEntity = new Operator { Id = id, Name = name, WarName = warName, IsDeleted = isDeleted };

        var @event = new Events.OperatorCreatedDomainEvent(operatorEntity.Id, operatorEntity.Name, operatorEntity.WarName,
            operatorEntity.IsDeleted);

        operatorEntity.AddDomainEvent(@event);

        return operatorEntity;
    }
}
