using System;
using System.Collections.Generic;

namespace GRoll.Core.Events
{
    /// <summary>
    /// Birden fazla IDisposable'ı tek bir container'da yönetir.
    /// MonoBehaviour'larda OnDestroy'da tek bir Dispose() çağrısı ile
    /// tüm subscription'ları temizlemek için kullanılır.
    ///
    /// Kullanım:
    /// <code>
    /// private readonly CompositeDisposable _subscriptions = new();
    ///
    /// void Start()
    /// {
    ///     _subscriptions.Add(_messageBus.Subscribe&lt;CurrencyChangedMessage&gt;(OnCurrency));
    ///     _subscriptions.Add(_messageBus.Subscribe&lt;TaskProgressMessage&gt;(OnTask));
    /// }
    ///
    /// void OnDestroy()
    /// {
    ///     _subscriptions.Dispose();
    /// }
    /// </code>
    /// </summary>
    public sealed class CompositeDisposable : IDisposable
    {
        private readonly List<IDisposable> _disposables = new();
        private bool _disposed;
        private readonly object _lock = new();

        /// <summary>
        /// Disposable'ların sayısını döndürür.
        /// </summary>
        public int Count
        {
            get
            {
                lock (_lock)
                {
                    return _disposables.Count;
                }
            }
        }

        /// <summary>
        /// Bu container dispose edilmiş mi?
        /// </summary>
        public bool IsDisposed => _disposed;

        /// <summary>
        /// Yeni bir disposable ekler.
        /// Container zaten dispose edilmişse, eklenen disposable hemen dispose edilir.
        /// </summary>
        public void Add(IDisposable disposable)
        {
            if (disposable == null)
                throw new ArgumentNullException(nameof(disposable));

            bool shouldDispose = false;

            lock (_lock)
            {
                if (_disposed)
                {
                    shouldDispose = true;
                }
                else
                {
                    _disposables.Add(disposable);
                }
            }

            if (shouldDispose)
            {
                disposable.Dispose();
            }
        }

        /// <summary>
        /// Disposable'ı listeden çıkarır ve dispose eder.
        /// </summary>
        public bool Remove(IDisposable disposable)
        {
            if (disposable == null)
                throw new ArgumentNullException(nameof(disposable));

            lock (_lock)
            {
                if (_disposed)
                    return false;

                var removed = _disposables.Remove(disposable);
                if (removed)
                {
                    disposable.Dispose();
                }
                return removed;
            }
        }

        /// <summary>
        /// Tüm disposable'ları temizler (dispose eder) ama container'ı yeniden kullanılabilir bırakır.
        /// </summary>
        public void Clear()
        {
            List<IDisposable> toDispose;

            lock (_lock)
            {
                if (_disposed)
                    return;

                toDispose = new List<IDisposable>(_disposables);
                _disposables.Clear();
            }

            foreach (var disposable in toDispose)
            {
                disposable?.Dispose();
            }
        }

        /// <summary>
        /// Tüm disposable'ları dispose eder ve container'ı kapatır.
        /// Bu metod çağrıldıktan sonra Add() yapılan disposable'lar anında dispose edilir.
        /// </summary>
        public void Dispose()
        {
            List<IDisposable> toDispose;

            lock (_lock)
            {
                if (_disposed)
                    return;

                _disposed = true;
                toDispose = new List<IDisposable>(_disposables);
                _disposables.Clear();
            }

            foreach (var disposable in toDispose)
            {
                disposable?.Dispose();
            }
        }
    }

    /// <summary>
    /// IDisposable için extension method'lar.
    /// </summary>
    public static class DisposableExtensions
    {
        /// <summary>
        /// IDisposable'ı CompositeDisposable'a ekler ve aynı IDisposable'ı döndürür.
        /// Fluent kullanım için.
        /// </summary>
        public static T AddTo<T>(this T disposable, CompositeDisposable composite) where T : IDisposable
        {
            composite.Add(disposable);
            return disposable;
        }
    }
}
