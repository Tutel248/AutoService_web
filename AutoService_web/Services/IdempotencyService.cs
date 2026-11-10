using System.Collections.Concurrent;
using AutoService_web.Domain;

namespace AutoService_web.Services;

public class IdempotencyService
{
    private readonly ConcurrentDictionary<string, Order> _store = new();

    public Order GetOrAdd(string key, Func<Order> createFunc)
    {
        return _store.GetOrAdd(key, _ => createFunc());
    }
}