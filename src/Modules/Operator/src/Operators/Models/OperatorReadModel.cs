namespace Operator.Operators.Models;

public class OperatorReadModel
{
    public required Guid Id { get; init; }
    public required Guid OperatorId { get; init; }
    public required string Name { get; init; }
    public required string WarName { get; init; }
    public required string Rank { get; init; }
    public required int RankLevel { get; init; }
    public required string Organization { get; init; }
    public required string OrganizationCode { get; init; }
    public required bool IsDeleted { get; init; }
}
