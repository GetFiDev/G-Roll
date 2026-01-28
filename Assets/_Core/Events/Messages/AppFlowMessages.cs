using GRoll.Core.Interfaces.Services;

namespace GRoll.Core.Events.Messages
{
    /// <summary>
    /// App flow state değiştiğinde yayınlanan message.
    /// </summary>
    public readonly struct AppFlowStateChangedMessage : IMessage
    {
        public AppFlowState PreviousState { get; }
        public AppFlowState NewState { get; }
        public string Message { get; }

        public AppFlowStateChangedMessage(AppFlowState previous, AppFlowState newState, string message = null)
        {
            PreviousState = previous;
            NewState = newState;
            Message = message;
        }
    }

    /// <summary>
    /// Login bekleniyor durumuna geçildiğinde yayınlanan message.
    /// UI bu message'ı dinleyerek login panelini gösterir.
    /// </summary>
    public readonly struct AppFlowWaitingForAuthMessage : IMessage
    {
    }

    /// <summary>
    /// Profil tamamlanması bekleniyor durumuna geçildiğinde yayınlanan message.
    /// UI bu message'ı dinleyerek set name panelini gösterir.
    /// </summary>
    public readonly struct AppFlowWaitingForProfileMessage : IMessage
    {
    }

    /// <summary>
    /// Game data yüklenirken yayınlanan message.
    /// UI loading göstergesi için kullanılabilir.
    /// </summary>
    public readonly struct AppFlowLoadingGameDataMessage : IMessage
    {
    }

    /// <summary>
    /// Uygulama hazır durumuna geçtiğinde yayınlanan message.
    /// UI meta ekranını gösterir.
    /// </summary>
    public readonly struct AppFlowReadyMessage : IMessage
    {
    }
}
