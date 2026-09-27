using AutoService_web.Services;
using AutoService_web.Domain;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace AutoService_web.Tests;

public class CatalogServiceTests
{
    private AppDbContext GetInMemoryDb()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        return new AppDbContext(options);
    }

    [Fact]
    public void Create_ValidData_ReturnsSuccess()
    {
        var db = GetInMemoryDb();
        var service = new CatalogService(db);
        var request = new ServiceItemRequest("Ремонт двигуна");

        var result = service.Create(request);

        Assert.NotNull(result);
        Assert.Equal("Ремонт двигуна", result.Name);
        Assert.True(result.Id > 0);
    }

    [Fact]
    public void Create_EmptyName_ThrowsArgumentException()
    {
        var db = GetInMemoryDb();
        var service = new CatalogService(db);
        var request = new ServiceItemRequest("");

        Assert.Throws<ArgumentException>(() => service.Create(request));
    }
}