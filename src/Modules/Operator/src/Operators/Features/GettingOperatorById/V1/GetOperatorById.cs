namespace Operator.Operators.Features.GettingOperatorById.V1;

using Ardalis.GuardClauses;
using BuildingBlocks.Core.CQRS;
using BuildingBlocks.Web;
using Duende.IdentityServer.EntityFramework.Entities;
using FluentValidation;
using Mapster;
using MapsterMapper;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using MongoDB.Driver;
using MongoDB.Driver.Linq;
using Operator.Data;
using Operator.Operators.Dtos;
using Operator.Operators.Exceptions;

public record GetOperatorById(Guid Id) : IQuery<GetOperatorByIdResult>;

public record GetOperatorByIdResult(OperatorDto OperatorDto);

public record GetOperatorByIdResponseDto(OperatorDto OperatorDto);

public class GetOperatorByIdEndpoint : IMinimalEndpoint
{
    public IEndpointRouteBuilder MapEndpoint(IEndpointRouteBuilder builder)
    {
        builder.MapGet($"{EndpointConfig.BaseApiPath}/operator/{{id}}",
                async (Guid id, IMediator mediator, CancellationToken cancellationToken) =>
                {
                    var result = await mediator.Send(new GetOperatorById(id), cancellationToken);

                    var response = result.Adapt<GetOperatorByIdResponseDto>();

                    return Results.Ok(response);
                })
            .RequireAuthorization(nameof(ApiScope))
            .WithName("GetOperatorById")
            .WithApiVersionSet(builder.NewApiVersionSet("Operator").Build())
            .Produces<GetOperatorByIdResponseDto>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .WithSummary("Get Operator By Id")
            .WithDescription("Get Military Operator By Id")
            .WithOpenApi()
            .HasApiVersion(1.0);

        return builder;
    }
}

public class GetOperatorByIdValidator : AbstractValidator<GetOperatorById>
{
    public GetOperatorByIdValidator()
    {
        RuleFor(x => x.Id).NotNull().WithMessage("Id is required!");
    }
}

internal class GetOperatorByIdHandler : IQueryHandler<GetOperatorById, GetOperatorByIdResult>
{
    private readonly IMapper _mapper;
    private readonly OperatorReadDbContext _operatorReadDbContext;

    public GetOperatorByIdHandler(IMapper mapper, OperatorReadDbContext operatorReadDbContext)
    {
        _mapper = mapper;
        _operatorReadDbContext = operatorReadDbContext;
    }

    public async Task<GetOperatorByIdResult> Handle(GetOperatorById query, CancellationToken cancellationToken)
    {
        Guard.Against.Null(query, nameof(query));

        var operatorEntity =
            await _operatorReadDbContext.Operator.AsQueryable()
                .SingleOrDefaultAsync(x => x.OperatorId == query.Id && x.IsDeleted == false, cancellationToken);

        if (operatorEntity is null)
        {
            throw new OperatorNotFoundException();
        }

        var operatorDto = _mapper.Map<OperatorDto>(operatorEntity);

        return new GetOperatorByIdResult(operatorDto);
    }
}
