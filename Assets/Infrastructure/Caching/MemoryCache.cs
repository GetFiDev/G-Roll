using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;

namespace GRoll.Infrastructure.Caching
{
    /// <summary>
    /// Generic in-memory cache with TTL support.
    /// Thread-safe implementation using locks.
    /// </summary>
    /// <typeparam name="TKey">Cache key type</typeparam>
    /// <typeparam name="TValue">Cache value type</typeparam>
    public class MemoryCache<TKey, TValue> : IDisposable where TKey : notnull
    {
        private readonly Dictionary<TKey, CacheEntry> _cache = new();
        private readonly object _lock = new();
        private readonly TimeSpan _defaultTtl;
        private readonly int _maxSize;
        private CancellationTokenSource _cleanupCts;
        private bool _disposed;

        /// <summary>
        /// Creates a new memory cache.
        /// </summary>
        /// <param name="defaultTtl">Default time-to-live for cache entries</param>
        /// <param name="maxSize">Maximum number of entries (0 = unlimited)</param>
        /// <param name="cleanupIntervalSeconds">Interval for automatic cleanup (0 = no auto cleanup)</param>
        public MemoryCache(
            TimeSpan defaultTtl,
            int maxSize = 0,
            int cleanupIntervalSeconds = 60)
        {
            _defaultTtl = defaultTtl;
            _maxSize = maxSize;

            if (cleanupIntervalSeconds > 0)
            {
                StartCleanupTask(cleanupIntervalSeconds);
            }
        }

        /// <summary>
        /// Gets a value from the cache.
        /// </summary>
        public bool TryGet(TKey key, out TValue value)
        {
            lock (_lock)
            {
                if (_cache.TryGetValue(key, out var entry))
                {
                    if (!entry.IsExpired)
                    {
                        value = entry.Value;
                        return true;
                    }

                    // Remove expired entry
                    _cache.Remove(key);
                }

                value = default;
                return false;
            }
        }

        /// <summary>
        /// Gets a value from the cache, or adds it using the factory if not present.
        /// </summary>
        public async UniTask<TValue> GetOrAddAsync(TKey key, Func<UniTask<TValue>> factory, TimeSpan? ttl = null)
        {
            // First check if value exists
            if (TryGet(key, out var existingValue))
            {
                return existingValue;
            }

            // Create new value
            var value = await factory();

            // Add to cache
            Set(key, value, ttl);

            return value;
        }

        /// <summary>
        /// Gets a value from the cache, or adds it using the factory if not present.
        /// </summary>
        public TValue GetOrAdd(TKey key, Func<TValue> factory, TimeSpan? ttl = null)
        {
            // First check if value exists
            if (TryGet(key, out var existingValue))
            {
                return existingValue;
            }

            // Create new value
            var value = factory();

            // Add to cache
            Set(key, value, ttl);

            return value;
        }

        /// <summary>
        /// Sets a value in the cache.
        /// </summary>
        public void Set(TKey key, TValue value, TimeSpan? ttl = null)
        {
            var actualTtl = ttl ?? _defaultTtl;
            var expiration = actualTtl == TimeSpan.MaxValue
                ? DateTime.MaxValue
                : DateTime.UtcNow.Add(actualTtl);

            lock (_lock)
            {
                // Check if we need to evict
                if (_maxSize > 0 && _cache.Count >= _maxSize && !_cache.ContainsKey(key))
                {
                    EvictOldest();
                }

                _cache[key] = new CacheEntry(value, expiration);
            }
        }

        /// <summary>
        /// Removes a value from the cache.
        /// </summary>
        public bool Remove(TKey key)
        {
            lock (_lock)
            {
                return _cache.Remove(key);
            }
        }

        /// <summary>
        /// Clears all entries from the cache.
        /// </summary>
        public void Clear()
        {
            lock (_lock)
            {
                _cache.Clear();
            }
        }

        /// <summary>
        /// Gets the number of entries in the cache.
        /// </summary>
        public int Count
        {
            get
            {
                lock (_lock)
                {
                    return _cache.Count;
                }
            }
        }

        /// <summary>
        /// Checks if a key exists and is not expired.
        /// </summary>
        public bool ContainsKey(TKey key)
        {
            return TryGet(key, out _);
        }

        /// <summary>
        /// Removes all expired entries.
        /// </summary>
        public int RemoveExpired()
        {
            lock (_lock)
            {
                var keysToRemove = new List<TKey>();
                foreach (var kvp in _cache)
                {
                    if (kvp.Value.IsExpired)
                    {
                        keysToRemove.Add(kvp.Key);
                    }
                }

                foreach (var key in keysToRemove)
                {
                    _cache.Remove(key);
                }

                return keysToRemove.Count;
            }
        }

        private void EvictOldest()
        {
            // Find the oldest entry (by expiration time)
            TKey oldestKey = default;
            DateTime oldestExpiration = DateTime.MaxValue;

            foreach (var kvp in _cache)
            {
                if (kvp.Value.Expiration < oldestExpiration)
                {
                    oldestExpiration = kvp.Value.Expiration;
                    oldestKey = kvp.Key;
                }
            }

            if (oldestKey != null)
            {
                _cache.Remove(oldestKey);
            }
        }

        private void StartCleanupTask(int intervalSeconds)
        {
            _cleanupCts = new CancellationTokenSource();
            RunCleanupLoop(intervalSeconds, _cleanupCts.Token).Forget();
        }

        private async UniTaskVoid RunCleanupLoop(int intervalSeconds, CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                await UniTask.Delay(TimeSpan.FromSeconds(intervalSeconds), cancellationToken: cancellationToken);

                if (cancellationToken.IsCancellationRequested) break;

                RemoveExpired();
            }
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            _cleanupCts?.Cancel();
            _cleanupCts?.Dispose();
            _cleanupCts = null;

            Clear();
        }

        private class CacheEntry
        {
            public TValue Value { get; }
            public DateTime Expiration { get; }
            public bool IsExpired => DateTime.UtcNow > Expiration;

            public CacheEntry(TValue value, DateTime expiration)
            {
                Value = value;
                Expiration = expiration;
            }
        }
    }
}
