namespace GRoll.Core.Events.Messages
{
    /// <summary>
    /// Kamera transition basladiginda yayinlanan message
    /// </summary>
    public readonly struct CameraTransitionStartedMessage : IMessage
    {
        public string TransitionType { get; }

        public CameraTransitionStartedMessage(string transitionType)
        {
            TransitionType = transitionType;
        }
    }

    /// <summary>
    /// Kamera transition tamamlandiginda yayinlanan message
    /// </summary>
    public readonly struct CameraTransitionCompletedMessage : IMessage { }

    /// <summary>
    /// Kamera Z-axis tracking basladiginda yayinlanan message
    /// </summary>
    public readonly struct CameraZTrackingStartedMessage : IMessage { }

    /// <summary>
    /// Kamera Z-axis tracking durdugunda yayinlanan message
    /// </summary>
    public readonly struct CameraZTrackingStoppedMessage : IMessage { }
}
