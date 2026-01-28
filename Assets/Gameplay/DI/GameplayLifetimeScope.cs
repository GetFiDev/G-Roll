using GRoll.Core.Interfaces.Services;
using GRoll.Gameplay.Camera;
using GRoll.Gameplay.Scoring;
using GRoll.Gameplay.Session;
using GRoll.Gameplay.Spawning;
using GRoll.Gameplay.Services;
using VContainer;
using VContainer.Unity;

namespace GRoll.Gameplay.DI
{
    /// <summary>
    /// DI container configuration for Gameplay systems.
    /// Registers all gameplay-related services and controllers.
    /// </summary>
    public class GameplayLifetimeScope : LifetimeScope
    {
        protected override void Configure(IContainerBuilder builder)
        {
            // Speed and booster management (replaces GameplayLogicApplier)
            builder.Register<GameplaySpeedService>(Lifetime.Singleton)
                .As<IGameplaySpeedService>();

            // Camera management (replaces GameplayCameraManager)
            builder.Register<CameraService>(Lifetime.Scoped)
                .As<ICameraService>();

            // Session management
            builder.Register<GameplaySessionController>(Lifetime.Singleton)
                .As<IGameplaySessionController>();

            // Scoring system
            builder.Register<ScoreManager>(Lifetime.Singleton)
                .As<IScoreManager>();

            // Player spawning
            builder.Register<PlayerSpawnController>(Lifetime.Singleton)
                .As<IPlayerSpawnController>();

            // Main gameplay orchestrator
            builder.Register<GameplayManager>(Lifetime.Singleton)
                .AsImplementedInterfaces()
                .AsSelf();
        }
    }
}
