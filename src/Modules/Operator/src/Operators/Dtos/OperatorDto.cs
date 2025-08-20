namespace Operator.Operators.Dtos;

public record OperatorDto(
    Guid Id, 
    string Name, 
    string WarName, 
    string Rank, 
    int RankLevel,
    string Organization, 
    string OrganizationCode
);
