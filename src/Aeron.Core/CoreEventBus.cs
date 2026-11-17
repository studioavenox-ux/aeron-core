using Aeron.Contracts;

namespace Aeron.Core;

/// <summary>
/// Thread-safe, strongly typed in-process event bus with isolated subscriber fault handling.
/// </summary>
public sealed class CoreEventBus : ICoreEventBus
{
    private readonly Lock _sync = new();
    private readonly Dictionary<Type, Delegate[]> _handlersByType = [];
    private readonly IAeronLogger? _logger;

    public CoreEventBus(IAeronLogger? logger = null)
    {
        _logger = logger;
    }

    public IDisposable Subscribe<TEvent>(Action<TEvent> handler) where TEvent : class, ICoreEvent
    {
        ArgumentNullException.ThrowIfNull(handler);

        var eventType = typeof(TEvent);
        lock (_sync)
        {
            if (_handlersByType.TryGetValue(eventType, out var existing))
            {
                var updated = new Delegate[existing.Length + 1];
                Array.Copy(existing, updated, existing.Length);
                updated[existing.Length] = handler;
                _handlersByType[eventType] = updated;
            }
            else
            {
                _handlersByType[eventType] = [handler];
            }
        }

        return new Subscription(this, eventType, handler);
    }

    public void Publish<TEvent>(TEvent coreEvent) where TEvent : class, ICoreEvent
    {
        ArgumentNullException.ThrowIfNull(coreEvent);

        Delegate[]? snapshot;
        lock (_sync)
        {
            _handlersByType.TryGetValue(typeof(TEvent), out snapshot);
        }

        if (snapshot is null || snapshot.Length == 0)
        {
            return;
        }

        foreach (var candidate in snapshot)
        {
            if (candidate is not Action<TEvent> typedHandler)
            {
                continue;
            }

            try
            {
                typedHandler(coreEvent);
            }
            catch (Exception ex)
            {
                // Isolate subscriber faults so core lifecycle transitions never fail due to an observer.
                _logger?.LogError(
                    nameof(CoreEventBus),
                    $"Event subscriber faulted while handling '{typeof(TEvent).Name}'.",
                    ex);
            }
        }
    }

    private void Unsubscribe(Type eventType, Delegate handler)
    {
        lock (_sync)
        {
            if (!_handlersByType.TryGetValue(eventType, out var existing))
            {
                return;
            }

            var index = Array.IndexOf(existing, handler);
            if (index < 0)
            {
                return;
            }

            if (existing.Length == 1)
            {
                _handlersByType.Remove(eventType);
                return;
            }

            var updated = new Delegate[existing.Length - 1];
            if (index > 0)
            {
                Array.Copy(existing, 0, updated, 0, index);
            }

            if (index < existing.Length - 1)
            {
                Array.Copy(existing, index + 1, updated, index, existing.Length - index - 1);
            }

            _handlersByType[eventType] = updated;
        }
    }

    private sealed class Subscription : IDisposable
    {
        private CoreEventBus? _owner;
        private readonly Type _eventType;
        private readonly Delegate _handler;

        public Subscription(CoreEventBus owner, Type eventType, Delegate handler)
        {
            _owner = owner;
            _eventType = eventType;
            _handler = handler;
        }

        public void Dispose()
        {
            var owner = Interlocked.Exchange(ref _owner, null);
            owner?.Unsubscribe(_eventType, _handler);
        }
    }
}
