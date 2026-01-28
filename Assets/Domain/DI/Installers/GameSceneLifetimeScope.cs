using GRoll.Core.Interfaces.Services;
using GRoll.Domain.Economy;
using GRoll.Domain.Gameplay;
using GRoll.Domain.Progression;
using GRoll.Domain.Social;
using VContainer;
using VContainer.Unity;

namespace GRoll.Domain.DI.Installers
{
    /// <summary>
    /// Game scene lifetime scope - oyun sahnesinde yaşayan servisler.
    /// Domain servisleri (01.2 Phase Service Layer) burada register edilir.
    /// RootLifetimeScope'un child'ı olarak çalışır.
    /// </summary>
    public class GameSceneLifetimeScope : LifetimeScope
    {
        protected override void Configure(IContainerBuilder builder)
        {
            // ==================== DOMAIN SERVICES ====================
            // Optimistic update pattern kullanan servisler

            // Economy Domain
            builder.Register<ICurrencyService, CurrencyService>(Lifetime.Scoped);
            builder.Register<IInventoryService, InventoryService>(Lifetime.Scoped);

            // Progression Domain
            builder.Register<ITaskService, TaskService>(Lifetime.Scoped);
            builder.Register<IAchievementService, AchievementService>(Lifetime.Scoped);

            // Gameplay Domain
            builder.Register<IEnergyService, EnergyService>(Lifetime.Scoped);
            builder.Register<ISessionService, SessionService>(Lifetime.Scoped);
            builder.Register<IGameStateService, GameStateService>(Lifetime.Scoped);

            // Social Domain
            builder.Register<ILeaderboardService, LeaderboardService>(Lifetime.Scoped);
        }
    }
}
