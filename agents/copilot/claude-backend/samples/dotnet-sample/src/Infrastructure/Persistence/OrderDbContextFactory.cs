using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Project.Infrastructure.Persistence;

public class OrderDbContextFactory : IDesignTimeDbContextFactory<OrderDbContext>
{
    public OrderDbContext CreateDbContext(string[] args)
    {
        var conn = Environment.GetEnvironmentVariable("CONNECTION") ?? "Host=localhost;Database=kim;Username=kim;Password=kim";
        var builder = new DbContextOptionsBuilder<OrderDbContext>();
        builder.UseNpgsql(conn);
        return new OrderDbContext(builder.Options);
    }
}
