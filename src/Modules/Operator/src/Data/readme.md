dotnet ef migrations add initial --context OperatorDbContext -o "Data\Migrations"
dotnet ef database update --context OperatorDbContext
