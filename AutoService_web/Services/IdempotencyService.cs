using System.Collections.Concurrent;
using AutoService_web.Domain;

namespace AutoService_web.Services;

public class IdempotencyService
{
    private readonly ConcurrentDictionary<string, Order> _store = new();

    public bool TryGet(string key, out Order? order) => _store.TryGetValue(key, out order);

    public void Set(string key, Order order) => _store[key] = order;
}