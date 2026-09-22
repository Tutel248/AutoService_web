using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddCors();
builder.Services.AddDbContext<AppDbContext>(opt =>
    opt.UseSqlite("Data Source=autoservice.db"));

var app = builder.Build();
app.UseCors(x => x.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader());

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.EnsureCreated();
    if (!db.Items.Any())
    {
        db.Items.AddRange(
            new Item { Name = "Діагностика" },
            new Item { Name = "Ремонт двигуна" },
            new Item { Name = "Ремонт підвіски" },
            new Item { Name = "Кузовні роботи" }
        );
        db.SaveChanges();
    }
}

app.MapGet("/items", (AppDbContext db) => db.Items.ToList());

app.Run();

class Item
{
    public int Id { get; set; }
    public string Name { get; set; }
}

class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }
    public DbSet<Item> Items { get; set; }
}