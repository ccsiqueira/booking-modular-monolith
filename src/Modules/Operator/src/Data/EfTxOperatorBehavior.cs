using System.Data;
using System.Text.Json;
using System.Transactions;
using BuildingBlocks.Core;
using BuildingBlocks.Core.CQRS;
using BuildingBlocks.EFCore;
using BuildingBlocks.Web;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Operator.Data;

public class EfTxOperatorBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull, IRequest<TResponse>
    where TResponse : notnull
{
    private readonly ILogger<EfTxOperatorBehavior<TRequest, TResponse>> _logger;
    private readonly OperatorDbContext _operatorDbContext;
    private readonly IEventDispatcher _eventDispatcher;
    private readonly ICurrentUserProvider _currentUserProvider;

    public EfTxOperatorBehavior(
        ILogger<EfTxOperatorBehavior<TRequest, TResponse>> logger,
        OperatorDbContext operatorDbContext,
        IEventDispatcher eventDispatcher,
        ICurrentUserProvider currentUserProvider)
    {
        _logger = logger;
        _operatorDbContext = operatorDbContext;
        _eventDispatcher = eventDispatcher;
        _currentUserProvider = currentUserProvider;
    }

    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken
    )
    {
        _logger.LogInformation(
            "{Prefix} Handled command {MediatrRequest}",
            GetType().Name,
            typeof(TRequest).FullName);

        _logger.LogDebug(
            "{Prefix} Handled command {MediatrRequest} with content {RequestContent}",
            GetType().Name,
            typeof(TRequest).FullName,
            JsonSerializer.Serialize(request));

        var response = await next();

        _logger.LogInformation(
            "{Prefix} Executed the {MediatrRequest} request",
            GetType().Name,
            typeof(TRequest).FullName);

        while (true)
        {
            var domainEvents = _operatorDbContext.GetDomainEvents();

            if (domainEvents is null || !domainEvents.Any())
            {
                return response;
            }

            _logger.LogInformation(
                "{Prefix} Open the transaction for {MediatrRequest}",
                GetType().Name,
                typeof(TRequest).FullName);

            using var scope = new TransactionScope(
                TransactionScopeOption.Required,
                new TransactionOptions { IsolationLevel = System.Transactions.IsolationLevel.ReadCommitted },
                TransactionScopeAsyncFlowOption.Enabled);

            await _eventDispatcher.SendAsync(
                domainEvents.ToArray(),
                typeof(TRequest),
                cancellationToken);

            scope.Complete();

            _logger.LogInformation(
                "{Prefix} Completed the transaction for {MediatrRequest}",
                GetType().Name,
                typeof(TRequest).FullName);
        }
    }
}
