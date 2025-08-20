namespace Operator.Operators.Features.CompletingRegisterOperator.V1;

using Ardalis.GuardClauses;
using BuildingBlocks.Core.CQRS;
using BuildingBlocks.Core.Event;
using Data;
using MapsterMapper;
using MediatR;
using Models;

public record CompleteRegisterOperatorMongoCommand(Guid Id, string Name, string WarName, string Rank, 
    int RankLevel, string Organization, string OrganizationCode, bool IsDeleted = false) : InternalCommand;

internal class CompleteRegisterOperatorMongoCommandHandler : ICommandHandler<CompleteRegisterOperatorMongoCommand>
{
    private readonly IMapper _mapper;
    private readonly OperatorReadDbContext _operatorReadDbContext;

    public CompleteRegisterOperatorMongoCommandHandler(IMapper mapper, OperatorReadDbContext operatorReadDbContext)
    {
        _mapper = mapper;
        _operatorReadDbContext = operatorReadDbContext;
    }

    public async Task<Unit> Handle(CompleteRegisterOperatorMongoCommand request, CancellationToken cancellationToken)
    {
        Guard.Against.Null(request, nameof(request));

        var operatorReadModel = _mapper.Map<OperatorReadModel>(request);

        await _operatorReadDbContext.Operator.InsertOneAsync(operatorReadModel, cancellationToken: cancellationToken);
        
        return Unit.Value;
    }
}
