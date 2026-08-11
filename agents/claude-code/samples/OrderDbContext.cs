using Microsoft.EntityFrameworkCore;
using Project.Domain.Orders;

namespace Project.Infrastructure.Persistence;

public class OrderDbContext : DbContext
{
    public OrderDbContext(DbContextOptions<OrderDbContext> options) : base(options) {}

    public DbSet<OrderReadModel> Orders { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("orders");

        modelBuilder.Entity<OrderReadModel>(b =>
        {
            b.ToTable("orders");
            b.HasKey(x => x.Id);
            b.Property(x => x.Id).HasConversion(v => v.Value, v => new OrderId(v));
            b.Property(x => x.Title).HasMaxLength(200);
        });
    }
}

public class OrderReadModel
{
    public OrderId Id { get; set; }
    public string Title { get; set; }
}
