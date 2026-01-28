using GRoll.Core.Events;
using GRoll.Core.Interfaces.Infrastructure;
using GRoll.Core.Interfaces.Services;
using GRoll.Core.SceneManagement;
using GRoll.Domain.Application;
using GRoll.Domain.Shop;
using GRoll.Domain.Social;
using GRoll.Infrastructure.Firebase;
using GRoll.Infrastructure.Firebase.Interfaces;
using GRoll.Infrastructure.Firebase.Services;
using GRoll.Infrastructure.Logging;
using GRoll.Infrastructure.Services;
using VContainer;
using VContainer.Unity;

namespace GRoll.Domain.DI.Installers
{
    /// <summary>
    /// Root lifetime scope - uygulama boyunca yaşayan singleton servisler.
    /// Infrastructure ve Core servisleri burada register edilir.
    /// DontDestroyOnLoad olarak Boot Scene'de oluşturulur.
    /// </summary>
    public class RootLifetimeScope : LifetimeScope
    {
        protected override void Configure(IContainerBuilder builder)
        {
            // ==================== INFRASTRUCTURE ====================

            // Logger - Singleton (tüm uygulama boyunca tek instance)
            builder.Register<IGRollLogger, UnityLogger>(Lifetime.Singleton);

            // MessageBus - Singleton (event bus)
            builder.Register<IMessageBus, MessageBus>(Lifetime.Singleton);

            // Firebase Gateway - Singleton with auto-initialization
            // IAsyncStartable ensures InitializeAsync is called at startup
            builder.Register<FirebaseGateway>(Lifetime.Singleton)
                .As<IFirebaseGateway>()
                .As<IAsyncStartable>();

            // ==================== SCENE MANAGEMENT ====================

            // SceneFlowManager - Singleton (sahne geçişlerini yönetir)
            builder.Register<SceneFlowManager>(Lifetime.Singleton)
                .As<ISceneFlowManager>();

            // ==================== APPLICATION SERVICES ====================
            // Uygulama seviyesi servisler (App lifecycle, Auth, Profile)

            // Auth Service - Singleton (kullanıcı kimlik doğrulama)
            builder.Register<AuthService>(Lifetime.Singleton)
                .As<IAuthService>();

            // User Profile Service - Singleton (kullanıcı profil yönetimi)
            builder.Register<UserProfileService>(Lifetime.Singleton)
                .As<IUserProfileService>();

            // App Flow Service - Singleton (boot sequence orchestration)
            builder.Register<AppFlowService>(Lifetime.Singleton)
                .As<IAppFlowService>();

            // ==================== UTILITY SERVICES ====================
            // Audio, Haptic, Ad servisleri

            // Audio Service - Singleton (müzik ve ses efektleri)
            builder.Register<AudioService>(Lifetime.Singleton)
                .As<IAudioService>();

            // Haptic Service - Singleton (titreşim feedback)
            builder.Register<HapticService>(Lifetime.Singleton)
                .As<IHapticService>();

            // Ad Service - Singleton (reklam yönetimi)
            builder.Register<AdService>(Lifetime.Singleton)
                .As<IAdService>();

            // IAP Service - Singleton (in-app purchase yönetimi)
            builder.Register<IAPService>(Lifetime.Singleton)
                .As<IIAPService>();

            // ==================== REMOTE SERVICES ====================
            // Firebase uzerinden server iletisimi saglayan servisler

            builder.Register<ICurrencyRemoteService, CurrencyRemoteService>(Lifetime.Singleton);
            builder.Register<IInventoryRemoteService, InventoryRemoteService>(Lifetime.Singleton);
            builder.Register<ITaskRemoteService, TaskRemoteService>(Lifetime.Singleton);
            builder.Register<IAchievementRemoteService, AchievementRemoteService>(Lifetime.Singleton);
            builder.Register<IEnergyRemoteService, EnergyRemoteService>(Lifetime.Singleton);
            builder.Register<ISessionRemoteService, SessionRemoteService>(Lifetime.Singleton);
            builder.Register<ILeaderboardRemoteService, LeaderboardRemoteService>(Lifetime.Singleton);

            // NEW: Referral Remote Service
            builder.Register<IReferralRemoteService, ReferralRemoteService>(Lifetime.Singleton);

            // NEW: Player Stats Remote Service
            builder.Register<IPlayerStatsRemoteService, PlayerStatsRemoteService>(Lifetime.Singleton);

            // NEW: IAP Remote Service (purchase verification)
            builder.Register<IIAPRemoteService, IAPRemoteService>(Lifetime.Singleton);

            // NEW: Remote Item Service (shop items)
            builder.Register<IRemoteItemService, RemoteItemService>(Lifetime.Singleton);

            // ==================== DOMAIN SERVICES (App-Level Singletons) ====================

            // NEW: Referral Service - Singleton (kullanici referral yonetimi)
            builder.Register<IReferralService, Social.ReferralService>(Lifetime.Singleton);

            // NEW: Player Stats Service - Singleton (kullanici istatistikleri)
            builder.Register<IPlayerStatsService, PlayerStatsService>(Lifetime.Singleton);

            // NEW: Item Service - Singleton (shop item yonetimi)
            builder.Register<IItemService, ItemService>(Lifetime.Singleton);
        }
    }
}
