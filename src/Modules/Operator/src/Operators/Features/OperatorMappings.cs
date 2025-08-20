using Mapster;

namespace Operator.Operators.Features;

using CompletingRegisterOperator.V1;
using Dtos;
using MassTransit;
using Models;
using ValueObjects;

public class OperatorMappings : IRegister
{
    public void Register(TypeAdapterConfig config)
    {
        config.NewConfig<CompleteRegisterOperatorMongoCommand, OperatorReadModel>()
            .Map(d => d.Id, s => NewId.NextGuid())
            .Map(d => d.OperatorId, s => OperatorId.New());

        config.NewConfig<CompleteRegisterOperatorRequestDto, CompleteRegisterOperator>()
            .ConstructUsing(x => new CompleteRegisterOperator(x.WarName, x.Rank, x.RankLevel, x.Organization, x.OrganizationCode));

        config.NewConfig<OperatorReadModel, OperatorDto>()
            .ConstructUsing(x => new OperatorDto(x.OperatorId, x.Name, x.WarName, x.Rank, x.RankLevel, x.Organization, x.OrganizationCode));

        config.NewConfig<Operator, OperatorDto>()
            .ConstructUsing(x => new OperatorDto(x.Id.Value, x.Name.Value, x.WarName.Value, x.Rank.Value, x.Rank.Level, x.Organization.Value, x.Organization.Code));
    }
}
