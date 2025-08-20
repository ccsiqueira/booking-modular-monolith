namespace Operator.Operators.Features.CompletingRegisterOperator.V1;

using Ardalis.GuardClauses;
using BuildingBlocks.Core.CQRS;
using BuildingBlocks.Core.Event;
using BuildingBlocks.Web;
using Data;
using Dtos;
using Duende.IdentityServer.EntityFramework.Entities;
using Exceptions;
using FluentValidation;
using Mapster;
using MapsterMapper;
using MassTransit;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Operator.Operators.Enums;
using Operator.Operators.ValueObjects;

public record CompleteRegisterOperator
    (string WarName, string Rank, int RankLevel, string Organization, string OrganizationCode) : ICommand<CompleteRegisterOperatorResult>,
        IInternalCommand
{
    public Guid Id { get; init; } = NewId.NextGuid();
}

public record OperatorRegistrationCompletedDomainEvent(Guid Id, string Name, string WarName,
    Rank Rank, Organization Organization, bool IsDeleted = false) : IDomainEvent;

public record CompleteRegisterOperatorResult(OperatorDto OperatorDto);

public record CompleteRegisterOperatorRequestDto(string WarName, string Rank, int RankLevel, string Organization, string OrganizationCode);

public record CompleteRegisterOperatorResponseDto(OperatorDto OperatorDto);

public class CompleteRegisterOperatorEndpoint : IMinimalEndpoint
{
    public IEndpointRouteBuilder MapEndpoint(IEndpointRouteBuilder builder)
    {
        builder.MapPost($"{EndpointConfig.BaseApiPath}/operator/complete-registration", async (
                CompleteRegisterOperatorRequestDto request, IMapper mapper,
                IMediator mediator, CancellationToken cancellationToken, IHttpContextAccessor httpContextAccessor) =>
            {
                var command = mapper.Map<CompleteRegisterOperator>(request);

                var result = await mediator.Send(command, cancellationToken);

                var response = result.Adapt<CompleteRegisterOperatorResponseDto>();

                return Results.Ok(response);
            })
            .RequireAuthorization(nameof(ApiScope))
            .WithName("CompleteRegisterOperator")
            .WithApiVersionSet(builder.NewApiVersionSet("Operator").Build())
            .Produces<CompleteRegisterOperatorResponseDto>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .WithSummary("Complete Register Operator")
            .WithDescription("Complete Register Military Operator")
            .WithOpenApi()
            .HasApiVersion(1.0);

        return builder;
    }
}

public class CompleteRegisterOperatorValidator : AbstractValidator<CompleteRegisterOperator>
{
    public CompleteRegisterOperatorValidator()
    {
        RuleFor(x => x.WarName).NotEmpty().WithMessage("The WarName is required!");
        RuleFor(x => x.Rank).NotEmpty().WithMessage("The Rank is required!");
        RuleFor(x => x.RankLevel).GreaterThan(0).WithMessage("The RankLevel must be greater than 0!");
        RuleFor(x => x.Organization).NotEmpty().WithMessage("The Organization is required!");
        RuleFor(x => x.OrganizationCode).NotEmpty().WithMessage("The OrganizationCode is required!");
    }
}

internal class CompleteRegisterOperatorCommandHandler : ICommandHandler<CompleteRegisterOperator,
    CompleteRegisterOperatorResult>
{
    private readonly IMapper _mapper;
    private readonly OperatorDbContext _operatorDbContext;

    public CompleteRegisterOperatorCommandHandler(IMapper mapper, OperatorDbContext operatorDbContext)
    {
        _mapper = mapper;
        _operatorDbContext = operatorDbContext;
    }

    public async Task<CompleteRegisterOperatorResult> Handle(CompleteRegisterOperator request,
        CancellationToken cancellationToken)
    {
        Guard.Against.Null(request, nameof(request));

        var operatorEntity = await _operatorDbContext.Operators.SingleOrDefaultAsync(
            x => x.WarName.Value == request.WarName, cancellationToken);

        if (operatorEntity is null)
        {
            throw new OperatorNotExist();
        }

        var rank = Rank.Of(request.Rank, request.RankLevel, RankType.Officer); // Default to Officer, can be improved
        var organization = Organization.Of(request.Organization, request.OrganizationCode);

        operatorEntity.CompleteRegistrationOperator(operatorEntity.Id, operatorEntity.Name,
            operatorEntity.WarName, rank, organization);

        var updateOperator = _operatorDbContext.Operators.Update(operatorEntity).Entity;

        var operatorDto = _mapper.Map<OperatorDto>(updateOperator);

        return new CompleteRegisterOperatorResult(operatorDto);
    }
}
