using BuildingBlocks.Mongo;
using Humanizer;
using Microsoft.Extensions.Options;
using MongoDB.Driver;

namespace Operator.Data;

using Operators.Models;

public class OperatorReadDbContext : MongoDbContext
{
    public OperatorReadDbContext(IOptions<MongoOptions> options) : base(options)
    {
        Operator = GetCollection<OperatorReadModel>(nameof(Operator).Underscore());
    }

    public IMongoCollection<OperatorReadModel> Operator { get; }
}