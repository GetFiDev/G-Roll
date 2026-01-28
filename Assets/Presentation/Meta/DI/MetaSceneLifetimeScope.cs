using GRoll.Core.Interfaces.UI;
using GRoll.Presentation.Navigation;
using GRoll.Presentation.Services;
using VContainer;
using VContainer.Unity;

namespace GRoll.Presentation.Meta.DI
{
    /// <summary>
    /// Meta Scene için VContainer lifetime scope.
    /// Meta (ana menü) scene'e özgü servisler ve UI servisleri burada register edilir.
    /// Parent: RootLifetimeScope (veya GameSceneLifetimeScope)
    /// </summary>
    public class MetaSceneLifetimeScope : LifetimeScope
    {
        protected override void Configure(IContainerBuilder builder)
        {
            // ==================== UI SERVICES ====================
            // Meta scene'de kullanılacak UI servisleri

            // Navigation Service - Screen geçişleri için
            builder.Register<NavigationService>(Lifetime.Scoped)
                .As<INavigationService>();

            // Dialog Service - Popup'lar, onay dialogları vs.
            builder.Register<DialogService>(Lifetime.Scoped)
                .As<IDialogService>();

            // Feedback Service - Toast ve haptic
            builder.Register<FeedbackService>(Lifetime.Scoped)
                .As<IFeedbackService>();

            // ==================== META-SPECIFIC SERVICES ====================
            // Meta scene'e özgü ek servisler gerekirse buraya eklenebilir
            // Örn: ShopCatalogService, LeaderboardCacheService, vb.
        }
    }
}
