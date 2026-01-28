using GRoll.Core.Interfaces.UI;
using GRoll.Presentation.Navigation;
using GRoll.Presentation.Services;
using VContainer;
using VContainer.Unity;

namespace GRoll.Presentation.DI
{
    /// <summary>
    /// Presentation layer lifetime scope.
    /// UI navigation ve dialog servisleri burada register edilir.
    /// </summary>
    public class PresentationLifetimeScope : LifetimeScope
    {
        protected override void Configure(IContainerBuilder builder)
        {
            // Navigation Service - Scoped (her sahne için ayrı instance)
            builder.Register<NavigationService>(Lifetime.Scoped)
                .As<INavigationService>();

            // Dialog Service - Scoped
            builder.Register<DialogService>(Lifetime.Scoped)
                .As<IDialogService>();

            // Feedback Service - Scoped (toast, haptic, dialog facade)
            builder.Register<FeedbackService>(Lifetime.Scoped)
                .As<IFeedbackService>();
        }
    }
}
