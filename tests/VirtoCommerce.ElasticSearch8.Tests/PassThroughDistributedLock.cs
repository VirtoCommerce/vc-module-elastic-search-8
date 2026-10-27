using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using VirtoCommerce.Platform.Core.DistributedLock;

namespace VirtoCommerce.ElasticSearch8.Tests;

/// <summary>
/// Grants every lock at once and records the requested resources and waits.
/// </summary>
public sealed class PassThroughDistributedLock : IDistributedLock
{
    public ConcurrentQueue<(string Resource, TimeSpan? Timeout)> Requests { get; } = new();

    public Task<IDistributedLockHandle> AcquireAsync(string resource, TimeSpan? timeout = null, CancellationToken cancellationToken = default)
    {
        Requests.Enqueue((resource, timeout));
        return Task.FromResult<IDistributedLockHandle>(new Handle(resource));
    }

    public Task<IDistributedLockHandle> TryAcquireAsync(string resource, TimeSpan timeout = default, CancellationToken cancellationToken = default)
    {
        Requests.Enqueue((resource, timeout));
        return Task.FromResult<IDistributedLockHandle>(new Handle(resource));
    }

    private sealed class Handle(string resource) : IDistributedLockHandle
    {
        public string Resource { get; } = resource;

        // The pass-through lock is never lost.
        public CancellationToken HandleLostToken => CancellationToken.None;

        public void Dispose()
        {
        }

        public ValueTask DisposeAsync()
        {
            return ValueTask.CompletedTask;
        }
    }
}
