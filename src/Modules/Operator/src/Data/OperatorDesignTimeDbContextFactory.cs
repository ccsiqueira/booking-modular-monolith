using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Operator.Data;

public class OperatorDesignTimeDbContextFactory : IDesignTimeDbContextFactory<OperatorDbContext>
{
    public OperatorDbContext CreateDbContext(string[] args)
    {
        var builder = new DbContextOptionsBuilder<OperatorDbContext>();

        builder.UseNpgsql("Server=localhost;Port=5432;Database=prisma_server_operator;User Id=postgres;Password=postgres;Include Error Detail=true")
            .UseSnakeCaseNamingConvention();
        return new OperatorDbContext(builder.Options);
    }
}
