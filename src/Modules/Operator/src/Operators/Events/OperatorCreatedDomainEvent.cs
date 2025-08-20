using BuildingBlocks.Core.Event;

namespace Operator.Operators.Events;

public record OperatorCreatedDomainEvent(Guid Id, string Name, string WarName, bool IsDeleted = false) : IDomainEvent;
