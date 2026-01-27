using System;
using Cysharp.Threading.Tasks;
using GRoll.Core.Optimistic;

namespace GRoll.Core.Interfaces.Services
{
    /// <summary>
    /// Authentication (kimlik doğrulama) için service interface.
    /// Login, logout ve user state yönetimini yapar.
    /// </summary>
    public interface IAuthService
    {
        /// <summary>
        /// Kullanıcı giriş yapmış mı?
        /// </summary>
        bool IsAuthenticated { get; }

        /// <summary>
        /// Mevcut kullanıcı ID'si
        /// </summary>
        string CurrentUserId { get; }

        /// <summary>
        /// Mevcut kullanıcı bilgileri
        /// </summary>
        UserInfo CurrentUser { get; }

        /// <summary>
        /// Anonim olarak giriş yapar.
        /// </summary>
        UniTask<OperationResult<UserInfo>> SignInAnonymouslyAsync();

        /// <summary>
        /// Google ile giriş yapar.
        /// </summary>
        UniTask<OperationResult<UserInfo>> SignInWithGoogleAsync();

        /// <summary>
        /// Apple ile giriş yapar.
        /// </summary>
        UniTask<OperationResult<UserInfo>> SignInWithAppleAsync();

        /// <summary>
        /// Çıkış yapar.
        /// </summary>
        UniTask SignOutAsync();

        /// <summary>
        /// Anonim hesabı kalıcı hesaba bağlar.
        /// </summary>
        UniTask<OperationResult> LinkWithGoogleAsync();

        /// <summary>
        /// Auth state değiştiğinde tetiklenen event.
        /// </summary>
        event Action<AuthStateChangedEventArgs> OnAuthStateChanged;
    }

    /// <summary>
    /// Kullanıcı bilgileri
    /// </summary>
    public class UserInfo
    {
        public string UserId { get; set; }
        public string DisplayName { get; set; }
        public string Email { get; set; }
        public string PhotoUrl { get; set; }
        public bool IsAnonymous { get; set; }
        public AuthProvider Provider { get; set; }
    }

    /// <summary>
    /// Auth provider tipleri
    /// </summary>
    public enum AuthProvider
    {
        Anonymous,
        Google,
        Apple,
        Email
    }

    /// <summary>
    /// Auth state change event args
    /// </summary>
    public class AuthStateChangedEventArgs
    {
        public bool IsAuthenticated { get; set; }
        public UserInfo User { get; set; }
        public AuthStateChangeReason Reason { get; set; }
    }

    /// <summary>
    /// Auth state değişiklik sebepleri
    /// </summary>
    public enum AuthStateChangeReason
    {
        SignedIn,
        SignedOut,
        TokenRefreshed,
        AccountLinked,
        SessionExpired
    }
}
