using Grpc.Core;
using MediatR;

namespace Operator.GrpcServer.Services;

using Mapster;
using Operators.Features.GettingOperatorById.V1;

public class OperatorGrpcServices : OperatorGrpcService.OperatorGrpcServiceBase
{
    private readonly IMediator _mediator;

    public OperatorGrpcServices(IMediator mediator)
    {
        _mediator = mediator;
    }

    public override async Task<global::Operator.GetOperatorByIdResult> GetById(global::Operator.GetByIdRequest request, ServerCallContext context)
    {
        var result = await _mediator.Send(new GetOperatorById(new Guid(request.Id)));
        return result?.Adapt<global::Operator.GetOperatorByIdResult>();
    }
}