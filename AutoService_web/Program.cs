using Microsoft.EntityFrameworkCore;
using AutoService_web.Domain;
using AutoService_web.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddCors();
builder.Services.AddDbContext<AppDbContext>(opt =>
    opt.UseSqlite(@"Data Source=autoservice.db"));

builder.Services.AddScoped<CatalogService>();

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
            new Item { Name = "Ремонт двигуна" }
        );
        db.SaveChanges();
    }
}


app.MapGet("/health", () => Results.Ok("ok"));

app.MapGet("/services", (CatalogService service) => Results.Ok(service.GetAll()));

app.MapGet("/services/{id}", (int id, CatalogService service) =>
{
    var result = service.GetById(id);
    return result != null ? Results.Ok(result) : Results.NotFound(new ErrorResponse("Not Found", "ITEM_NOT_FOUND"));
});

app.MapPost("/services", (ServiceItemRequest req, CatalogService service) =>
{
    try
    {
        var result = service.Create(req);
        return Results.Created($"/services/{result.Id}", result);
    }
    catch (ArgumentException)
    {
        return Results.BadRequest(new ErrorResponse("Validation Error", "NAME_REQUIRED"));
    }
});

app.MapPut("/services/{id}", (int id, ServiceItemRequest req, CatalogService service) =>
{
    try
    {
        var result = service.Update(id, req);
        return result != null ? Results.Ok(result) : Results.NotFound(new ErrorResponse("Not Found", "ITEM_NOT_FOUND"));
    }
    catch (ArgumentException)
    {
        return Results.BadRequest(new ErrorResponse("Validation Error", "NAME_REQUIRED"));
    }
});

app.MapDelete("/services/{id}", (int id, CatalogService service) =>
{
    var success = service.Delete(id);
    return success ? Results.NoContent() : Results.NotFound(new ErrorResponse("Not Found", "ITEM_NOT_FOUND"));
});

app.Run();