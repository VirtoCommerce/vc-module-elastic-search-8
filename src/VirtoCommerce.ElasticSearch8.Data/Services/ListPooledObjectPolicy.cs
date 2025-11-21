using System.Collections.Generic;
using Microsoft.Extensions.ObjectPool;

namespace VirtoCommerce.ElasticSearch8.Data.Services;

internal sealed class ListPooledObjectPolicy<T> : PooledObjectPolicy<List<T>>
{
    public int InitialCapacity { get; set; }

    public int MaximumRetainedCapacity { get; set; } = 1024;

    public override List<T> Create() => new(InitialCapacity);

    public override bool Return(List<T> obj)
    {
        if (obj.Capacity > MaximumRetainedCapacity)
        {
            return false;
        }

        obj.Clear();

        return true;
    }
}
