using System.Reflection;
using BuildingBlocks.EFCore;
using BuildingBlocks.Web;
using Microsoft.EntityFrameworkCore;

namespace Operator.Data;

using Microsoft.Extensions.Logging;

public sealed class OperatorDbContext : AppDbContextBase
{
    public OperatorDbContext(DbContextOptions<OperatorDbContext> options,
        ICurrentUserProvider? currentUserProvider = null, ILogger<OperatorDbContext>? logger = null) :
        base(options, currentUserProvider, logger)
    {
    }

    public DbSet<Operators.Models.Operator> Operators => Set<Operators.Models.Operator>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        builder.ApplyConfigurationsFromAssembly(Assembly.GetExecutingAssembly());
        base.OnModelCreating(builder);
        builder.FilterSoftDeletedProperties();
        builder.ToSnakeCaseTables();
    }
}