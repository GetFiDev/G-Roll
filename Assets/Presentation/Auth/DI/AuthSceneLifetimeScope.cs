using GRoll.Core.Interfaces.UI;
using GRoll.Presentation.Navigation;
using GRoll.Presentation.Services;
using VContainer;
using VContainer.Unity;

namespace GRoll.Presentation.Auth.DI
{
    /// <summary>
    /// Auth Scene için VContainer lifetime scope.
    /// Auth scene'e özgü servisler ve UI servisleri burada register edilir.
    /// Parent: RootLifetimeScope
    /// </summary>
    public class AuthSceneLifetimeScope : LifetimeScope
    {
        protected override void Configure(IContainerBuilder builder)
        {
            // ==================== UI SERVICES ====================
            // Auth scene'de kullanılacak UI servisleri

            // Navigation Service - Auth flow için
            builder.Register<NavigationService>(Lifetime.Scoped)
                .As<INavigationService>();

            // Dialog Service - Login hatası, onay vs. için
            builder.Register<DialogService>(Lifetime.Scoped)
                .As<IDialogService>();

            // Feedback Service - Toast ve haptic
            builder.Register<FeedbackService>(Lifetime.Scoped)
                .As<IFeedbackService>();

            // ==================== AUTH-SPECIFIC SERVICES ====================
            // Auth scene'e özgü ek servisler gerekirse buraya eklenebilir
            // Örn: LoginFormValidator, SocialAuthHandler, vb.
        }
    }
}
