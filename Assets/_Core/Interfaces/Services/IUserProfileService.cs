using System;
using Cysharp.Threading.Tasks;
using GRoll.Core.Optimistic;

namespace GRoll.Core.Interfaces.Services
{
    /// <summary>
    /// Kullanıcı profili yönetimi için service interface.
    /// Username, avatar, stats ve user-specific data yönetimini yapar.
    /// </summary>
    public interface IUserProfileService
    {
        /// <summary>
        /// Mevcut kullanıcı profili
        /// </summary>
        UserProfile CurrentProfile { get; }

        /// <summary>
        /// Kullanıcının görünen adı (kısayol)
        /// </summary>
        string DisplayName { get; }

        /// <summary>
        /// Profil yüklendi mi?
        /// </summary>
        bool IsLoaded { get; }

        /// <summary>
        /// Profil tamamlandı mı? (username set edilmiş mi?)
        /// </summary>
        bool IsComplete { get; }

        /// <summary>
        /// Profili sunucudan yükler.
        /// </summary>
        UniTask<OperationResult<UserProfile>> LoadProfileAsync();

        /// <summary>
        /// Username'i ayarlar veya günceller.
        /// </summary>
        UniTask<OperationResult> SetUsernameAsync(string username);

        /// <summary>
        /// Display name'i optimistic olarak günceller.
        /// </summary>
        UniTask<OperationResult> UpdateDisplayNameOptimisticAsync(string displayName);

        /// <summary>
        /// Avatar'ı günceller.
        /// </summary>
        UniTask<OperationResult> SetAvatarAsync(string avatarId);

        /// <summary>
        /// Profili günceller (genel amaçlı).
        /// </summary>
        UniTask<OperationResult> UpdateProfileAsync(UserProfileUpdateRequest request);

        /// <summary>
        /// Referral kodunu uygular.
        /// </summary>
        UniTask<OperationResult> ApplyReferralCodeAsync(string code);

        /// <summary>
        /// Kullanıcının referral sayısını döner.
        /// </summary>
        UniTask<int> GetReferralCountAsync();

        /// <summary>
        /// Profil değiştiğinde tetiklenen event.
        /// </summary>
        event Action<UserProfile> OnProfileChanged;
    }

    /// <summary>
    /// Kullanıcı profili
    /// </summary>
    public class UserProfile
    {
        public string UserId { get; set; }
        public string Username { get; set; }
        public string AvatarId { get; set; }
        public bool HasElitePass { get; set; }
        public int TotalGamesPlayed { get; set; }
        public int HighScore { get; set; }
        public int TotalCoinsEarned { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime LastLoginAt { get; set; }

        /// <summary>
        /// Profil tamamlandı mı?
        /// </summary>
        public bool IsComplete => !string.IsNullOrWhiteSpace(Username) && Username != "Guest";
    }

    /// <summary>
    /// Profil güncelleme isteği
    /// </summary>
    public class UserProfileUpdateRequest
    {
        public string Username { get; set; }
        public string AvatarId { get; set; }
    }
}
