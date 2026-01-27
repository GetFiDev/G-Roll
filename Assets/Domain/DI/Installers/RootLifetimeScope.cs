using GRoll.Core.Events;
using GRoll.Core.Interfaces.Infrastructure;
using GRoll.Infrastructure.Firebase;
using GRoll.Infrastructure.Firebase.Interfaces;
using GRoll.Infrastructure.Firebase.Services;
using GRoll.Infrastructure.Logging;
using VContainer;
using VContainer.Unity;

namespace GRoll.Domain.DI.Installers
{
    /// <summary>
    /// Root lifetime scope - uygulama boyunca yaşayan singleton servisler.
    /// Infrastructure ve Core servisleri burada register edilir.
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

            // ==================== REMOTE SERVICES ====================
            // Firebase üzerinden server iletişimi sağlayan servisler

            builder.Register<ICurrencyRemoteService, CurrencyRemoteService>(Lifetime.Singleton);
            builder.Register<IInventoryRemoteService, InventoryRemoteService>(Lifetime.Singleton);
            builder.Register<ITaskRemoteService, TaskRemoteService>(Lifetime.Singleton);
            builder.Register<IAchievementRemoteService, AchievementRemoteService>(Lifetime.Singleton);
            builder.Register<IEnergyRemoteService, EnergyRemoteService>(Lifetime.Singleton);
            builder.Register<ISessionRemoteService, SessionRemoteService>(Lifetime.Singleton);
            builder.Register<ILeaderboardRemoteService, LeaderboardRemoteService>(Lifetime.Singleton);
        }
    }
}
