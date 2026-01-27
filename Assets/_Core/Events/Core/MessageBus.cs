using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using GRoll.Core.Interfaces.Infrastructure;
using VContainer;

namespace GRoll.Core.Events
{
    /// <summary>
    /// Type-safe publish/subscribe message bus implementasyonu.
    /// Thread-safe ve memory leak free tasarım.
    /// </summary>
    public sealed class MessageBus : IMessageBus
    {
        private readonly Dictionary<Type, List<Delegate>> _handlers = new();
        private readonly Dictionary<Type, List<Delegate>> _asyncHandlers = new();
        private readonly object _lock = new();
        private readonly IGRollLogger _logger;

        [Inject]
        public MessageBus(IGRollLogger logger)
        {
            _logger = logger;
        }

        public void Publish<T>(T message) where T : IMessage
        {
            if (message == null)
                throw new ArgumentNullException(nameof(message));

            List<Delegate> handlers;
            lock (_lock)
            {
                if (!_handlers.TryGetValue(typeof(T), out handlers))
                    return;

                // Copy to avoid modification during iteration
                handlers = new List<Delegate>(handlers);
            }

            foreach (var handler in handlers)
            {
                try
                {
                    ((Action<T>)handler)(message);
                }
                catch (Exception ex)
                {
                    _logger.LogError($"[MessageBus] Handler error for {typeof(T).Name}: {ex}");
                }
            }
        }

        public IDisposable Subscribe<T>(Action<T> handler) where T : IMessage
        {
            if (handler == null)
                throw new ArgumentNullException(nameof(handler));

            lock (_lock)
            {
                if (!_handlers.ContainsKey(typeof(T)))
                    _handlers[typeof(T)] = new List<Delegate>();

                _handlers[typeof(T)].Add(handler);
            }

            return new Subscription(() =>
            {
                lock (_lock)
                {
                    if (_handlers.TryGetValue(typeof(T), out var list))
                        list.Remove(handler);
                }
            });
        }

        public async UniTask PublishAsync<T>(T message) where T : IMessage
        {
            if (message == null)
                throw new ArgumentNullException(nameof(message));

            // First, publish to sync handlers
            Publish(message);

            // Then, publish to async handlers
            List<Delegate> asyncHandlers;
            lock (_lock)
            {
                if (!_asyncHandlers.TryGetValue(typeof(T), out asyncHandlers))
                    return;

                asyncHandlers = new List<Delegate>(asyncHandlers);
            }

            var tasks = new List<UniTask>();
            foreach (var handler in asyncHandlers)
            {
                try
                {
                    tasks.Add(((Func<T, UniTask>)handler)(message));
                }
                catch (Exception ex)
                {
                    _logger.LogError($"[MessageBus] Async handler error for {typeof(T).Name}: {ex}");
                }
            }

            if (tasks.Count > 0)
            {
                await UniTask.WhenAll(tasks);
            }
        }

        public IDisposable SubscribeAsync<T>(Func<T, UniTask> handler) where T : IMessage
        {
            if (handler == null)
                throw new ArgumentNullException(nameof(handler));

            lock (_lock)
            {
                if (!_asyncHandlers.ContainsKey(typeof(T)))
                    _asyncHandlers[typeof(T)] = new List<Delegate>();

                _asyncHandlers[typeof(T)].Add(handler);
            }

            return new Subscription(() =>
            {
                lock (_lock)
                {
                    if (_asyncHandlers.TryGetValue(typeof(T), out var list))
                        list.Remove(handler);
                }
            });
        }

        /// <summary>
        /// Internal subscription class for automatic unsubscribe.
        /// </summary>
        private sealed class Subscription : IDisposable
        {
            private Action _unsubscribe;
            private bool _disposed;

            public Subscription(Action unsubscribe)
            {
                _unsubscribe = unsubscribe;
            }

            public void Dispose()
            {
                if (_disposed) return;
                _disposed = true;
                _unsubscribe?.Invoke();
                _unsubscribe = null;
            }
        }
    }
}
