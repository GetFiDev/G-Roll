using System;
using Cysharp.Threading.Tasks;

namespace GRoll.Core.Events
{
    /// <summary>
    /// Type-safe publish/subscribe message bus.
    /// Memory leak önlemek için IDisposable subscription döndürür.
    ///
    /// Kullanım:
    /// <code>
    /// // Subscribe
    /// _subscription = _messageBus.Subscribe&lt;CurrencyChangedMessage&gt;(OnCurrencyChanged);
    ///
    /// // Publish
    /// _messageBus.Publish(new CurrencyChangedMessage(...));
    ///
    /// // Unsubscribe (OnDestroy'da)
    /// _subscription?.Dispose();
    /// </code>
    /// </summary>
    public interface IMessageBus
    {
        /// <summary>
        /// Message yayınlar. Tüm subscriber'lara senkron olarak iletilir.
        /// </summary>
        /// <typeparam name="T">Message tipi</typeparam>
        /// <param name="message">Yayınlanacak message instance'ı</param>
        void Publish<T>(T message) where T : IMessage;

        /// <summary>
        /// Message tipine subscribe olur.
        /// Dönen IDisposable dispose edildiğinde subscription iptal edilir.
        /// </summary>
        /// <typeparam name="T">Dinlenecek message tipi</typeparam>
        /// <param name="handler">Message geldiğinde çağrılacak handler</param>
        /// <returns>Subscription'ı iptal etmek için kullanılacak IDisposable</returns>
        IDisposable Subscribe<T>(Action<T> handler) where T : IMessage;

        /// <summary>
        /// Async message yayınlar. Tüm async subscriber'ları bekler.
        /// </summary>
        /// <typeparam name="T">Message tipi</typeparam>
        /// <param name="message">Yayınlanacak message instance'ı</param>
        UniTask PublishAsync<T>(T message) where T : IMessage;

        /// <summary>
        /// Async handler ile subscribe olur.
        /// </summary>
        /// <typeparam name="T">Dinlenecek message tipi</typeparam>
        /// <param name="handler">Message geldiğinde çağrılacak async handler</param>
        /// <returns>Subscription'ı iptal etmek için kullanılacak IDisposable</returns>
        IDisposable SubscribeAsync<T>(Func<T, UniTask> handler) where T : IMessage;
    }
}
