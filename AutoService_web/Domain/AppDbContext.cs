using Microsoft.EntityFrameworkCore;

namespace AutoService_web.Domain;

public class Item
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
}

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }
    public DbSet<Item> Items { get; set; }
}