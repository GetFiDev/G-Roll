using System;
using Cysharp.Threading.Tasks;

namespace GRoll.Infrastructure.Firebase.Interfaces
{
    /// <summary>
    /// Firebase servislerine merkezi erişim noktası.
    /// Tüm Firebase çağrıları bu interface üzerinden yapılır.
    /// </summary>
    public interface IFirebaseGateway
    {
        /// <summary>
        /// Firebase'i başlatır.
        /// </summary>
        UniTask InitializeAsync();

        /// <summary>
        /// Firebase Cloud Function çağırır.
        /// </summary>
        /// <typeparam name="T">Response tipi</typeparam>
        /// <param name="functionName">Function adı</param>
        /// <param name="data">Request datası</param>
        /// <returns>Parsed response</returns>
        UniTask<T> CallFunctionAsync<T>(string functionName, object data = null);

        /// <summary>
        /// Firebase bağlantı durumu
        /// </summary>
        bool IsInitialized { get; }

        // ==================== AUTH ====================

        /// <summary>
        /// Kullanıcı giriş yapmış mı?
        /// </summary>
        bool IsAuthenticated { get; }

        /// <summary>
        /// Anonim kullanıcı mı?
        /// </summary>
        bool IsAnonymous { get; }

        /// <summary>
        /// Mevcut kullanıcı ID'si
        /// </summary>
        string CurrentUserId { get; }

        /// <summary>
        /// Mevcut kullanıcı görünen adı
        /// </summary>
        string CurrentUserDisplayName { get; }

        /// <summary>
        /// Mevcut kullanıcı email'i
        /// </summary>
        string CurrentUserEmail { get; }

        /// <summary>
        /// Anonim olarak giriş yapar.
        /// </summary>
        UniTask<bool> SignInAnonymouslyAsync();

        /// <summary>
        /// Google ile giriş yapar.
        /// </summary>
        UniTask<bool> SignInWithGoogleAsync();

        /// <summary>
        /// Game Center ile giriş yapar (iOS).
        /// </summary>
        UniTask<bool> SignInWithGameCenterAsync();

        /// <summary>
        /// Apple ile giriş yapar.
        /// Deprecated: Use SignInWithGameCenterAsync for iOS.
        /// </summary>
        [System.Obsolete("Use SignInWithGameCenterAsync instead")]
        UniTask<bool> SignInWithAppleAsync();

        /// <summary>
        /// Çıkış yapar.
        /// </summary>
        UniTask SignOutAsync();

        /// <summary>
        /// Anonim hesabı Google'a bağlar.
        /// </summary>
        UniTask<bool> LinkWithGoogleAsync();

        /// <summary>
        /// Hesabı siler.
        /// </summary>
        UniTask<bool> DeleteAccountAsync();

        /// <summary>
        /// Auth state değiştiğinde tetiklenir.
        /// </summary>
        event Action<bool, string> OnAuthStateChanged;
    }
}
