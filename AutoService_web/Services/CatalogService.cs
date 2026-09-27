using AutoService_web.Domain;

namespace AutoService_web.Services;

public record ServiceItemRequest(string Name);
public record ServiceItemResponse(int Id, string Name);
public record ErrorResponse(string Error, string Code);

public class CatalogService
{
    private readonly AppDbContext _db;

    public CatalogService(AppDbContext db)
    {
        _db = db;
    }

    public List<ServiceItemResponse> GetAll()
    {
        return _db.Items.Select(i => new ServiceItemResponse(i.Id, i.Name)).ToList();
    }

    public ServiceItemResponse? GetById(int id)
    {
        var item = _db.Items.FirstOrDefault(i => i.Id == id);
        return item != null ? new ServiceItemResponse(item.Id, item.Name) : null;
    }

    public ServiceItemResponse Create(ServiceItemRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
            throw new ArgumentException("NAME_REQUIRED");

        var item = new Item { Name = request.Name };
        _db.Items.Add(item);
        _db.SaveChanges();

        return new ServiceItemResponse(item.Id, item.Name);
    }

    public ServiceItemResponse? Update(int id, ServiceItemRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
            throw new ArgumentException("NAME_REQUIRED");

        var item = _db.Items.FirstOrDefault(i => i.Id == id);
        if (item == null) return null;

        item.Name = request.Name;
        _db.SaveChanges();

        return new ServiceItemResponse(item.Id, item.Name);
    }

    public bool Delete(int id)
    {
        var item = _db.Items.FirstOrDefault(i => i.Id == id);
        if (item == null) return false;

        _db.Items.Remove(item);
        _db.SaveChanges();
        return true;
    }
}